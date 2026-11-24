using EventFlow.Application.Abstractions;
using EventFlow.Application.Common;
using EventFlow.Application.Dtos;
using EventFlow.Domain;
using EventFlow.Domain.Events;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Application.Features.Events;

public sealed record ListEventsQuery(EventStatus? Status = null) : IQuery<IReadOnlyList<EventDto>>;
public sealed record GetEventQuery(string Id) : IQuery<EventDto>;
public sealed record UpsertEventCommand(string Id, string Name, string Date, string Location, string? Description,
    string? Banner, EventStatus Status) : ICommand<EventDto>;
public sealed record DeleteEventCommand(string Id) : ICommand<Unit>;

public sealed class UpsertEventValidator : AbstractValidator<UpsertEventCommand>
{
    public UpsertEventValidator()
    {
        RuleFor(x => x.Id).ValidId();
        RuleFor(x => x.Name).NotEmpty().WithMessage("Preencha nome, data e local").MaximumLength(200);
        RuleFor(x => x.Location).NotEmpty().WithMessage("Preencha nome, data e local").MaximumLength(200);
        RuleFor(x => x.Date).NotEmpty().WithMessage("Preencha nome, data e local")
            .Must(d => Mapping.TryParseEventDate(d, out _)).WithMessage("Data deve estar no formato 2026-09-10 18:00");
        RuleFor(x => x.Banner).HttpUrl();
        RuleFor(x => x.Status).IsInEnum();
    }
}

internal sealed class ListEventsHandler(IAppDbContext db) : IRequestHandler<ListEventsQuery, IReadOnlyList<EventDto>>
{
    public async Task<Result<IReadOnlyList<EventDto>>> Handle(ListEventsQuery query, CancellationToken ct)
    {
        var events = db.Events.AsNoTracking();
        if (query.Status is { } status) events = events.Where(e => e.Status == status);
        var list = await events.OrderBy(e => e.StartsAt).ToListAsync(ct);
        return list.Select(e => e.ToDto()).ToList();
    }
}

internal sealed class GetEventHandler(IAppDbContext db) : IRequestHandler<GetEventQuery, EventDto>
{
    public async Task<Result<EventDto>> Handle(GetEventQuery query, CancellationToken ct)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == query.Id, ct);
        return ev is null ? Error.NotFound("Evento não encontrado.") : ev.ToDto();
    }
}

internal sealed class UpsertEventHandler(IAppDbContext db, AccessScope access, AuditTrail audit, IRealtimeNotifier realtime)
    : IRequestHandler<UpsertEventCommand, EventDto>
{
    private const string DefaultBanner = "https://images.unsplash.com/photo-1492684223066-81342ee5ff30?w=1600&q=80";

    public async Task<Result<EventDto>> Handle(UpsertEventCommand cmd, CancellationToken ct)
    {
        if (!access.IsAdmin) return Error.Forbidden();
        Mapping.TryParseEventDate(cmd.Date, out var startsAt);
        var banner = string.IsNullOrWhiteSpace(cmd.Banner) ? DefaultBanner : cmd.Banner;

        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == cmd.Id, ct);
        if (ev is null)
        {
            ev = Event.Create(cmd.Id, cmd.Name, startsAt, cmd.Location, cmd.Description, banner, cmd.Status);
            db.Events.Add(ev);
            audit.Record("create", "event", ev.Id, cmd);
        }
        else
        {
            ev.Update(cmd.Name, startsAt, cmd.Location, cmd.Description, banner, cmd.Status);
            audit.Record("update", "event", ev.Id, cmd);
        }

        await db.SaveChangesAsync(ct);
        await realtime.CatalogChangedAsync("events", ct);
        return ev.ToDto();
    }
}

internal sealed class DeleteEventHandler(IAppDbContext db, AccessScope access, AuditTrail audit, IRealtimeNotifier realtime)
    : IRequestHandler<DeleteEventCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteEventCommand cmd, CancellationToken ct)
    {
        if (!access.IsAdmin) return Error.Forbidden();
        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == cmd.Id, ct);
        if (ev is null) return Error.NotFound("Evento não encontrado.");
        if (await db.Partners.AnyAsync(p => p.EventId == cmd.Id, ct))
            return Error.Rule("Remova as empresas parceiras deste evento antes de excluí-lo.");

        // Stands e produtos do evento saem junto (cascade). Pedidos ficam como histórico.
        db.Events.Remove(ev);
        audit.Record("delete", "event", ev.Id);
        await db.SaveChangesAsync(ct);
        await realtime.CatalogChangedAsync("events", ct);
        return Unit.Value;
    }
}
