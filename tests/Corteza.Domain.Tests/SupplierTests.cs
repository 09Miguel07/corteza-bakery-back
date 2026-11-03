using Corteza.Domain.Entities;
using Corteza.Domain.Exceptions;

namespace Corteza.Domain.Tests;

/// <summary>Taller 1 · CRUD de proveedores: NIT único y válido, correo válido, nombre obligatorio, no se desactiva con órdenes activas.</summary>
public class SupplierTests
{
    [Fact(DisplayName = "Un proveedor válido se crea activo y con su presupuesto")]
    public void Create_with_valid_data_starts_active()
    {
        var supplier = Supplier.Create("ACME S.A.S.", "900123456", "Ana Ruiz", "3001112233", "contacto@acme.com", 5_000_000);

        Assert.True(supplier.IsActive);
        Assert.Equal("ACME S.A.S.", supplier.Name);
        Assert.Equal(5_000_000, supplier.AvailableBudget);
    }

    [Theory(DisplayName = "El NIT debe tener 9 o 10 dígitos y dígito de verificación opcional")]
    [InlineData("900123456", true)]
    [InlineData("9001234567", true)]
    [InlineData("900123456-7", true)]
    [InlineData("123", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("90012345A", false)]
    [InlineData("90012345678", false)]
    public void IsValidTaxId_accepts_only_valid_nits(string? taxId, bool expected)
    {
        Assert.Equal(expected, Supplier.IsValidTaxId(taxId));
    }

    [Theory(DisplayName = "El correo debe tener formato válido")]
    [InlineData("contacto@acme.com", true)]
    [InlineData("ventas@molinos.com.co", true)]
    [InlineData("sin-arroba.com", false)]
    [InlineData("usuario@", false)]
    [InlineData("", false)]
    public void IsValidEmail_checks_format(string email, bool expected)
    {
        Assert.Equal(expected, Supplier.IsValidEmail(email));
    }

    [Fact(DisplayName = "No se crea un proveedor con NIT inválido")]
    public void Create_rejects_invalid_nit()
    {
        var error = Assert.Throws<InvalidSupplierException>(() =>
            Supplier.Create("ACME", "123", "Ana Ruiz", "3001112233", "contacto@acme.com"));

        Assert.Equal("NIT inválido", error.Message);
    }

    [Fact(DisplayName = "No se crea un proveedor con correo mal formado")]
    public void Create_rejects_invalid_email()
    {
        var error = Assert.Throws<InvalidSupplierException>(() =>
            Supplier.Create("ACME", "900123456", "Ana Ruiz", "3001112233", "sin-arroba.com"));

        Assert.Equal("Formato de correo inválido", error.Message);
    }

    [Theory(DisplayName = "El nombre del proveedor es obligatorio")]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_empty_name(string name)
    {
        Assert.Throws<InvalidSupplierException>(() =>
            Supplier.Create(name, "900123456", "Ana Ruiz", "3001112233", "contacto@acme.com"));
    }

    [Fact(DisplayName = "El presupuesto no puede ser negativo")]
    public void Create_rejects_negative_budget()
    {
        Assert.Throws<InvalidSupplierException>(() =>
            Supplier.Create("ACME", "900123456", "Ana Ruiz", "3001112233", "contacto@acme.com", -1));
    }

    [Fact(DisplayName = "Se desactiva un proveedor sin órdenes activas")]
    public void Deactivate_without_active_orders_sets_inactive()
    {
        var supplier = TestData.Supplier();

        supplier.Deactivate(hasActiveOrders: false);

        Assert.False(supplier.IsActive);
    }

    [Fact(DisplayName = "No se desactiva un proveedor con órdenes activas y sigue activo")]
    public void Deactivate_with_active_orders_throws_and_stays_active()
    {
        var supplier = TestData.Supplier();

        var error = Assert.Throws<SupplierHasActiveOrdersException>(() => supplier.Deactivate(hasActiveOrders: true));

        Assert.Equal("No se puede desactivar: el proveedor tiene órdenes activas.", error.Message);
        Assert.True(supplier.IsActive);
    }

    [Fact(DisplayName = "Activar un proveedor inactivo lo deja activo")]
    public void Activate_sets_active()
    {
        var supplier = TestData.Supplier(active: false);

        supplier.Activate();

        Assert.True(supplier.IsActive);
    }

    [Fact(DisplayName = "Reservar presupuesto dentro del disponible lo descuenta")]
    public void ReserveBudget_within_budget_reduces_available()
    {
        var supplier = TestData.Supplier(budget: 10_000_000);

        supplier.ReserveBudget(5_000_000);

        Assert.Equal(5_000_000, supplier.AvailableBudget);
    }

    [Fact(DisplayName = "Reservar más del presupuesto disponible falla y no lo cambia")]
    public void ReserveBudget_exceeding_throws_and_keeps_budget()
    {
        var supplier = TestData.Supplier(budget: 1_000_000);

        Assert.Throws<BudgetExceededException>(() => supplier.ReserveBudget(1_000_001));
        Assert.Equal(1_000_000, supplier.AvailableBudget);
    }

    [Theory(DisplayName = "No se reserva un monto cero o negativo")]
    [InlineData(0)]
    [InlineData(-500)]
    public void ReserveBudget_rejects_non_positive_amounts(decimal amount)
    {
        var supplier = TestData.Supplier();

        Assert.Throws<InvalidAmountException>(() => supplier.ReserveBudget(amount));
    }
}
