using EventFlow.Domain.Common;

namespace EventFlow.Domain.Events;

public sealed class Product
{
    public string Id { get; private set; } = default!;
    public string StandId { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public decimal Price { get; private set; }
    public int PrepMinutes { get; private set; }
    public string Image { get; private set; } = default!;
    public bool Available { get; private set; }

    private Product() { }

    public static Product Create(string id, string standId, string name, string? description, decimal price,
        int prepMinutes, string image, bool available)
    {
        var product = new Product { Id = Guard.Required(id, "Id", 64), StandId = Guard.Required(standId, "Stand", 64) };
        product.Update(name, description, price, prepMinutes, image, available);
        return product;
    }

    public void Update(string name, string? description, decimal price, int prepMinutes, string image, bool available)
    {
        if (price <= 0) throw new DomainException("O preço deve ser maior que zero.");
        if (prepMinutes is < 0 or > 600) throw new DomainException("O tempo de preparo deve estar entre 0 e 600 minutos.");
        Name = Guard.Required(name, "Nome", 120);
        Description = Guard.Optional(description, "Descrição");
        Price = decimal.Round(price, 2);
        PrepMinutes = prepMinutes;
        Image = Guard.Required(image, "Imagem", 2000);
        Available = available;
    }
}
