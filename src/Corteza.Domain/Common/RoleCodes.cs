namespace Corteza.Domain.Common;

/// <summary>
/// Códigos de rol. Cada usuario tiene un solo rol y entra solo a su módulo (pantalla 00.2).
/// El administrador ve todos los módulos.
/// </summary>
public static class RoleCodes
{
    public const string Admin = "ADMIN";
    public const string Crm = "CRM";
    public const string Sales = "SALES";
    public const string Inventory = "INVENTORY";
    public const string Suppliers = "SUPPLIERS";
    public const string Purchases = "PURCHASES";
    public const string Hr = "HR";

    public static readonly IReadOnlyList<string> All = [Admin, Crm, Sales, Inventory, Suppliers, Purchases, Hr];
}
