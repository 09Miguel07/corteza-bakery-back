using Corteza.Application.Common.Interfaces;
using Corteza.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Corteza.Infrastructure.Persistence;

public sealed class CortezaDbContext(DbContextOptions<CortezaDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<PayrollPeriod> PayrollPeriods => Set<PayrollPeriod>();
    public DbSet<Payslip> Payslips => Set<Payslip>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Product> Products => Set<Product>();
    public DbSet<DiningTable> DiningTables => Set<DiningTable>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();

    public DbSet<SupplyCategory> SupplyCategories => Set<SupplyCategory>();
    public DbSet<Supply> Supplies => Set<Supply>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierSupplyCategory> SupplierSupplyCategories => Set<SupplierSupplyCategory>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();

    public DbSet<RequestForQuotation> RequestsForQuotation => Set<RequestForQuotation>();
    public DbSet<Quotation> Quotations => Set<Quotation>();

    /// <summary>Hijos que sí se borran en cascada con su padre (líneas de documentos y tablas puente).</summary>
    private static readonly HashSet<Type> CascadeChildren =
        [typeof(OrderItem), typeof(PurchaseOrderItem), typeof(PayslipLine), typeof(SupplierSupplyCategory),
         typeof(RequestForQuotationItem), typeof(QuotationItem)];

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Español, sin distinguir mayúsculas ni tildes: buscar "maria" encuentra "María".
        modelBuilder.UseCollation("Modern_Spanish_CI_AI");

        Sequences.Register(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CortezaDbContext).Assembly);

        // SQL Server no permite varios caminos de borrado en cascada hacia la misma tabla.
        // Todo es Restrict salvo las líneas de documento: los maestros se desactivan, no se borran.
        foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            if (!foreignKey.IsOwnership && !CascadeChildren.Contains(foreignKey.DeclaringEntityType.ClrType))
                foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Dinero y cantidades: decimal(14,2). Nunca float/double.
        configurationBuilder.Properties<decimal>().HavePrecision(14, 2);
        // Textos acotados por defecto; cada configuración ajusta su largo.
        configurationBuilder.Properties<string>().HaveMaxLength(200);
        // Enums como texto: legibles en SSMS y estables si cambia el orden de los miembros.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(20);
        // Fechas con hora siempre en UTC.
        configurationBuilder.Properties<DateTime>().HaveColumnType("datetime2(3)");
    }
}
