using Corteza.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Corteza.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.Property(c => c.Code)
            .HasMaxLength(12)
            .HasDefaultValueSql(Sequences.CodeDefault("CLI-", Sequences.Customer, "000"));
        b.HasIndex(c => c.Code).IsUnique();

        b.Property(c => c.DocumentNumber).HasMaxLength(20);
        b.Property(c => c.FirstNames).HasMaxLength(80);
        b.Property(c => c.LastNames).HasMaxLength(80);
        b.Property(c => c.Phone).HasMaxLength(20);
        b.Property(c => c.Email).HasMaxLength(120);

        b.HasIndex(c => c.DocumentNumber).IsUnique();
        b.HasIndex(c => new { c.FirstNames, c.LastNames });
    }
}
