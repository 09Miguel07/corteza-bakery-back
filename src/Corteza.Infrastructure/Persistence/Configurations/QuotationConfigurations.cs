using Corteza.Domain.Entities;
using Corteza.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Corteza.Infrastructure.Persistence.Configurations;

internal sealed class RequestForQuotationConfiguration : IEntityTypeConfiguration<RequestForQuotation>
{
    public void Configure(EntityTypeBuilder<RequestForQuotation> b)
    {
        b.ToTable("RequestsForQuotation", t =>
            t.HasCheckConstraint("CK_RequestsForQuotation_Status", EnumCheck.In<RfqStatus>(nameof(RequestForQuotation.Status))));

        b.Property(r => r.Code)
            .HasMaxLength(12)
            .HasDefaultValueSql(Sequences.CodeDefault("SC-", Sequences.RequestForQuotation, "000"));
        b.HasIndex(r => r.Code).IsUnique();
        b.HasIndex(r => new { r.Status, r.CreatedAt });
        b.Ignore(r => r.LowestQuotation);

        b.HasOne(r => r.CreatedBy).WithMany().HasForeignKey(r => r.CreatedByUserId);
        b.HasMany(r => r.Items).WithOne().HasForeignKey(i => i.RequestForQuotationId);
        b.HasMany(r => r.Quotations).WithOne(q => q.RequestForQuotation).HasForeignKey(q => q.RequestForQuotationId);
    }
}

internal sealed class RequestForQuotationItemConfiguration : IEntityTypeConfiguration<RequestForQuotationItem>
{
    public void Configure(EntityTypeBuilder<RequestForQuotationItem> b)
    {
        b.ToTable(t => t.HasCheckConstraint("CK_RequestForQuotationItems_Quantity", "[Quantity] > 0"));
        b.Property(i => i.Quantity).HasPrecision(12, 2);
        b.HasOne(i => i.Supply).WithMany().HasForeignKey(i => i.SupplyId);
        b.HasIndex(i => new { i.RequestForQuotationId, i.SupplyId }).IsUnique();
    }
}

internal sealed class QuotationConfiguration : IEntityTypeConfiguration<Quotation>
{
    public void Configure(EntityTypeBuilder<Quotation> b)
    {
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Quotations_Status", EnumCheck.In<QuotationStatus>(nameof(Quotation.Status)));
            t.HasCheckConstraint("CK_Quotations_Total", "[Total] > 0");
            t.HasCheckConstraint("CK_Quotations_DeliveryDays", "[DeliveryDays] >= 0");
        });

        b.HasOne(q => q.Supplier).WithMany().HasForeignKey(q => q.SupplierId);
        b.HasMany(q => q.Items).WithOne().HasForeignKey(i => i.QuotationId);
        // Un proveedor cotiza una sola vez cada solicitud (también lo valida el dominio).
        b.HasIndex(q => new { q.RequestForQuotationId, q.SupplierId }).IsUnique();
    }
}

internal sealed class QuotationItemConfiguration : IEntityTypeConfiguration<QuotationItem>
{
    public void Configure(EntityTypeBuilder<QuotationItem> b)
    {
        b.ToTable(t => t.HasCheckConstraint("CK_QuotationItems_UnitPrice", "[UnitPrice] > 0"));
        b.Property(i => i.Quantity).HasPrecision(12, 2);
        b.Ignore(i => i.LineTotal);
        b.HasOne(i => i.Supply).WithMany().HasForeignKey(i => i.SupplyId);
    }
}
