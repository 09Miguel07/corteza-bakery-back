using Corteza.Application.Common.Exceptions;
using Corteza.Application.Features.Purchasing;
using Corteza.Domain.Enums;

namespace Corteza.Application.Tests;

public class PurchaseInvoiceServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly PurchaseInvoiceService _service;

    public PurchaseInvoiceServiceTests() => _service = new PurchaseInvoiceService(_db.Context, _db.Time);

    public void Dispose() => _db.Dispose();

    private PurchaseInvoiceCreateRequest Request(int supplierId, string number = "FC-9921", int dueInDays = 30) => new()
    {
        SupplierId = supplierId,
        SupplierInvoiceNumber = number,
        IssueDate = _db.Today.AddDays(-40),
        DueDate = _db.Today.AddDays(dueInDays),
        Total = 1_250_000,
    };

    [Fact(DisplayName = "Registrar una factura de compra la deja pendiente")]
    public async Task Create_saves_pending()
    {
        var supplier = await _db.AddSupplierAsync();

        var invoice = await _service.CreateAsync(Request(supplier.Id), default);

        Assert.Equal(PurchaseInvoiceStatus.Pending, invoice.Status);
        Assert.Equal("Molinos del Valle", invoice.SupplierName);
        Assert.False(invoice.IsOverdue);
    }

    [Fact(DisplayName = "El mismo número de factura del mismo proveedor no se registra dos veces")]
    public async Task Create_rejects_duplicate_number()
    {
        var supplier = await _db.AddSupplierAsync();
        await _service.CreateAsync(Request(supplier.Id), default);

        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(Request(supplier.Id, "fc-9921"), default));
    }

    [Fact(DisplayName = "El filtro Vencidas trae solo las pendientes con el vencimiento pasado")]
    public async Task List_overdue()
    {
        var supplier = await _db.AddSupplierAsync();
        await _service.CreateAsync(Request(supplier.Id, "FC-1", dueInDays: -2), default);
        await _service.CreateAsync(Request(supplier.Id, "FC-2", dueInDays: 10), default);

        var overdue = await _service.ListAsync(new PurchaseInvoiceQuery { View = PurchaseInvoiceView.Overdue }, default);
        var summary = await _service.SummaryAsync(default);

        Assert.Equal("FC-1", overdue.Items.Single().SupplierInvoiceNumber);
        Assert.Equal(1, summary.OverdueCount);
        Assert.Equal(2_500_000, summary.PendingTotal);
    }

    [Fact(DisplayName = "Pagar marca la factura como pagada")]
    public async Task Pay_marks_paid()
    {
        var supplier = await _db.AddSupplierAsync();
        var invoice = await _service.CreateAsync(Request(supplier.Id), default);

        var paid = await _service.PayAsync(invoice.Id, default);

        Assert.Equal(PurchaseInvoiceStatus.Paid, paid.Status);
        Assert.NotNull(paid.PaidAt);
    }
}
