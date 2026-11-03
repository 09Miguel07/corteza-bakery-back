using Corteza.Application.Common.Interfaces;
using Corteza.Application.Features.Suppliers;
using Microsoft.EntityFrameworkCore;

namespace Corteza.Application.Features.Inventory;

public sealed class SupplyCategoryService(IAppDbContext db)
{
    public async Task<IReadOnlyList<CategoryRef>> ListAsync(CancellationToken ct) =>
        await db.SupplyCategories.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryRef(c.Id, c.Name))
            .ToListAsync(ct);
}
