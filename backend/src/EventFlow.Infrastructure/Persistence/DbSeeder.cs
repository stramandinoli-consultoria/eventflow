using EventFlow.Application.Abstractions;
using EventFlow.Domain;
using EventFlow.Domain.Events;
using EventFlow.Domain.Orders;
using EventFlow.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EventFlow.Infrastructure.Persistence;

/// <summary>
/// Dados de exemplo (os mesmos que o app do Lovable trazia no localStorage).
/// Só roda com o banco vazio.
/// </summary>
public sealed class DbSeeder(AppDbContext db, IPasswordHasher hasher, ICrypto crypto, TimeProvider clock, ILogger<DbSeeder> logger)
{
    private static DateTime At(string text) => DateTime.ParseExact(text, "yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(ct)) return;
        var now = clock.GetUtcNow();

        var events = new[]
        {
            Event.Create("ev1", "Festival Sabores da Cidade", At("2026-06-14 17:00"), "Parque Ibirapuera, São Paulo",
                "Três dias de comida de rua, cervejas artesanais e shows ao vivo.",
                "https://images.unsplash.com/photo-1555939594-58d7cb561ad1?w=1600&q=80", EventStatus.Live),
            Event.Create("ev2", "Brew & BBQ Fest", At("2026-07-22 16:00"), "Centro de Eventos, Curitiba",
                "O maior encontro de cervejarias independentes e churrasco autoral.",
                "https://images.unsplash.com/photo-1504674900247-0877df9cc836?w=1600&q=80", EventStatus.Upcoming),
            Event.Create("ev3", "GastroNight Rooftop", At("2026-08-03 19:00"), "Edifício Copan, São Paulo",
                "Drinks autorais e cozinha internacional sob as estrelas.",
                "https://images.unsplash.com/photo-1414235077428-338989a2e8c0?w=1600&q=80", EventStatus.Upcoming),
        };

        var stands = new[]
        {
            Stand.Create("s1", "ev1", "Taco Loco", StandType.Food, "Tacos mexicanos autênticos com salsas artesanais.",
                "https://images.unsplash.com/photo-1565299585323-38d6b0865b47?w=800&q=80", 4.8m, 80, 120, 120, 90),
            Stand.Create("s2", "ev1", "Beer Station", StandType.Beer, "12 chopps artesanais sempre gelados.",
                "https://images.unsplash.com/photo-1535958636474-b021ee887b13?w=800&q=80", 4.7m, 260, 80, 120, 90),
            Stand.Create("s3", "ev1", "Burger Republic", StandType.Food, "Burgers artesanais 180g, blend exclusivo.",
                "https://images.unsplash.com/photo-1568901346375-23c9450c58cd?w=800&q=80", 4.9m, 440, 160, 120, 90),
            Stand.Create("s4", "ev1", "Sweet Cloud", StandType.Dessert, "Sorvetes, donuts e brownies recém-feitos.",
                "https://images.unsplash.com/photo-1488477181946-6428a0291777?w=800&q=80", 4.6m, 620, 110, 120, 90),
            Stand.Create("s5", "ev1", "Sakura Drinks", StandType.Drink, "Drinks autorais com gin, sake e frutas.",
                "https://images.unsplash.com/photo-1551024709-8f23befc6f87?w=800&q=80", 4.5m, 180, 290, 120, 90),
            Stand.Create("s6", "ev1", "Palco Principal", StandType.Stage, "Shows ao vivo durante todo o evento.", "", 0, 380, 320, 220, 100),
            Stand.Create("s7", "ev1", "Banheiros", StandType.Restroom, "", "", 0, 680, 290, 80, 80),
            Stand.Create("s8", "ev1", "Caixa Central", StandType.Cashier, "Pague seu pedido aqui.", "", 0, 40, 410, 120, 70),
        };

        var products = new[]
        {
            Product.Create("p1", "s1", "Taco al Pastor (3un)", "Carne marinada, abacaxi, coentro e cebola roxa.", 32, 8,
                "https://images.unsplash.com/photo-1565299585323-38d6b0865b47?w=600&q=80", true),
            Product.Create("p2", "s1", "Nachos Supreme", "Tortilhas, queijo derretido, guacamole e jalapeños.", 28, 6,
                "https://images.unsplash.com/photo-1582169296194-e4d644c48063?w=600&q=80", true),
            Product.Create("p3", "s2", "Chopp IPA 500ml", "Cervejaria local, lúpulo cítrico.", 22, 2,
                "https://images.unsplash.com/photo-1608270586620-248524c67de9?w=600&q=80", true),
            Product.Create("p4", "s2", "Chopp Pilsen 500ml", "Clássica e refrescante.", 18, 2,
                "https://images.unsplash.com/photo-1535958636474-b021ee887b13?w=600&q=80", true),
            Product.Create("p5", "s3", "Smash Burger Duplo", "180g, queijo cheddar, picles e molho da casa.", 38, 12,
                "https://images.unsplash.com/photo-1568901346375-23c9450c58cd?w=600&q=80", true),
            Product.Create("p6", "s3", "Fritas Trufadas", "Batatas rústicas, parmesão e óleo trufado.", 24, 7,
                "https://images.unsplash.com/photo-1573080496219-bb080dd4f877?w=600&q=80", true),
            Product.Create("p7", "s4", "Brownie com Sorvete", "Brownie quente com sorvete de baunilha.", 22, 4,
                "https://images.unsplash.com/photo-1606313564200-e75d5e30476c?w=600&q=80", true),
            Product.Create("p8", "s5", "Gin Tropical", "Gin, maracujá, hortelã e gengibre.", 32, 5,
                "https://images.unsplash.com/photo-1551024709-8f23befc6f87?w=600&q=80", true),
        };

        var partner = Partner.Create("pt1", "Burger Republic LTDA", "12.345.678/0001-90", "Carlos Souza", "burger@demo.com",
            "+5511988880000", "Rua das Hambúrguer, 123 — SP", StandType.Food, "Burgers artesanais.",
            "https://images.unsplash.com/photo-1568901346375-23c9450c58cd?w=400&q=80", "ev1", "s3");

        var users = new[]
        {
            User.Create("u-admin", "Admin Master", "admin@eventflow.app", null, hasher.Hash("admin123"), UserRole.Admin, now),
            User.Create("u-cliente", "Marina Costa", "cliente@demo.com", "+5511999990001", hasher.Hash("demo123"), UserRole.Customer, now),
            User.Create("u-parceiro", "Burger Republic", "burger@demo.com", null, hasher.Hash("demo123"), UserRole.Partner, now, "pt1"),
        };

        // Dois pedidos de exemplo, com a timeline nos mesmos momentos relativos do app original.
        var order1 = Order.Place("ord-1042", stands[2], products, [("p5", 1), ("p6", 1)], "u-cliente", "Marina Costa",
            "+5511999990001", crypto.Hash(crypto.RandomToken()), now.AddMinutes(-7));
        order1.Advance(now.AddMinutes(-4));

        var order2 = Order.Place("ord-1043", stands[1], products, [("p3", 2)], null, "Rafael Lima",
            "+5511999990002", crypto.Hash(crypto.RandomToken()), now.AddMinutes(-12));
        order2.Advance(now.AddMinutes(-10));
        order2.Advance(now.AddMinutes(-2));

        db.Events.AddRange(events);
        db.Stands.AddRange(stands);
        db.Products.AddRange(products);
        db.Partners.Add(partner);
        db.Users.AddRange(users);
        db.Orders.AddRange(order1, order2);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Banco preenchido com os dados de exemplo (3 eventos, 8 stands, 8 produtos, 3 usuários, 2 pedidos).");
    }
}
