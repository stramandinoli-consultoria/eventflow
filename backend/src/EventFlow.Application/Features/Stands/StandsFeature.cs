using EventFlow.Application.Abstractions;
using EventFlow.Application.Common;
using EventFlow.Application.Dtos;
using EventFlow.Domain;
using EventFlow.Domain.Events;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Application.Features.Stands;

public sealed record ListStandsQuery(string? EventId = null) : IQuery<IReadOnlyList<StandDto>>;
public sealed record UpsertStandCommand(string Id, string EventId, string Name, StandType Category, string? Description,
    string? Cover, decimal Rating, int X, int Y, int Width, int Height) : ICommand<StandDto>;
public sealed record MoveStandCommand(string Id, int X, int Y) : ICommand<StandDto>;
public sealed record DeleteStandCommand(string Id) : ICommand<Unit>;

/// <summary>Layout do mapa em JSON (mesmo formato do botão "Exportar JSON").</summary>
public sealed record EventMapDto(string EventId, MapLayoutDto Map);
public sealed record MapLayoutDto(IReadOnlyList<MapStandDto> Stands);
public sealed record MapStandDto(string Id, string Name, StandType Type, int X, int Y, int Width, int Height);
public sealed record GetEventMapQuery(string EventId) : IQuery<EventMapDto>;
public sealed record SaveEventMapCommand(string EventId, IReadOnlyList<MapStandDto> Stands) : ICommand<EventMapDto>;

public sealed class UpsertStandValidator : AbstractValidator<UpsertStandCommand>
{
    public UpsertStandValidator()
    {
        RuleFor(x => x.Id).ValidId();
        RuleFor(x => x.EventId).ValidId();
        RuleFor(x => x.Name).MaximumLength(120);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Rating).InclusiveBetween(0, 5);
        RuleFor(x => x.Cover).HttpUrl();
    }
}

public sealed class SaveEventMapValidator : AbstractValidator<SaveEventMapCommand>
{
    public SaveEventMapValidator()
    {
        RuleFor(x => x.EventId).ValidId();
        RuleForEach(x => x.Stands).ChildRules(s =>
        {
            s.RuleFor(x => x.Id).ValidId();
            s.RuleFor(x => x.Type).IsInEnum();
        });
    }
}

internal sealed class ListStandsHandler(IAppDbContext db) : IRequestHandler<ListStandsQuery, IReadOnlyList<StandDto>>
{
    public async Task<Result<IReadOnlyList<StandDto>>> Handle(ListStandsQuery query, CancellationToken ct)
    {
        var stands = db.Stands.AsNoTracking();
        if (query.EventId is not null) stands = stands.Where(s => s.EventId == query.EventId);
        var list = await stands.OrderBy(s => s.EventId).ThenBy(s => s.Id).ToListAsync(ct);
        return list.Select(s => s.ToDto()).ToList();
    }
}

internal sealed class UpsertStandHandler(IAppDbContext db, AccessScope access, AuditTrail audit, IRealtimeNotifier realtime)
    : IRequestHandler<UpsertStandCommand, StandDto>
{
    public async Task<Result<StandDto>> Handle(UpsertStandCommand cmd, CancellationToken ct)
    {
        if (!access.IsAdmin) return Error.Forbidden();
        var stand = await db.Stands.FirstOrDefaultAsync(s => s.Id == cmd.Id, ct);
        if (stand is null)
        {
            if (!await db.Events.AnyAsync(e => e.Id == cmd.EventId, ct)) return Error.NotFound("Evento não encontrado.");
            stand = Stand.Create(cmd.Id, cmd.EventId, cmd.Name, cmd.Category, cmd.Description, cmd.Cover, cmd.Rating,
                cmd.X, cmd.Y, cmd.Width, cmd.Height);
            db.Stands.Add(stand);
            audit.Record("create", "stand", stand.Id, cmd);
        }
        else
        {
            stand.Update(cmd.Name, cmd.Category, cmd.Description, cmd.Cover, cmd.Rating, cmd.Width, cmd.Height);
            stand.MoveTo(cmd.X, cmd.Y);
        }

        await db.SaveChangesAsync(ct);
        await realtime.CatalogChangedAsync("stands", ct);
        return stand.ToDto();
    }
}

