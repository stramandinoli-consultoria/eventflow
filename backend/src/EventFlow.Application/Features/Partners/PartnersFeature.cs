using EventFlow.Application.Abstractions;
using EventFlow.Application.Common;
using EventFlow.Application.Dtos;
using EventFlow.Domain;
using EventFlow.Domain.Events;
using EventFlow.Domain.Users;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Application.Features.Partners;

public sealed record ListPartnersQuery(string? EventId = null) : IQuery<IReadOnlyList<PartnerDto>>;

/// <summary>
/// Cadastra ou atualiza a empresa parceira. Num cadastro novo, cria também o
/// login da empresa com <see cref="LoginPassword"/>.
/// </summary>
public sealed record UpsertPartnerCommand(string Id, string CompanyName, string Cnpj, string ContactName, string Email,
    string Phone, string Address, StandType Category, string? Description, string? Logo, string EventId, string? StandId,
    string? LoginPassword) : ICommand<PartnerDto>;

public sealed record LinkPartnerStandCommand(string PartnerId, string? StandId) : ICommand<PartnerDto>;
public sealed record DeletePartnerCommand(string Id) : ICommand<Unit>;

public sealed class UpsertPartnerValidator : AbstractValidator<UpsertPartnerCommand>
{
    public UpsertPartnerValidator()
    {
        RuleFor(x => x.Id).ValidId();
        RuleFor(x => x.CompanyName).NotEmpty().WithMessage("Preencha: companyName").MaximumLength(200);
        RuleFor(x => x.Cnpj).NotEmpty().WithMessage("Preencha: cnpj").MaximumLength(20);
        RuleFor(x => x.ContactName).NotEmpty().WithMessage("Preencha: contactName").MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().WithMessage("Preencha: email").EmailAddress().WithMessage("E-mail inválido");
        RuleFor(x => x.Phone).NotEmpty().WithMessage("Preencha: phone").MaximumLength(40);
        RuleFor(x => x.Address).NotEmpty().WithMessage("Preencha: address").MaximumLength(300);
        RuleFor(x => x.EventId).NotEmpty().WithMessage("Preencha: eventId");
        RuleFor(x => x.Category).IsInEnum().Must(c => c.IsVendor()).WithMessage("Preencha: category");
        RuleFor(x => x.Logo).HttpUrl();
        RuleFor(x => x.LoginPassword).MinimumLength(6).When(x => !string.IsNullOrEmpty(x.LoginPassword))
            .WithMessage("A senha deve ter pelo menos 6 caracteres");
    }
}

internal sealed class ListPartnersHandler(IAppDbContext db, AccessScope access) : IRequestHandler<ListPartnersQuery, IReadOnlyList<PartnerDto>>
{
    public async Task<Result<IReadOnlyList<PartnerDto>>> Handle(ListPartnersQuery query, CancellationToken ct)
    {
        var partners = db.Partners.AsNoTracking();
        if (query.EventId is not null) partners = partners.Where(p => p.EventId == query.EventId);
        var list = await partners.OrderBy(p => p.CompanyName).ToListAsync(ct);
        var ownPartnerId = access.IsPartner ? access.User.PartnerId : null;
        return list.Select(p => p.ToDto(access.IsAdmin || p.Id == ownPartnerId)).ToList();
    }
}

