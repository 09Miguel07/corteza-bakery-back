using Corteza.Application.Features.Reports;
using Corteza.Application.Features.Sales;
using Corteza.Domain.Enums;

namespace Corteza.Application.Tests;

public class ReportServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    [Fact(DisplayName = "El reporte suma las ventas pagadas, excluye las anuladas y ordena los más vendidos")]
    public async Task Overview_sums_paid_sales()
    {
        var orders = new OrderService(_db.Context, _db.CurrentUser.Object, _db.Time);
        var invoices = new SalesInvoiceService(_db.Context, _db.Time);
        var bread = await _db.AddProductAsync("Pan francés", 1_500);
        var cake = await _db.AddProductAsync("Torta de chocolate", 7_500);

        async Task<int> SellAsync(int productId, short quantity)
        {
            var order = await orders.CreateAsync(new OrderCreateRequest
            {
                ServiceType = ServiceType.Takeaway,
                Items = [new OrderItemRequest { ProductId = productId, Quantity = quantity }],
            }, default);
            return (await orders.CheckoutAsync(order.Id, new CheckoutRequest { PaymentMethod = PaymentMethod.Card }, default)).Id;
        }

        await SellAsync(bread.Id, 10);  // 15.000
        await SellAsync(cake.Id, 1);    // 7.500
        var voided = await SellAsync(cake.Id, 2);
        await invoices.VoidAsync(voided, new VoidSalesInvoiceRequest { Reason = "Prueba" }, default);

        var report = await new ReportService(_db.Context, _db.Time).OverviewAsync(ReportRange.Today, default);

        Assert.Equal(22_500, report.SalesTotal);
        Assert.Equal(2, report.InvoicesCount);
        Assert.Equal(11_250, report.AverageTicket);
        Assert.Equal("Pan francés", report.TopProducts.First().ProductName);
        Assert.Equal(7, report.Last7Days.Count);
        Assert.Equal(22_500, report.Last7Days.Last().Total);
    }
}
