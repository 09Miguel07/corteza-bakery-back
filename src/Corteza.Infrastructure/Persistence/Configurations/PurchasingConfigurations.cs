using Corteza.Domain.Entities;
using Corteza.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Corteza.Infrastructure.Persistence.Configurations;

internal sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> b)
    {
        b.Property(s => s.Name).HasMaxLength(120);
        b.Property(s => s.TaxId).HasMaxLength(20);
        b.Property(s => s.ContactName).HasMaxLength(80);
        b.Property(s => s.Phone).HasMaxLength(20);
        b.Property(s => s.Email).HasMaxLength(120);
        b.Property(s => s.RowVersion).IsRowVersion();
        b.ToTable(t => t.HasCheckConstraint("CK_Suppliers_AvailableBudget", "[AvailableBudget] >= 0"));
        b.HasIndex(s => s.TaxId).IsUnique();
        b.HasIndex(s => s.Name);
    }
}

internal sealed class SupplierSupplyCategoryConfiguration : IEntityTypeConfiguration<SupplierSupplyCategory>
{
    public void Configure(EntityTypeBuilder<SupplierSupplyCategory> b)
    {
        b.HasKey(l => new { l.SupplierId, l.SupplyCategoryId });
        b.HasOne(l => l.Supplier).WithMany(s => s.CategoryLinks).HasForeignKey(l => l.SupplierId);
        b.HasOne(l => l.SupplyCategory).WithMany(c => c.SupplierLinks).HasForeignKey(l => l.SupplyCategoryId);
    }
}

internal sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> b)
    {
        b.ToTable(t => t.HasCheckConstraint("CK_PurchaseOrders_Status", EnumCheck.In<PurchaseOrderStatus>(nameof(PurchaseOrder.Status))));

        b.Property(o => o.Code)
            .HasMaxLength(12)
            .HasDefaultValueSql(Sequences.CodeDefault("OC-", Sequences.PurchaseOrder, "0000"));
        b.HasIndex(o => o.Code).IsUnique();
        b.HasIndex(o => new { o.SupplierId, o.Status });

        b.Property(o => o.CancelReason).HasMaxLength(200);
        b.Ignore(o => o.IsActive);

        b.HasOne(o => o.Supplier).WithMany(s => s.PurchaseOrders).HasForeignKey(o => o.SupplierId);
        b.HasOne(o => o.CreatedBy).WithMany().HasForeignKey(o => o.CreatedByUserId);
        b.HasOne(o => o.ApprovedBy).WithMany().HasForeignKey(o => o.ApprovedByUserId);
        b.HasOne(o => o.Quotation).WithMany().HasForeignKey(o => o.QuotationId);
        b.HasIndex(o => o.QuotationId).IsUnique(); // una cotización genera como máximo una orden
        b.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.PurchaseOrderId);
    }
}

internal sealed class PurchaseOrderItemConfiguration : IEntityTypeConfiguration<PurchaseOrderItem>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderItem> b)
    {
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_PurchaseOrderItems_Quantity", "[Quantity] > 0");
            t.HasCheckConstraint("CK_PurchaseOrderItems_UnitPrice", "[UnitPrice] >= 0");
        });
        b.Property(i => i.Quantity).HasPrecision(12, 2);
        b.Ignore(i => i.LineTotal); // se calcula en el dominio (Quantity * UnitPrice)
        b.HasOne(i => i.Supply).WithMany().HasForeignKey(i => i.SupplyId);
        b.HasIndex(i => new { i.PurchaseOrderId, i.SupplyId }).IsUnique();
    }
}

internal sealed class PurchaseInvoiceConfiguration : IEntityTypeConfiguration<PurchaseInvoice>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoice> b)
    {
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_PurchaseInvoices_Status", EnumCheck.In<PurchaseInvoiceStatus>(nameof(PurchaseInvoice.Status)));
            t.HasCheckConstraint("CK_PurchaseInvoices_Total", "[Total] > 0");
            t.HasCheckConstraint("CK_PurchaseInvoices_Dates", "[DueDate] >= [IssueDate]");
        });
        b.Property(i => i.SupplierInvoiceNumber).HasMaxLength(30);
        b.HasIndex(i => new { i.SupplierId, i.SupplierInvoiceNumber }).IsUnique();
        b.HasOne(i => i.Supplier).WithMany().HasForeignKey(i => i.SupplierId);
        b.HasOne(i => i.PurchaseOrder).WithMany().HasForeignKey(i => i.PurchaseOrderId);
    }
}
