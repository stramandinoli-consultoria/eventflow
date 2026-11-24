using EventFlow.Application.Abstractions;
using EventFlow.Application.Common;
using EventFlow.Application.Dtos;
using EventFlow.Domain.Events;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Application.Features.Products;

public sealed record ListProductsQuery(string? StandId = null) : IQuery<IReadOnlyList<ProductDto>>;
public sealed record UpsertProductCommand(string Id, string StandId, string Name, string? Description, decimal Price,
    int PrepMinutes, string? Image, bool Available) : ICommand<ProductDto>;
public sealed record DeleteProductCommand(string Id) : ICommand<Unit>;

public sealed class UpsertProductValidator : AbstractValidator<UpsertProductCommand>
{
    public UpsertProductValidator()
    {
        RuleFor(x => x.Id).ValidId();
        RuleFor(x => x.StandId).ValidId();
        RuleFor(x => x.Name).NotEmpty().WithMessage("Nome e preço obrigatórios").MaximumLength(120);
        RuleFor(x => x.Price).GreaterThan(0).WithMessage("Nome e preço obrigatórios").LessThan(100000);
        RuleFor(x => x.PrepMinutes).InclusiveBetween(0, 600);
        RuleFor(x => x.Image).HttpUrl();
    }
}

internal sealed class ListProductsHandler(IAppDbContext db) : IRequestHandler<ListProductsQuery, IReadOnlyList<ProductDto>>
{
    public async Task<Result<IReadOnlyList<ProductDto>>> Handle(ListProductsQuery query, CancellationToken ct)
    {
        var products = db.Products.AsNoTracking();
        if (query.StandId is not null) products = products.Where(p => p.StandId == query.StandId);
        var list = await products.OrderBy(p => p.StandId).ThenBy(p => p.Id).ToListAsync(ct);
        return list.Select(p => p.ToDto()).ToList();
    }
}

internal sealed class UpsertProductHandler(IAppDbContext db, AccessScope access, AuditTrail audit, IRealtimeNotifier realtime)
    : IRequestHandler<UpsertProductCommand, ProductDto>
{
    private const string DefaultImage = "https://images.unsplash.com/photo-1546069901-ba9599a7e63c?w=600&q=80";

    public async Task<Result<ProductDto>> Handle(UpsertProductCommand cmd, CancellationToken ct)
    {
        if (!await access.CanManageStandAsync(cmd.StandId, ct)) return Error.Forbidden();
        if (!await db.Stands.AnyAsync(s => s.Id == cmd.StandId, ct)) return Error.NotFound("Stand não encontrado.");
        var image = string.IsNullOrWhiteSpace(cmd.Image) ? DefaultImage : cmd.Image;

        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == cmd.Id, ct);
        if (product is null)
        {
            product = Product.Create(cmd.Id, cmd.StandId, cmd.Name, cmd.Description, cmd.Price, cmd.PrepMinutes, image, cmd.Available);
            db.Products.Add(product);
            audit.Record("create", "product", product.Id, cmd);
        }
        else
        {
            if (product.StandId != cmd.StandId) return Error.Forbidden("Este produto pertence a outro stand.");
            product.Update(cmd.Name, cmd.Description, cmd.Price, cmd.PrepMinutes, image, cmd.Available);
            audit.Record("update", "product", product.Id, cmd);
        }

        await db.SaveChangesAsync(ct);
        await realtime.CatalogChangedAsync("products", ct);
        return product.ToDto();
    }
}

internal sealed class DeleteProductHandler(IAppDbContext db, AccessScope access, AuditTrail audit, IRealtimeNotifier realtime)
    : IRequestHandler<DeleteProductCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteProductCommand cmd, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == cmd.Id, ct);
        if (product is null) return Error.NotFound("Produto não encontrado.");
        if (!await access.CanManageStandAsync(product.StandId, ct)) return Error.Forbidden();

        db.Products.Remove(product);
        audit.Record("delete", "product", product.Id);
        await db.SaveChangesAsync(ct);
        await realtime.CatalogChangedAsync("products", ct);
        return Unit.Value;
    }
}
