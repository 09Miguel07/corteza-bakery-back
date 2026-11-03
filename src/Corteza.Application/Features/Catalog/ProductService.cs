using Corteza.Application.Common.Interfaces;
using Corteza.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Corteza.Application.Features.Catalog;

public sealed record ProductDto(int Id, string Name, string? Description, ProductCategory Category, decimal Price, bool IsActive);

/// <summary>Productos de venta. No tienen pantalla en el Sprint 1: se cargan por seed y se leen para tomar pedidos.</summary>
public sealed class ProductService(IAppDbContext db)
{
    public async Task<IReadOnlyList<ProductDto>> ListAsync(bool? active, CancellationToken ct)
    {
        var query = db.Products.AsNoTracking();
        if (active is not null) query = query.Where(p => p.IsActive == active);

        return await query
            .OrderBy(p => p.Category).ThenBy(p => p.Name)
            .Select(p => new ProductDto(p.Id, p.Name, p.Description, p.Category, p.Price, p.IsActive))
            .ToListAsync(ct);
    }
}
