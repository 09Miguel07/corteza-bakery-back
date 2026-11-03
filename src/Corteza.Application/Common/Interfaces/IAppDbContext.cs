using Corteza.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Corteza.Application.Common.Interfaces;

/// <summary>
/// Contrato del DbContext que usan los servicios. La implementación vive en Infrastructure,
/// así Application no depende de SQL Server.
/// </summary>
public interface IAppDbContext
{
    DbSet<Role> Roles { get; }
    DbSet<User> Users { get; }

    DbSet<Employee> Employees { get; }
    DbSet<PayrollPeriod> PayrollPeriods { get; }
    DbSet<Payslip> Payslips { get; }

    DbSet<Customer> Customers { get; }

    DbSet<Product> Products { get; }
    DbSet<DiningTable> DiningTables { get; }
    DbSet<Order> Orders { get; }
    DbSet<SalesInvoice> SalesInvoices { get; }

    DbSet<SupplyCategory> SupplyCategories { get; }
    DbSet<Supply> Supplies { get; }
    DbSet<InventoryMovement> InventoryMovements { get; }

    DbSet<Supplier> Suppliers { get; }
    DbSet<SupplierSupplyCategory> SupplierSupplyCategories { get; }
    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<PurchaseInvoice> PurchaseInvoices { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
