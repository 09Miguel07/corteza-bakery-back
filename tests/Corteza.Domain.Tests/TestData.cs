using Corteza.Domain.Entities;

namespace Corteza.Domain.Tests;

/// <summary>Constructores de datos de prueba con valores válidos por defecto.</summary>
internal static class TestData
{
    public static readonly DateOnly Today = new(2026, 11, 2);
    public static readonly DateTime Now = new(2026, 11, 2, 14, 0, 0, DateTimeKind.Utc);
    public const int UserId = 7;

    public static Supplier Supplier(int id = 1, string taxId = "900123456-7", decimal budget = 10_000_000, bool active = true)
    {
        var supplier = Entities.Supplier.Create("Molinos del Valle", taxId, "Carlos Mendoza", "3104458890",
            "ventas@molinosdelvalle.com", budget);
        supplier.Id = id;
        if (!active) supplier.Deactivate(hasActiveOrders: false);
        return supplier;
    }
}
