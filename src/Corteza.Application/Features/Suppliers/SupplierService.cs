using Corteza.Application.Common.Exceptions;
using Corteza.Application.Common.Interfaces;
using Corteza.Application.Common.Models;
using Corteza.Application.Common.Validation;
using Corteza.Domain.Entities;
using Corteza.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Corteza.Application.Features.Suppliers;

public sealed class SupplierService(IAppDbContext db)
{
    public async Task<PagedResult<SupplierListItem>> ListAsync(SupplierQuery q, CancellationToken ct)
    {
        var query = db.Suppliers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q.TaxId))
        {
            var taxId = Normalize.TaxId(q.TaxId);
            query = query.Where(s => s.TaxId.Contains(taxId));
        }

        if (!string.IsNullOrWhiteSpace(q.Name))
        {
            var name = Normalize.Text(q.Name);
            query = query.Where(s => s.Name.Contains(name));
        }

        if (q.CategoryId is { } categoryId)
            query = query.Where(s => s.CategoryLinks.Any(l => l.SupplyCategoryId == categoryId));

        if (q.Active is not null)
            query = query.Where(s => s.IsActive == q.Active);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(s => s.Name)
            .Skip(q.Skip).Take(q.PageSize)
            .Select(s => new SupplierListItem(
                s.Id, s.Name, s.TaxId, s.ContactName, s.Phone, s.Email, s.AvailableBudget, s.IsActive,
                s.CategoryLinks
                    .OrderBy(l => l.SupplyCategory.Name)
                    .Select(l => new CategoryRef(l.SupplyCategoryId, l.SupplyCategory.Name))
                    .ToList()))
            .ToListAsync(ct);

        return new PagedResult<SupplierListItem>(items, total, q.Page, q.PageSize);
    }

    public async Task<SupplierDetail> GetAsync(int id, CancellationToken ct)
    {
        return await db.Suppliers.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new SupplierDetail(
                s.Id, s.Name, s.TaxId, s.ContactName, s.Phone, s.Email, s.AvailableBudget, s.IsActive, s.CreatedAt, s.UpdatedAt,
                s.CategoryLinks
                    .OrderBy(l => l.SupplyCategory.Name)
                    .Select(l => new CategoryRef(l.SupplyCategoryId, l.SupplyCategory.Name))
                    .ToList(),
                s.PurchaseOrders.Count(o => o.Status == PurchaseOrderStatus.Pending || o.Status == PurchaseOrderStatus.Approved)))
            .FirstOrDefaultAsync(ct)
            ?? throw NotFoundException.For("el proveedor", id);
    }

    public async Task<SupplierDetail> CreateAsync(SupplierUpsertRequest request, CancellationToken ct)
    {
        var taxId = Normalize.TaxId(request.TaxId);
        await EnsureUniqueTaxIdAsync(taxId, excludeId: null, ct);
        var categoryIds = await ValidateCategoriesAsync(request.CategoryIds, ct);

        var supplier = Supplier.Create(
            Normalize.Text(request.Name), taxId, Normalize.Text(request.ContactName),
            Normalize.Phone(request.Phone), Normalize.Email(request.Email)!, request.AvailableBudget);
        foreach (var categoryId in categoryIds)
            supplier.CategoryLinks.Add(new SupplierSupplyCategory { SupplyCategoryId = categoryId });

        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(ct);
        return await GetAsync(supplier.Id, ct);
    }

    public async Task<SupplierDetail> UpdateAsync(int id, SupplierUpsertRequest request, CancellationToken ct)
    {
        var supplier = await db.Suppliers
            .Include(s => s.CategoryLinks)
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw NotFoundException.For("el proveedor", id);

        var taxId = Normalize.TaxId(request.TaxId);
        await EnsureUniqueTaxIdAsync(taxId, excludeId: id, ct);
        var categoryIds = await ValidateCategoriesAsync(request.CategoryIds, ct);

        supplier.UpdateDetails(
            Normalize.Text(request.Name), taxId, Normalize.Text(request.ContactName),
            Normalize.Phone(request.Phone), Normalize.Email(request.Email)!, request.AvailableBudget);
        supplier.UpdatedAt = DateTime.UtcNow;

        // Sincroniza las categorías: quita las desmarcadas y agrega las nuevas.
        foreach (var link in supplier.CategoryLinks.Where(l => !categoryIds.Contains(l.SupplyCategoryId)).ToList())
            db.SupplierSupplyCategories.Remove(link);
        foreach (var categoryId in categoryIds.Where(cid => supplier.CategoryLinks.All(l => l.SupplyCategoryId != cid)))
            supplier.CategoryLinks.Add(new SupplierSupplyCategory { SupplierId = id, SupplyCategoryId = categoryId });

        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>
    /// Regla del Sprint 2 (proveedores.feature): no se desactiva un proveedor con órdenes de compra activas.
    /// El proveedor desactivado no aparece al crear órdenes, pero conserva su historial (RF-PRO004).
    /// </summary>
    public async Task DeactivateAsync(int id, CancellationToken ct)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw NotFoundException.For("el proveedor", id);
        if (!supplier.IsActive) return;

        var hasActiveOrders = await db.PurchaseOrders
            .AnyAsync(o => o.SupplierId == id
                && (o.Status == PurchaseOrderStatus.Pending || o.Status == PurchaseOrderStatus.Approved), ct);

        supplier.Deactivate(hasActiveOrders); // la regla vive en el dominio
        supplier.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task ActivateAsync(int id, CancellationToken ct)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw NotFoundException.For("el proveedor", id);
        if (supplier.IsActive) return;

        supplier.Activate();
        supplier.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureUniqueTaxIdAsync(string taxId, int? excludeId, CancellationToken ct)
    {
        if (await db.Suppliers.AnyAsync(s => s.TaxId == taxId && s.Id != excludeId, ct))
            throw new ConflictException($"Ya existe un proveedor con el NIT {taxId}.");
    }

    private async Task<List<int>> ValidateCategoriesAsync(IEnumerable<int> requested, CancellationToken ct)
    {
        var ids = requested.Distinct().ToList();
        var found = await db.SupplyCategories.CountAsync(c => ids.Contains(c.Id), ct);
        if (found != ids.Count)
            throw new BusinessRuleException("Una de las categorías marcadas no existe.");
        return ids;
    }

}
