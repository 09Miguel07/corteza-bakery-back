using Corteza.Domain.Common;
using Corteza.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Corteza.Infrastructure.Persistence.Configurations;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.Property(r => r.Id).ValueGeneratedNever();
        b.Property(r => r.Code).HasMaxLength(30);
        b.Property(r => r.Name).HasMaxLength(60);
        b.HasIndex(r => r.Code).IsUnique();

        // Datos de referencia: van en la migración, no en el seed.
        b.HasData(
            new Role { Id = 1, Code = RoleCodes.Admin, Name = "Administrador" },
            new Role { Id = 2, Code = RoleCodes.Crm, Name = "CRM · Ventas" },
            new Role { Id = 3, Code = RoleCodes.Sales, Name = "Facturación y ventas" },
            new Role { Id = 4, Code = RoleCodes.Inventory, Name = "Inventarios" },
            new Role { Id = 5, Code = RoleCodes.Suppliers, Name = "Proveedores" },
            new Role { Id = 6, Code = RoleCodes.Purchases, Name = "Compras" },
            new Role { Id = 7, Code = RoleCodes.Hr, Name = "Talento Humano" });
    }
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.Property(u => u.Username).HasMaxLength(60);
        b.Property(u => u.Email).HasMaxLength(120);
        b.Property(u => u.PasswordHash).HasMaxLength(500);
        b.HasIndex(u => u.Username).IsUnique();
        b.HasIndex(u => u.Email).IsUnique(); // EF crea el índice filtrado WHERE [Email] IS NOT NULL

        b.HasOne(u => u.Role).WithMany(r => r.Users).HasForeignKey(u => u.RoleId);
        b.HasOne(u => u.Employee).WithOne(e => e.User).HasForeignKey<User>(u => u.EmployeeId);
    }
}
