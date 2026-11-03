using Corteza.Application.Features.Quotations;
using Corteza.Domain.Enums;
using Corteza.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Corteza.Application.Tests;

public class QuotationServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly QuotationService _service;

    public QuotationServiceTests() =>
        _service = new QuotationService(_db.Context, _db.CurrentUser.Object, _db.Time);

    public void Dispose() => _db.Dispose();

    private async Task<RfqDetail> NewRequestAsync(int supplyId, decimal quantity = 25) =>
        await _service.CreateAsync(new RfqCreateRequest
        {
            Description = "Harina para la semana",
            ResponseDeadline = _db.Today.AddDays(3),
            Items = [new RfqLineRequest { SupplyId = supplyId, Quantity = quantity }],
        }, default);

    private static QuotationCreateRequest Offer(int supplierId, int supplyId, decimal price, int days = 2) => new()
    {
        SupplierId = supplierId,
        DeliveryDays = days,
        Prices = [new QuotationPriceRequest { SupplyId = supplyId, UnitPrice = price }],
    };

    [Fact(DisplayName = "Crear una solicitud la deja ABIERTA")]
    public async Task Create_saves_open_request()
    {
        var flour = await _db.AddSupplyAsync();

        var request = await NewRequestAsync(flour.Id);

        Assert.Equal(RfqStatus.Open, request.Status);
        Assert.Equal("Harina de trigo", Assert.Single(request.Items).SupplyName);
    }

    [Fact(DisplayName = "Registrar la cotización de un proveedor la deja RECIBIDA con su total")]
    public async Task RegisterQuotation_saves_received_quotation()
    {
        var flour = await _db.AddSupplyAsync();
        var supplier = await _db.AddSupplierAsync();
        var request = await NewRequestAsync(flour.Id);

        var detail = await _service.RegisterQuotationAsync(request.Id, Offer(supplier.Id, flour.Id, 50_000), default);

        var quotation = Assert.Single(detail.Quotations);
        Assert.Equal(QuotationStatus.Received, quotation.Status);
        Assert.Equal(1_250_000, quotation.Total);
    }

    [Fact(DisplayName = "Una segunda cotización del mismo proveedor se rechaza sin guardar")]
    public async Task RegisterQuotation_duplicate_supplier_does_not_save()
    {
        var flour = await _db.AddSupplyAsync();
        var supplier = await _db.AddSupplierAsync();
        var request = await NewRequestAsync(flour.Id);
        await _service.RegisterQuotationAsync(request.Id, Offer(supplier.Id, flour.Id, 50_000), default);
        _db.Saves.Reset();

        await Assert.ThrowsAsync<DuplicateQuotationException>(() =>
            _service.RegisterQuotationAsync(request.Id, Offer(supplier.Id, flour.Id, 48_000), default));

        Assert.Equal(0, _db.Saves.Count);
    }

    [Fact(DisplayName = "Una cotización con monto cero se rechaza sin guardar")]
    public async Task RegisterQuotation_zero_amount_does_not_save()
    {
        var flour = await _db.AddSupplyAsync();
        var supplier = await _db.AddSupplierAsync();
        var request = await NewRequestAsync(flour.Id);
        _db.Saves.Reset();

        await Assert.ThrowsAsync<InvalidAmountException>(() =>
            _service.RegisterQuotationAsync(request.Id, Offer(supplier.Id, flour.Id, 0), default));

        Assert.Equal(0, _db.Saves.Count);
    }

    [Fact(DisplayName = "Elegir la ganadora cierra la solicitud y genera la orden PENDIENTE con sus precios")]
    public async Task Select_creates_purchase_order_from_winner()
    {
        var flour = await _db.AddSupplyAsync();
        var molinos = await _db.AddSupplierAsync("Molinos del Valle", "900123456-7");
        var harinera = await _db.AddSupplierAsync("Harinera San Antonio", "901234567-1");
        var request = await NewRequestAsync(flour.Id);
        await _service.RegisterQuotationAsync(request.Id, Offer(molinos.Id, flour.Id, 52_000), default);
        var detail = await _service.RegisterQuotationAsync(request.Id, Offer(harinera.Id, flour.Id, 50_000, days: 3), default);
        var winner = detail.Quotations.Single(q => q.SupplierId == harinera.Id);

        var result = await _service.SelectAsync(request.Id, winner.Id, new SelectQuotationRequest(), default);

        await using var fresh = _db.Fresh();
        var order = await fresh.PurchaseOrders.Include(o => o.Items).SingleAsync(o => o.Id == result.PurchaseOrderId);
        Assert.Equal(PurchaseOrderStatus.Pending, order.Status);
        Assert.Equal(harinera.Id, order.SupplierId);
        Assert.Equal(1_250_000, order.Total);
        Assert.Equal(_db.Today.AddDays(3), order.ExpectedDeliveryDate); // hoy + días de entrega ofrecidos
        var closed = await _service.GetAsync(request.Id, default);
        Assert.Equal(RfqStatus.Closed, closed.Status);
        Assert.Equal(result.PurchaseOrderId, closed.PurchaseOrderId);
    }

    [Fact(DisplayName = "La solicitud indica cuál es la oferta más barata")]
    public async Task Get_marks_lowest_quotation()
    {
        var flour = await _db.AddSupplyAsync();
        var molinos = await _db.AddSupplierAsync("Molinos del Valle", "900123456-7");
        var harinera = await _db.AddSupplierAsync("Harinera San Antonio", "901234567-1");
        var request = await NewRequestAsync(flour.Id);
        await _service.RegisterQuotationAsync(request.Id, Offer(molinos.Id, flour.Id, 52_000), default);
        var detail = await _service.RegisterQuotationAsync(request.Id, Offer(harinera.Id, flour.Id, 50_000), default);

        Assert.Equal(detail.Quotations.Single(q => q.SupplierId == harinera.Id).Id, detail.LowestQuotationId);
    }
}
