using Corteza.Application.Common.Exceptions;
using Corteza.Application.Features.Purchasing;
using Corteza.Application.Features.Suppliers;
using Corteza.Domain.Entities;
using Corteza.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Corteza.Application.Tests;

public class SupplierServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly SupplierService _service;

    public SupplierServiceTests() => _service = new SupplierService(_db.Context);

    public void Dispose() => _db.Dispose();

    private async Task<SupplyCategory> CategoryAsync()
    {
        var category = _db.Context.SupplyCategories.Add(new SupplyCategory { Name = "Harinas" }).Entity;
        await _db.Context.SaveChangesAsync();
        _db.Saves.Reset();
        return category;
    }

    private static SupplierUpsertRequest Request(int categoryId, string taxId = "900.123.456-7") => new()
    {
        Name = "ACME S.A.S.",
        TaxId = taxId,
        ContactName = "Ana Ruiz",
        Phone = "300 111 2233",
        Email = "contacto@acme.com",
        AvailableBudget = 3_000_000,
        CategoryIds = [categoryId],
    };

    [Fact(DisplayName = "Registrar un proveedor válido lo deja ACTIVO y normaliza el NIT")]
    public async Task Create_saves_active_supplier()
    {
        var category = await CategoryAsync();

        var supplier = await _service.CreateAsync(Request(category.Id), default);

        Assert.True(supplier.IsActive);
        Assert.Equal("900123456-7", supplier.TaxId);
        Assert.Equal(3_000_000, supplier.AvailableBudget);
    }

    [Fact(DisplayName = "Rechazar proveedor con NIT duplicado sin guardar")]
    public async Task Create_with_duplicated_nit_does_not_save()
    {
        var category = await CategoryAsync();
        await _service.CreateAsync(Request(category.Id), default);
        _db.Saves.Reset();

        var error = await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(Request(category.Id), default));

        Assert.Contains("900123456-7", error.Message);
        Assert.Equal(0, _db.Saves.Count);
    }

    [Fact(DisplayName = "No se desactiva un proveedor con órdenes de compra activas")]
    public async Task Deactivate_with_active_orders_is_rejected()
    {
        var supplier = await _db.AddSupplierAsync();
        var flour = await _db.AddSupplyAsync();
        await new PurchaseOrderService(_db.Context, _db.CurrentUser.Object, _db.Time).CreateAsync(new PurchaseOrderCreateRequest
        {
            SupplierId = supplier.Id,
            ExpectedDeliveryDate = _db.Today,
            Items = [new PurchaseOrderLineRequest { SupplyId = flour.Id, Quantity = 1, UnitPrice = 1_000 }],
        }, default);
        _db.Saves.Reset();

        await Assert.ThrowsAsync<SupplierHasActiveOrdersException>(() => _service.DeactivateAsync(supplier.Id, default));

        Assert.Equal(0, _db.Saves.Count);
        await using var fresh = _db.Fresh();
        Assert.True((await fresh.Suppliers.SingleAsync()).IsActive);
    }

    [Fact(DisplayName = "Se desactiva un proveedor sin órdenes activas")]
    public async Task Deactivate_without_active_orders()
    {
        var supplier = await _db.AddSupplierAsync();

        await _service.DeactivateAsync(supplier.Id, default);

        await using var fresh = _db.Fresh();
        Assert.False((await fresh.Suppliers.SingleAsync()).IsActive);
    }
}
