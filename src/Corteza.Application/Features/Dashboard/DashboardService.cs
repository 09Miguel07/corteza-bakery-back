using Corteza.Application.Common.Interfaces;
using Corteza.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Corteza.Application.Features.Dashboard;

/// <summary>Contadores de las tarjetas del inicio del administrador (pantalla 07.0). Siempre salen de la BD.</summary>
public sealed record DashboardSummary(
    int ActiveCustomers,
    int OrdersToday,
    int LowStockSupplies,
    int ActiveSuppliers,
    int PendingPurchaseOrders,
    int ActiveEmployees);

public sealed class DashboardService(IAppDbContext db)
{
    public async Task<DashboardSummary> GetSummaryAsync(CancellationToken ct)
    {
        // "Hoy" en hora de Colombia (UTC-5, sin horario de verano). Las fechas se guardan en UTC.
        var startOfDayUtc = DateTime.UtcNow.AddHours(-5).Date.AddHours(5);

        return new DashboardSummary(
            ActiveCustomers: await db.Customers.CountAsync(c => c.IsActive, ct),
            OrdersToday: await db.Orders.CountAsync(o => o.CreatedAt >= startOfDayUtc && o.Status != OrderStatus.Cancelled, ct),
            LowStockSupplies: await db.Supplies.CountAsync(s => s.IsActive && s.CurrentStock < s.MinStock, ct),
            ActiveSuppliers: await db.Suppliers.CountAsync(s => s.IsActive, ct),
            PendingPurchaseOrders: await db.PurchaseOrders.CountAsync(
                o => o.Status == PurchaseOrderStatus.Requested || o.Status == PurchaseOrderStatus.Confirmed, ct),
            ActiveEmployees: await db.Employees.CountAsync(e => e.IsActive, ct));
    }
}
