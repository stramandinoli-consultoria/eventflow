using EventFlow.Domain.Common;

namespace EventFlow.Domain.Events;

/// <summary>Empresa parceira (expositora) vinculada a um evento e, opcionalmente, a um stand do mapa.</summary>
public sealed class Partner
{
    public string Id { get; private set; } = default!;
    public string CompanyName { get; private set; } = default!;
    public string Cnpj { get; private set; } = default!;
    public string ContactName { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public string Phone { get; private set; } = default!;
    public string Address { get; private set; } = default!;
    public StandType Category { get; private set; }
    public string Description { get; private set; } = default!;
    public string Logo { get; private set; } = default!;
    public string EventId { get; private set; } = default!;
    public string? StandId { get; private set; }

    private Partner() { }

    public static Partner Create(string id, string companyName, string cnpj, string contactName, string email,
        string phone, string address, StandType category, string? description, string logo, string eventId, string? standId)
    {
        var partner = new Partner { Id = Guard.Required(id, "Id", 64) };
        partner.Update(companyName, cnpj, contactName, email, phone, address, category, description, logo, eventId, standId);
        return partner;
    }

    public void Update(string companyName, string cnpj, string contactName, string email, string phone, string address,
        StandType category, string? description, string logo, string eventId, string? standId)
    {
        if (!category.IsVendor()) throw new DomainException("A categoria do parceiro deve ser de venda (comida, drinks, chopp ou sobremesas).");
        CompanyName = Guard.Required(companyName, "Razão social");
        Cnpj = Guard.Required(cnpj, "CNPJ", 20);
        ContactName = Guard.Required(contactName, "Responsável");
        Email = Guard.Required(email, "E-mail", 320).ToLowerInvariant();
        Phone = Guard.Required(phone, "WhatsApp", 40);
        Address = Guard.Required(address, "Endereço", 300);
        Category = category;
        Description = Guard.Optional(description, "Descrição");
        Logo = Guard.Required(logo, "Logo", 2000);
        EventId = Guard.Required(eventId, "Evento", 64);
        LinkStand(standId);
    }

    public void LinkStand(string? standId) => StandId = string.IsNullOrWhiteSpace(standId) ? null : standId;
}
