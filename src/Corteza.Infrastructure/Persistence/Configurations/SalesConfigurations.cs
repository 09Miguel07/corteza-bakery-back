using Corteza.Domain.Entities;
using Corteza.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Corteza.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Products_Category", EnumCheck.In<ProductCategory>(nameof(Product.Category)));
            t.HasCheckConstraint("CK_Products_Price", "[Price] >= 0");
        });
        b.Property(p => p.Name).HasMaxLength(100);
        b.HasIndex(p => p.Name).IsUnique();
    }
}

internal sealed class DiningTableConfiguration : IEntityTypeConfiguration<DiningTable>
{
    public void Configure(EntityTypeBuilder<DiningTable> b)
    {
        b.ToTable(t => t.HasCheckConstraint("CK_DiningTables_Capacity", "[Capacity] > 0"));
        b.HasIndex(t => t.Number).IsUnique();
    }
}

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Orders_Status", EnumCheck.In<OrderStatus>(nameof(Order.Status)));
            t.HasCheckConstraint("CK_Orders_ServiceType", EnumCheck.In<ServiceType>(nameof(Order.ServiceType)));
            // En mesa ⇔ tiene mesa asignada.
            t.HasCheckConstraint("CK_Orders_Table",
                "([ServiceType] = 'DineIn' AND [DiningTableId] IS NOT NULL) OR ([ServiceType] = 'Takeaway' AND [DiningTableId] IS NULL)");
        });

        b.Property(o => o.Code)
            .HasMaxLength(12)
            .HasDefaultValueSql(Sequences.CodeDefault("P-", Sequences.Order, "0000"));
        b.HasIndex(o => o.Code).IsUnique();
        b.HasIndex(o => o.CreatedAt);

        // Una mesa solo puede tener un pedido abierto a la vez.
        b.HasIndex(o => o.DiningTableId)
            .IsUnique()
            .HasFilter("[DiningTableId] IS NOT NULL AND [ClosedAt] IS NULL")
            .HasDatabaseName("UX_Orders_OpenTable");

        b.HasOne(o => o.Customer).WithMany(c => c.Orders).HasForeignKey(o => o.CustomerId);
        b.HasOne(o => o.DiningTable).WithMany(t => t.Orders).HasForeignKey(o => o.DiningTableId);
        b.HasOne(o => o.ServedBy).WithMany().HasForeignKey(o => o.ServedByUserId);
        b.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId);
    }
}

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> b)
    {
        b.ToTable(t => t.HasCheckConstraint("CK_OrderItems_Quantity", "[Quantity] > 0"));
        b.Property(i => i.LineTotal).HasComputedColumnSql("[Quantity] * [UnitPrice]", stored: true);
        b.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId);
        b.HasIndex(i => new { i.OrderId, i.ProductId }).IsUnique();
    }
}

internal sealed class SalesInvoiceConfiguration : IEntityTypeConfiguration<SalesInvoice>
{
    public void Configure(EntityTypeBuilder<SalesInvoice> b)
    {
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_SalesInvoices_PaymentMethod", EnumCheck.In<PaymentMethod>(nameof(SalesInvoice.PaymentMethod)));
            t.HasCheckConstraint("CK_SalesInvoices_Status", EnumCheck.In<SalesInvoiceStatus>(nameof(SalesInvoice.Status)));
            // En efectivo, lo recibido cubre el total.
            t.HasCheckConstraint("CK_SalesInvoices_Cash", "[PaymentMethod] <> 'Cash' OR [AmountReceived] >= [Total]");
        });

        b.Property(i => i.Number)
            .HasMaxLength(12)
            .HasDefaultValueSql(Sequences.CodeDefault("FV-", Sequences.SalesInvoice, "0000"));
        b.HasIndex(i => i.Number).IsUnique();
        b.HasIndex(i => i.IssuedAt);

        b.HasOne(i => i.Order).WithOne(o => o.Invoice).HasForeignKey<SalesInvoice>(i => i.OrderId);
        b.HasOne(i => i.Customer).WithMany(c => c.SalesInvoices).HasForeignKey(i => i.CustomerId);
        b.HasOne(i => i.IssuedBy).WithMany().HasForeignKey(i => i.IssuedByUserId);
    }
}
