using System.Text.RegularExpressions;
using Corteza.Domain.Exceptions;

namespace Corteza.Domain.Entities;

/// <summary>
/// Proveedor de insumos. Las reglas del taller 1 viven aquí:
/// NIT válido, correo válido, nombre obligatorio, no se desactiva con órdenes activas
/// y la aprobación de órdenes descuenta su presupuesto disponible.
/// </summary>
public class Supplier
{
    private static readonly Regex TaxIdPattern = new(@"^\d{9,10}(-\d)?$", RegexOptions.Compiled);
    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    public int Id { get; set; }
    public string Name { get; private set; } = null!;
    public string TaxId { get; private set; } = null!; // NIT sin puntos: 900123456-7
    public string ContactName { get; private set; } = null!;
    public string Phone { get; private set; } = null!;
    public string Email { get; private set; } = null!;

    /// <summary>Presupuesto disponible para aprobar órdenes de compra (taller 3).</summary>
    public decimal AvailableBudget { get; private set; }

    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Token de concurrencia: dos aprobaciones simultáneas no pueden gastar el mismo presupuesto.</summary>
    public byte[]? RowVersion { get; set; }

    public ICollection<SupplierSupplyCategory> CategoryLinks { get; set; } = [];
    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = [];

    private Supplier() { } // EF Core

    public static Supplier Create(string name, string taxId, string contactName, string phone, string email, decimal availableBudget = 0)
    {
        var supplier = new Supplier();
        supplier.UpdateDetails(name, taxId, contactName, phone, email, availableBudget);
        return supplier;
    }

    public void UpdateDetails(string name, string taxId, string contactName, string phone, string email, decimal availableBudget)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidSupplierException("El nombre del proveedor es obligatorio.");
        if (!IsValidTaxId(taxId))
            throw new InvalidSupplierException("NIT inválido");
        if (!IsValidEmail(email))
            throw new InvalidSupplierException("Formato de correo inválido");
        if (string.IsNullOrWhiteSpace(contactName))
            throw new InvalidSupplierException("La persona de contacto es obligatoria.");
        if (availableBudget < 0)
            throw new InvalidSupplierException("El presupuesto no puede ser negativo.");

        Name = name.Trim();
        TaxId = taxId;
        ContactName = contactName.Trim();
        Phone = phone;
        Email = email.Trim().ToLowerInvariant();
        AvailableBudget = availableBudget;
    }

    /// <summary>NIT de 9 o 10 dígitos, con dígito de verificación opcional: 900123456 o 900123456-7.</summary>
    public static bool IsValidTaxId(string? taxId) => taxId is not null && TaxIdPattern.IsMatch(taxId);

    public static bool IsValidEmail(string? email) => email is not null && EmailPattern.IsMatch(email.Trim());

    /// <summary>Desactivar no borra: conserva el historial (RF-PRO004).</summary>
    public void Deactivate(bool hasActiveOrders)
    {
        if (!IsActive) return;
        if (hasActiveOrders) throw new SupplierHasActiveOrdersException();
        IsActive = false;
    }

    public void Activate() => IsActive = true;

    /// <summary>Descuenta el valor de una orden aprobada. Si no alcanza, falla sin cambiar nada.</summary>
    public void ReserveBudget(decimal amount)
    {
        if (amount <= 0) throw new InvalidAmountException();
        if (amount > AvailableBudget) throw new BudgetExceededException(amount, AvailableBudget);
        AvailableBudget -= amount;
    }
}

/// <summary>Qué categorías de insumo vende cada proveedor ("¿Qué nos vende?", pantalla 04.2).</summary>
public class SupplierSupplyCategory
{
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public int SupplyCategoryId { get; set; }
    public SupplyCategory SupplyCategory { get; set; } = null!;
}
