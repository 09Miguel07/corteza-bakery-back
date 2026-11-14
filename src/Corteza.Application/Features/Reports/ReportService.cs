using Corteza.Application.Common.Interfaces;
using Corteza.Application.Common.Time;
using Corteza.Domain.Entities;
using Corteza.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Corteza.Application.Features.Reports;

public enum ReportRange { Today, Week, Month }

public sealed record PaymentMethodTotal(PaymentMethod Method, int Count, decimal Total);
public sealed record DailySales(DateOnly Date, decimal Total);
public sealed record TopProduct(string ProductName, int Quantity, decimal Revenue);
public sealed record CriticalSupply(int Id, string Name, decimal CurrentStock, decimal MinStock, string UnitLabel, StockStatus Status);

/// <summary>Pantalla 07.1 · ¿Cómo va la panadería?</summary>
public sealed record ReportOverview(
    ReportRange Range,
    DateOnly From,
    DateOnly To,
    decimal SalesTotal,
    int InvoicesCount,
    decimal AverageTicket,
    int OrdersDelivered,
    int OrdersCancelled,
    IReadOnlyList<PaymentMethodTotal> ByPaymentMethod,
    IReadOnlyList<DailySales> Last7Days,
    IReadOnlyList<TopProduct> TopProducts,
    IReadOnlyList<CriticalSupply> CriticalSupplies,
    int PurchaseInvoicesPending,
    decimal PurchaseInvoicesPendingTotal,
    int PurchaseInvoicesOverdue,
    int ActiveEmployees,
    decimal? LastPayrollNet);

public sealed class ReportService(IAppDbContext db, TimeProvider time)
{
    private static DateTime UtcStartOf(DateOnly colombiaDay) => colombiaDay.ToDateTime(TimeOnly.MinValue).AddHours(5);
    private static DateOnly ColombiaDay(DateTime utc) => DateOnly.FromDateTime(utc.AddHours(-5));

    public async Task<ReportOverview> OverviewAsync(ReportRange range, CancellationToken ct)
    {
        var today = time.ColombiaToday();
        var from = range switch
        {
            ReportRange.Week => today.AddDays(-6),
            ReportRange.Month => new DateOnly(today.Year, today.Month, 1),
            _ => today,
        };
        var fromUtc = UtcStartOf(from);
        var toUtc = UtcStartOf(today.AddDays(1));

        var paid = db.SalesInvoices.AsNoTracking()
            .Where(i => i.Status == SalesInvoiceStatus.Paid && i.IssuedAt >= fromUtc && i.IssuedAt < toUtc);
        var sales = await paid.Select(i => new { i.Total, i.PaymentMethod }).ToListAsync(ct);
        var salesTotal = sales.Sum(s => s.Total);

        var orders = db.Orders.AsNoTracking().Where(o => o.CreatedAt >= fromUtc && o.CreatedAt < toUtc);

        // Ventas de los últimos 7 días (para la gráfica), siempre terminando hoy.
        var weekStart = today.AddDays(-6);
        var weekStartUtc = UtcStartOf(weekStart);
        var week = await db.SalesInvoices.AsNoTracking()
            .Where(i => i.Status == SalesInvoiceStatus.Paid && i.IssuedAt >= weekStartUtc && i.IssuedAt < toUtc)
            .Select(i => new { i.IssuedAt, i.Total })
            .ToListAsync(ct);
        var last7 = Enumerable.Range(0, 7)
            .Select(d => weekStart.AddDays(d))
            .Select(day => new DailySales(day, week.Where(i => ColombiaDay(i.IssuedAt) == day).Sum(i => i.Total)))
            .ToList();

        // Se agrupa en memoria: EF no traduce bien GroupBy + Sum sobre SelectMany en esta consulta.
        var lines = await db.SalesInvoices.AsNoTracking()
            .Where(i => i.Status == SalesInvoiceStatus.Paid && i.IssuedAt >= fromUtc && i.IssuedAt < toUtc)
            .SelectMany(i => i.Order.Items)
            .Select(l => new { l.Product.Name, l.Quantity, l.LineTotal })
            .ToListAsync(ct);
        var top = lines
            .GroupBy(l => l.Name)
            .Select(g => new TopProduct(g.Key, g.Sum(l => (int)l.Quantity), g.Sum(l => l.LineTotal)))
            .OrderByDescending(t => t.Quantity).ThenByDescending(t => t.Revenue)
            .Take(5)
            .ToList();

        var critical = await db.Supplies.AsNoTracking()
            .Where(s => s.IsActive && s.CurrentStock < s.MinStock)
            .OrderBy(s => s.CurrentStock / (s.MinStock == 0 ? 1 : s.MinStock))
            .Take(6)
            .Select(s => new { s.Id, s.Name, s.CurrentStock, s.MinStock, s.UnitLabel })
            .ToListAsync(ct);

        var pendingInvoices = await db.PurchaseInvoices.AsNoTracking()
            .Where(i => i.Status == PurchaseInvoiceStatus.Pending)
            .Select(i => new { i.Total, i.DueDate })
            .ToListAsync(ct);

        var lastPayroll = await db.PayrollPeriods.AsNoTracking()
            .Where(p => p.Status != PayrollStatus.Draft)
            .OrderByDescending(p => p.EndDate)
            .Select(p => (decimal?)p.Payslips.Sum(s => s.NetPay))
            .FirstOrDefaultAsync(ct);

        return new ReportOverview(
            range, from, today, salesTotal, sales.Count,
            sales.Count == 0 ? 0 : Math.Round(salesTotal / sales.Count, 0, MidpointRounding.AwayFromZero),
            await orders.CountAsync(o => o.Status == OrderStatus.Delivered, ct),
            await orders.CountAsync(o => o.Status == OrderStatus.Cancelled, ct),
            sales.GroupBy(s => s.PaymentMethod)
                .Select(g => new PaymentMethodTotal(g.Key, g.Count(), g.Sum(s => s.Total)))
                .OrderByDescending(p => p.Total).ToList(),
            last7, top,
            critical.Select(s => new CriticalSupply(s.Id, s.Name, s.CurrentStock, s.MinStock, s.UnitLabel, Supply.StatusOf(s.CurrentStock, s.MinStock))).ToList(),
            pendingInvoices.Count, pendingInvoices.Sum(i => i.Total), pendingInvoices.Count(i => i.DueDate < today),
            await db.Employees.CountAsync(e => e.IsActive, ct),
            lastPayroll);
    }
}
