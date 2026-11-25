using EventFlow.Api.Infrastructure;
using EventFlow.Application.Common;
using EventFlow.Application.Features.Events;
using EventFlow.Application.Features.Partners;
using EventFlow.Application.Features.Products;
using EventFlow.Application.Features.Stands;
using EventFlow.Domain;

namespace EventFlow.Api.Endpoints;

/// <summary>Eventos, mapa, stands, produtos e parceiros. Leitura pública; escrita conforme o papel.</summary>
internal static class CatalogEndpoints
{
    public sealed record EventBody(string Name, string Date, string Location, string? Description, string? Banner, EventStatus Status);
    public sealed record StandBody(string EventId, string Name, StandType Category, string? Description, string? Cover,
        decimal Rating, int X, int Y, int Width, int Height);
    public sealed record PositionBody(int X, int Y);
    public sealed record MapBody(IReadOnlyList<MapStandDto> Stands);
    public sealed record ProductBody(string StandId, string Name, string? Description, decimal Price, int PrepMinutes,
        string? Image, bool Available);
    public sealed record PartnerBody(string CompanyName, string Cnpj, string ContactName, string Email, string Phone,
        string Address, StandType Category, string? Description, string? Logo, string EventId, string? StandId, string? LoginPassword);
    public sealed record LinkStandBody(string? StandId);

    public static void MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var events = app.MapGroup("/api/events").WithTags("Eventos");
        events.MapGet("/", async (string? status, IDispatcher d, CancellationToken ct) =>
            EnumCodes.TryParse<EventStatus>(status, out var parsed)
                ? (await d.Send(new ListEventsQuery(parsed), ct)).ToHttp()
                : EnumCodes.Invalid("status"));
        events.MapGet("/{id}", async (string id, IDispatcher d, CancellationToken ct) =>
            (await d.Send(new GetEventQuery(id), ct)).ToHttp());
        events.MapPut("/{id}", async (string id, EventBody b, IDispatcher d, CancellationToken ct) =>
            (await d.Send(new UpsertEventCommand(id, b.Name, b.Date, b.Location, b.Description, b.Banner, b.Status), ct)).ToHttp())
            .RequireAuthorization().WithSummary("Cria ou atualiza um evento (admin)");
        events.MapDelete("/{id}", async (string id, IDispatcher d, CancellationToken ct) =>
            (await d.Send(new DeleteEventCommand(id), ct)).ToNoContent())
            .RequireAuthorization().WithSummary("Remove um evento (admin)");
        events.MapGet("/{id}/map", async (string id, IDispatcher d, CancellationToken ct) =>
            (await d.Send(new GetEventMapQuery(id), ct)).ToHttp()).WithSummary("Layout do mapa em JSON");
        events.MapPut("/{id}/map", async (string id, MapBody b, IDispatcher d, CancellationToken ct) =>
            (await d.Send(new SaveEventMapCommand(id, b.Stands), ct)).ToHttp())
            .RequireAuthorization().WithSummary("Salva o layout inteiro do mapa (admin)");

        var stands = app.MapGroup("/api/stands").WithTags("Stands");
        stands.MapGet("/", async (string? eventId, IDispatcher d, CancellationToken ct) =>
            (await d.Send(new ListStandsQuery(eventId), ct)).ToHttp());
        stands.MapPut("/{id}", async (string id, StandBody b, IDispatcher d, CancellationToken ct) =>
            (await d.Send(new UpsertStandCommand(id, b.EventId, b.Name, b.Category, b.Description, b.Cover, b.Rating,
                b.X, b.Y, b.Width, b.Height), ct)).ToHttp())
            .RequireAuthorization().WithSummary("Cria ou atualiza um elemento do mapa (admin)");
        stands.MapPatch("/{id}/position", async (string id, PositionBody b, IDispatcher d, CancellationToken ct) =>
            (await d.Send(new MoveStandCommand(id, b.X, b.Y), ct)).ToHttp())
            .RequireAuthorization().WithSummary("Move um elemento do mapa (admin)");
        stands.MapDelete("/{id}", async (string id, IDispatcher d, CancellationToken ct) =>
            (await d.Send(new DeleteStandCommand(id), ct)).ToNoContent())
            .RequireAuthorization();
        stands.MapGet("/{id}/products", async (string id, IDispatcher d, CancellationToken ct) =>
            (await d.Send(new ListProductsQuery(id), ct)).ToHttp()).WithSummary("Cardápio de um stand");

        var products = app.MapGroup("/api/products").WithTags("Produtos");
        products.MapGet("/", async (string? standId, IDispatcher d, CancellationToken ct) =>
            (await d.Send(new ListProductsQuery(standId), ct)).ToHttp());
        products.MapPut("/{id}", async (string id, ProductBody b, IDispatcher d, CancellationToken ct) =>
            (await d.Send(new UpsertProductCommand(id, b.StandId, b.Name, b.Description, b.Price, b.PrepMinutes, b.Image,
                b.Available), ct)).ToHttp())
            .RequireAuthorization().WithSummary("Cria ou atualiza um produto (admin ou parceiro do stand)");
        products.MapDelete("/{id}", async (string id, IDispatcher d, CancellationToken ct) =>
            (await d.Send(new DeleteProductCommand(id), ct)).ToNoContent())
            .RequireAuthorization();

        var partners = app.MapGroup("/api/partners").WithTags("Parceiros");
        partners.MapGet("/", async (string? eventId, IDispatcher d, CancellationToken ct) =>
            (await d.Send(new ListPartnersQuery(eventId), ct)).ToHttp())
            .WithSummary("Lista parceiros (dados cadastrais só para admin e para o próprio parceiro)");
        partners.MapPut("/{id}", async (string id, PartnerBody b, IDispatcher d, CancellationToken ct) =>
            (await d.Send(new UpsertPartnerCommand(id, b.CompanyName, b.Cnpj, b.ContactName, b.Email, b.Phone, b.Address,
                b.Category, b.Description, b.Logo, b.EventId, b.StandId, b.LoginPassword), ct)).ToHttp())
            .RequireAuthorization().WithSummary("Cadastra ou atualiza parceiro; no cadastro cria o login da empresa (admin)");
        partners.MapPut("/{id}/stand", async (string id, LinkStandBody b, IDispatcher d, CancellationToken ct) =>
            (await d.Send(new LinkPartnerStandCommand(id, b.StandId), ct)).ToHttp())
            .RequireAuthorization().WithSummary("Vincula o parceiro a um stand do mapa (admin)");
        partners.MapDelete("/{id}", async (string id, IDispatcher d, CancellationToken ct) =>
            (await d.Send(new DeletePartnerCommand(id), ct)).ToNoContent())
            .RequireAuthorization();
    }
}