internal sealed class MoveStandHandler(IAppDbContext db, AccessScope access, IRealtimeNotifier realtime)
    : IRequestHandler<MoveStandCommand, StandDto>
{
    public async Task<Result<StandDto>> Handle(MoveStandCommand cmd, CancellationToken ct)
    {
        if (!access.IsAdmin) return Error.Forbidden();
        var stand = await db.Stands.FirstOrDefaultAsync(s => s.Id == cmd.Id, ct);
        if (stand is null) return Error.NotFound("Stand não encontrado.");
        stand.MoveTo(cmd.X, cmd.Y);
        await db.SaveChangesAsync(ct);
        await realtime.CatalogChangedAsync("stands", ct);
        return stand.ToDto();
    }
}

internal sealed class DeleteStandHandler(IAppDbContext db, AccessScope access, AuditTrail audit, IRealtimeNotifier realtime)
    : IRequestHandler<DeleteStandCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteStandCommand cmd, CancellationToken ct)
    {
        if (!access.IsAdmin) return Error.Forbidden();
        var stand = await db.Stands.FirstOrDefaultAsync(s => s.Id == cmd.Id, ct);
        if (stand is null) return Error.NotFound("Stand não encontrado.");

        foreach (var partner in await db.Partners.Where(p => p.StandId == cmd.Id).ToListAsync(ct))
            partner.LinkStand(null);
        db.Stands.Remove(stand); // produtos do stand saem junto (cascade)
        audit.Record("delete", "stand", stand.Id);
        await db.SaveChangesAsync(ct);
        await realtime.CatalogChangedAsync("stands", ct);
        return Unit.Value;
    }
}

internal sealed class GetEventMapHandler(IAppDbContext db) : IRequestHandler<GetEventMapQuery, EventMapDto>
{
    public async Task<Result<EventMapDto>> Handle(GetEventMapQuery query, CancellationToken ct)
    {
        if (!await db.Events.AnyAsync(e => e.Id == query.EventId, ct)) return Error.NotFound("Evento não encontrado.");
        var stands = await db.Stands.AsNoTracking().Where(s => s.EventId == query.EventId).OrderBy(s => s.Id).ToListAsync(ct);
        return new EventMapDto(query.EventId, new MapLayoutDto(
            stands.Select(s => new MapStandDto(s.Id, s.Name, s.Category, s.X, s.Y, s.Width, s.Height)).ToList()));
    }
}

/// <summary>Salva o layout inteiro: atualiza posições/tamanhos e cria os elementos novos.</summary>
internal sealed class SaveEventMapHandler(IAppDbContext db, AccessScope access, AuditTrail audit, IRealtimeNotifier realtime,
    IRequestHandler<GetEventMapQuery, EventMapDto> getMap) : IRequestHandler<SaveEventMapCommand, EventMapDto>
{
    public async Task<Result<EventMapDto>> Handle(SaveEventMapCommand cmd, CancellationToken ct)
    {
        if (!access.IsAdmin) return Error.Forbidden();
        if (!await db.Events.AnyAsync(e => e.Id == cmd.EventId, ct)) return Error.NotFound("Evento não encontrado.");

        var existing = await db.Stands.Where(s => s.EventId == cmd.EventId).ToDictionaryAsync(s => s.Id, ct);
        foreach (var item in cmd.Stands)
        {
            if (existing.TryGetValue(item.Id, out var stand))
            {
                stand.Update(item.Name, item.Type, stand.Description, stand.Cover, stand.Rating, item.Width, item.Height);
                stand.MoveTo(item.X, item.Y);
            }
            else if (await db.Stands.AnyAsync(s => s.Id == item.Id, ct))
            {
                return Error.Conflict($"O stand {item.Id} pertence a outro evento.");
            }
            else
            {
                db.Stands.Add(Stand.Create(item.Id, cmd.EventId, item.Name, item.Type, null, null, 4.5m,
                    item.X, item.Y, item.Width, item.Height));
            }
        }

        audit.Record("save_map", "event", cmd.EventId, new { count = cmd.Stands.Count });
        await db.SaveChangesAsync(ct);
        await realtime.CatalogChangedAsync("stands", ct);
        return await getMap.Handle(new GetEventMapQuery(cmd.EventId), ct);
    }
}