internal sealed class UpsertPartnerHandler(IAppDbContext db, AccessScope access, AuditTrail audit, IPasswordHasher hasher,
    IRealtimeNotifier realtime, TimeProvider clock) : IRequestHandler<UpsertPartnerCommand, PartnerDto>
{
    private const string DefaultLogo = "https://images.unsplash.com/photo-1546069901-ba9599a7e63c?w=800&q=80";
    private const string DefaultPassword = "demo123";

    public async Task<Result<PartnerDto>> Handle(UpsertPartnerCommand cmd, CancellationToken ct)
    {
        if (!access.IsAdmin) return Error.Forbidden();
        if (!await db.Events.AnyAsync(e => e.Id == cmd.EventId, ct)) return Error.NotFound("Evento não encontrado.");
        if (!string.IsNullOrEmpty(cmd.StandId) && !await db.Stands.AnyAsync(s => s.Id == cmd.StandId && s.EventId == cmd.EventId, ct))
            return Error.Rule("O stand informado não pertence a este evento.");

        var logo = string.IsNullOrWhiteSpace(cmd.Logo) ? DefaultLogo : cmd.Logo;
        var partner = await db.Partners.FirstOrDefaultAsync(p => p.Id == cmd.Id, ct);
        var isNew = partner is null;
        if (partner is null)
        {
            var email = User.NormalizeEmail(cmd.Email);
            if (await db.Users.AnyAsync(u => u.Email == email, ct))
                return Error.Conflict("Já existe uma conta com este e-mail.");

            partner = Partner.Create(cmd.Id, cmd.CompanyName, cmd.Cnpj, cmd.ContactName, cmd.Email, cmd.Phone, cmd.Address,
                cmd.Category, cmd.Description, logo, cmd.EventId, cmd.StandId);
            db.Partners.Add(partner);
            db.Users.Add(User.Create("u-" + Guid.NewGuid().ToString("N")[..12], cmd.ContactName, email, cmd.Phone,
                hasher.Hash(string.IsNullOrEmpty(cmd.LoginPassword) ? DefaultPassword : cmd.LoginPassword),
                UserRole.Partner, clock.GetUtcNow(), partner.Id));
        }
        else
        {
            partner.Update(cmd.CompanyName, cmd.Cnpj, cmd.ContactName, cmd.Email, cmd.Phone, cmd.Address, cmd.Category,
                cmd.Description, logo, cmd.EventId, cmd.StandId);
        }

        // Um stand tem no máximo uma empresa vinculada.
        if (partner.StandId is not null)
            foreach (var other in await db.Partners.Where(p => p.StandId == partner.StandId && p.Id != partner.Id).ToListAsync(ct))
                other.LinkStand(null);

        audit.Record(isNew ? "create" : "update", "partner", partner.Id, cmd with { LoginPassword = null });
        await db.SaveChangesAsync(ct);
        await realtime.CatalogChangedAsync("partners", ct);
        return partner.ToDto(includePrivateData: true);
    }
}

internal sealed class LinkPartnerStandHandler(IAppDbContext db, AccessScope access, AuditTrail audit, IRealtimeNotifier realtime)
    : IRequestHandler<LinkPartnerStandCommand, PartnerDto>
{
    public async Task<Result<PartnerDto>> Handle(LinkPartnerStandCommand cmd, CancellationToken ct)
    {
        if (!access.IsAdmin) return Error.Forbidden();
        var partner = await db.Partners.FirstOrDefaultAsync(p => p.Id == cmd.PartnerId, ct);
        if (partner is null) return Error.NotFound("Parceiro não encontrado.");
        if (!string.IsNullOrEmpty(cmd.StandId))
        {
            if (!await db.Stands.AnyAsync(s => s.Id == cmd.StandId && s.EventId == partner.EventId, ct))
                return Error.Rule("O stand informado não pertence ao evento do parceiro.");
            foreach (var other in await db.Partners.Where(p => p.StandId == cmd.StandId && p.Id != partner.Id).ToListAsync(ct))
                other.LinkStand(null);
        }

        partner.LinkStand(cmd.StandId);
        audit.Record("link_stand", "partner", partner.Id, new { cmd.StandId });
        await db.SaveChangesAsync(ct);
        await realtime.CatalogChangedAsync("partners", ct);
        return partner.ToDto(includePrivateData: true);
    }
}

internal sealed class DeletePartnerHandler(IAppDbContext db, AccessScope access, AuditTrail audit, IRealtimeNotifier realtime)
    : IRequestHandler<DeletePartnerCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeletePartnerCommand cmd, CancellationToken ct)
    {
        if (!access.IsAdmin) return Error.Forbidden();
        var partner = await db.Partners.FirstOrDefaultAsync(p => p.Id == cmd.Id, ct);
        if (partner is null) return Error.NotFound("Parceiro não encontrado.");

        // O login da empresa deixa de existir junto com ela.
        db.Users.RemoveRange(await db.Users.Where(u => u.PartnerId == partner.Id).ToListAsync(ct));
        db.Partners.Remove(partner);
        audit.Record("delete", "partner", partner.Id);
        await db.SaveChangesAsync(ct);
        await realtime.CatalogChangedAsync("partners", ct);
        return Unit.Value;
    }
}
