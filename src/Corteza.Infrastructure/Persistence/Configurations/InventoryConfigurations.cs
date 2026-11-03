using Corteza.Domain.Entities;
using Corteza.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Corteza.Infrastructure.Persistence.Configurations;

internal sealed class SupplyCategoryConfiguration : IEntityTypeConfiguration<SupplyCategory>
{
    public void Configure(EntityTypeBuilder<SupplyCategory> b)
    {
        b.Property(c => c.Name).HasMaxLength(40);
        b.HasIndex(c => c.Name).IsUnique();
    }
}

internal sealed class SupplyConfiguration : IEntityTypeConfiguration<Supply>
{
    public void Configure(EntityTypeBuilder<Supply> b)
    {
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Supplies_CurrentStock", "[CurrentStock] >= 0");
            t.HasCheckConstraint("CK_Supplies_MinStock", "[MinStock] >= 0");
        });

        b.Property(s => s.Code)
            .HasMaxLength(12)
            .HasDefaultValueSql(Sequences.CodeDefault("INS-", Sequences.Supply, "000"));
        b.HasIndex(s => s.Code).IsUnique();
        b.Property(s => s.RowVersion).IsRowVersion();
        b.Ignore(s => s.Status);

        b.Property(s => s.Name).HasMaxLength(100);
        b.Property(s => s.PurchaseUnit).HasMaxLength(60);
        b.Property(s => s.UnitLabel).HasMaxLength(20);
        b.Property(s => s.UnitSizeKg).HasPrecision(10, 3);
        b.Property(s => s.CurrentStock).HasPrecision(12, 2);
        b.Property(s => s.MinStock).HasPrecision(12, 2);
        b.HasIndex(s => s.Name).IsUnique();

        b.HasOne(s => s.Category).WithMany(c => c.Supplies).HasForeignKey(s => s.CategoryId);
        b.HasOne(s => s.MainSupplier).WithMany().HasForeignKey(s => s.MainSupplierId);
    }
}

internal sealed class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> b)
    {
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_InventoryMovements_Type", EnumCheck.In<MovementType>(nameof(InventoryMovement.Type)));
            t.HasCheckConstraint("CK_InventoryMovements_Quantity", "[Quantity] > 0");
        });

        b.Property(m => m.Quantity).HasPrecision(12, 2);
        b.Property(m => m.StockAfter).HasPrecision(12, 2);
        b.Property(m => m.Location).HasMaxLength(60);
        b.HasIndex(m => new { m.SupplyId, m.CreatedAt });

        b.HasOne(m => m.Supply).WithMany(s => s.Movements).HasForeignKey(m => m.SupplyId);
        b.HasOne(m => m.Supplier).WithMany().HasForeignKey(m => m.SupplierId);
        b.HasOne(m => m.PurchaseOrder).WithMany().HasForeignKey(m => m.PurchaseOrderId);
        b.HasOne(m => m.CreatedBy).WithMany().HasForeignKey(m => m.CreatedByUserId);
    }
}
