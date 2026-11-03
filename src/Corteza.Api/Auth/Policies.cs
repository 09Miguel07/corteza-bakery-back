using Corteza.Domain.Common;

namespace Corteza.Api.Auth;

/// <summary>
/// Una política por módulo. El administrador entra a todo.
/// Los guards de Angular solo esconden pantallas: la seguridad real es esta (RNF-13).
/// </summary>
public static class Policies
{
    public const string Crm = nameof(Crm);
    public const string Sales = nameof(Sales);
    public const string InventoryRead = nameof(InventoryRead);
    public const string InventoryWrite = nameof(InventoryWrite);
    public const string SuppliersRead = nameof(SuppliersRead);
    public const string SuppliersWrite = nameof(SuppliersWrite);
    public const string Purchases = nameof(Purchases);
    public const string Hr = nameof(Hr);
    public const string Admin = nameof(Admin);

    public static IServiceCollection AddCortezaAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(Crm, p => p.RequireRole(RoleCodes.Crm, RoleCodes.Admin))
            // ⚠️ Supuesto: el vendedor de CRM también toma pedidos (pantalla 01.0 "Nuevo pedido").
            .AddPolicy(Sales, p => p.RequireRole(RoleCodes.Sales, RoleCodes.Crm, RoleCodes.Admin))
            // Compras necesita ver insumos para armar órdenes.
            .AddPolicy(InventoryRead, p => p.RequireRole(RoleCodes.Inventory, RoleCodes.Purchases, RoleCodes.Admin))
            .AddPolicy(InventoryWrite, p => p.RequireRole(RoleCodes.Inventory, RoleCodes.Admin))
            // Inventario y Compras eligen proveedores en sus formularios.
            .AddPolicy(SuppliersRead, p => p.RequireRole(RoleCodes.Suppliers, RoleCodes.Purchases, RoleCodes.Inventory, RoleCodes.Admin))
            .AddPolicy(SuppliersWrite, p => p.RequireRole(RoleCodes.Suppliers, RoleCodes.Admin))
            .AddPolicy(Purchases, p => p.RequireRole(RoleCodes.Purchases, RoleCodes.Admin)) // el jefe de compras aprueba
            .AddPolicy(Hr, p => p.RequireRole(RoleCodes.Hr, RoleCodes.Admin))
            .AddPolicy(Admin, p => p.RequireRole(RoleCodes.Admin));
        return services;
    }
}
