using Corteza.Application.Common.Interfaces;
using Corteza.Domain.Common;
using Corteza.Domain.Entities;
using Corteza.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Corteza.Infrastructure.Persistence.Seed;

/// <summary>
/// Aplica las migraciones y carga datos de demostración tomados de los mockups del Sprint 1.
/// Solo corre en Development (ver Program.cs). Si ya hay usuarios, no hace nada.
/// </summary>
public sealed class DbInitializer(
    CortezaDbContext db,
    IPasswordHasherService hasher,
    IConfiguration configuration,
    ILogger<DbInitializer> logger)
{
    public async Task InitialiseAsync(CancellationToken ct = default)
    {
        if (!db.Database.GetMigrations().Any())
        {
            logger.LogWarning(
                "No hay migraciones todavía. Desde la carpeta api ejecuta: " +
                "dotnet ef migrations add InitialSchema -p src/Corteza.Infrastructure -s src/Corteza.Api -o Persistence/Migrations");
            return;
        }

        await db.Database.MigrateAsync(ct);
        await SeedAsync(ct);
    }

    private async Task SeedAsync(CancellationToken ct)
    {
        if (await db.Users.AnyAsync(ct)) return;

        logger.LogInformation("Cargando datos de demostración de Panadería La Espiga…");
        var password = configuration["Seed:DefaultPassword"]
            ?? throw new InvalidOperationException("Falta Seed:DefaultPassword en appsettings.Development.json.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // ---------- Talento Humano + usuarios (pantalla 00.2) ----------
        var roles = await db.Roles.ToDictionaryAsync(r => r.Code, ct);

        (string User, string Role, Employee Employee)[] people =
        [
            ("laura.gomez", RoleCodes.Admin, NewEmployee("Laura", "Gómez", "1010234567", "Administradora", "Gerencia", WorkShift.Morning, 4_500_000)),
            ("andres.pardo", RoleCodes.Crm, NewEmployee("Andrés", "Pardo", "1015670334", "Vendedor", "Ventas", WorkShift.Morning, 1_900_000)),
            ("sofia.diaz", RoleCodes.Sales, NewEmployee("Sofía", "Díaz", "1032890112", "Cajera", "Ventas", WorkShift.Afternoon, 1_750_000)),
            ("ana.torres", RoleCodes.Inventory, NewEmployee("Ana", "Torres", "1024556890", "Auxiliar de bodega", "Bodega", WorkShift.Morning, 1_800_000)),
            ("camila.ortiz", RoleCodes.Suppliers, NewEmployee("Camila", "Ortiz", "1029334578", "Coordinadora de proveedores", "Compras", WorkShift.Morning, 2_600_000)),
            ("diego.rojas", RoleCodes.Purchases, NewEmployee("Diego", "Rojas", "1013778902", "Analista de compras", "Compras", WorkShift.Morning, 2_500_000)),
            ("paula.gomez", RoleCodes.Hr, NewEmployee("Paula", "Gómez", "1021445566", "Gestora de talento humano", "Talento Humano", WorkShift.Morning, 2_800_000)),
        ];

        foreach (var (username, role, employee) in people)
        {
            var user = new User
            {
                Username = username,
                Email = $"{username}@laespiga.co",
                RoleId = roles[role].Id,
                Employee = employee,
            };
            user.PasswordHash = hasher.Hash(user, password);
            db.Users.Add(user);
        }

        db.Employees.AddRange(
            NewEmployee("Carlos", "Ruiz", "1020450789", "Panadero", "Producción y Hornos", WorkShift.Morning, 2_200_000),
            NewEmployee("Laura", "Rojas", "1018324901", "Pastelera", "Producción y Hornos", WorkShift.Morning, 2_100_000),
            NewEmployee("Jorge", "León", "1019880445", "Panadero", "Producción y Hornos", WorkShift.Night, 2_200_000, active: false));

        await db.SaveChangesAsync(ct);
        var admin = await db.Users.SingleAsync(u => u.Username == "laura.gomez", ct);

        // ---------- CRM (pantalla 01.1) ----------
        db.Customers.AddRange(
            NewCustomer("1020456789", "María Fernanda", "López Gómez", "3124589012", "maria.lopez@gmail.com", new DateOnly(1992, 5, 14)),
            NewCustomer("1018932411", "Juan Camilo", "Ríos", "3001245589", "juan.rios@gmail.com"),
            NewCustomer("1037645120", "Valentina", "Gómez", "3158902341", "valentina.gomez@gmail.com"),
            NewCustomer("1024781990", "Andrés Felipe", "Torres", "3116729045"),
            NewCustomer("1042115308", "Laura", "Restrepo", "3145567812", "laura.restrepo@hotmail.com"),
            NewCustomer("1015678234", "Santiago", "Herrera", "3209814433"),
            NewCustomer("1033456892", "Camila", "Osorio", "3184329901", "camila.osorio@gmail.com"),
            NewCustomer("1022345671", "Daniel", "Vargas", "3167890123", active: false));

        // ---------- Proveedores (pantalla 04.1) ----------
        var categories = new[] { "Harinas", "Lácteos", "Huevos", "Azúcares", "Levaduras", "Empaques", "Chocolates", "Frutas" }
            .ToDictionary(n => n, n => new SupplyCategory { Name = n });
        db.SupplyCategories.AddRange(categories.Values);

        Supplier NewSupplier(string name, string taxId, string contact, string phone, string email, bool active, params string[] cats)
        {
            var supplier = Supplier.Create(name, taxId, contact, phone, email, availableBudget: 20_000_000);
            if (!active) supplier.Deactivate(hasActiveOrders: false);
            foreach (var c in cats) supplier.CategoryLinks.Add(new SupplierSupplyCategory { SupplyCategory = categories[c] });
            return supplier;
        }

        var molinos = NewSupplier("Molinos del Valle", "900123456-7", "Carlos Mendoza", "3104458890", "ventas@molinosdelvalle.com", true, "Harinas");
        var pradera = NewSupplier("Lácteos La Pradera", "860234567-1", "Marta Salazar", "3112203344", "pedidos@lacteoslapradera.com", true, "Lácteos");
        var dorado = NewSupplier("Avícola El Dorado", "901345678-2", "Hernán Quiroga", "3135567788", "ventas@avicolaeldorado.com", true, "Huevos");
        var azucarera = NewSupplier("Azucarera del Sur", "830456789-3", "Patricia Lozano", "3157781122", "comercial@azucareradelsur.com", true, "Azúcares");
        var trigal = NewSupplier("Levaduras El Trigal", "900567890-4", "Rubén Castaño", "3209981234", "ventas@levaduraseltrigal.com", true, "Levaduras");
        var kraft = NewSupplier("Empaques Kraft", "901678901-5", "Diana Becerra", "3186654321", "pedidos@empaqueskraft.com", true, "Empaques");
        db.Suppliers.AddRange(molinos, pradera, dorado, azucarera, trigal, kraft,
            NewSupplier("Cacao Colombia", "860789012-6", "Felipe Arango", "3014456789", "ventas@cacaocolombia.com", true, "Chocolates"),
            NewSupplier("Frutas del Norte", "900890123-7", "Gloria Pineda", "3123344556", "contacto@frutasdelnorte.com", false, "Frutas"));

        // ---------- Inventario (pantalla 03.1) ----------
        Supply NewSupply(string name, string category, string unit, string label, decimal? kg, decimal stock, decimal min, Supplier? supplier)
        {
            var supply = new Supply
            {
                Name = name, Category = categories[category], PurchaseUnit = unit, UnitLabel = label, UnitSizeKg = kg,
                CurrentStock = stock, MinStock = min, MainSupplier = supplier,
            };
            if (stock > 0)
            {
                supply.Movements.Add(new InventoryMovement
                {
                    Type = MovementType.In, Quantity = stock, StockAfter = stock, Supplier = supplier,
                    MovementDate = DateOnly.FromDateTime(DateTime.Today), Notes = "Stock inicial (datos de demostración)",
                    CreatedByUserId = admin.Id,
                });
            }
            return supply;
        }

        db.Supplies.AddRange(
            NewSupply("Harina de trigo", "Harinas", "Bulto de 50 kg", "bultos", 50, 65, 20, molinos),
            NewSupply("Mantequilla pura", "Lácteos", "Caja de 25 kg", "cajas", 25, 8, 15, pradera),
            NewSupply("Azúcar refinada", "Azúcares", "Bulto de 50 kg", "bultos", 50, 42, 15, azucarera),
            NewSupply("Levadura fresca", "Levaduras", "Bloque de 500 g", "bloques", 0.5m, 120, 40, trigal),
            NewSupply("Huevos frescos AA", "Huevos", "Cubeta x 30 unidades", "cubetas", null, 35, 20, dorado),
            NewSupply("Leche entera pasteurizada", "Lácteos", "Bolsa de 1 litro", "bolsas", null, 0, 60, pradera),
            NewSupply("Cajas de cartón para tortas", "Empaques", "Caja estándar 30x30 cm", "unidades", null, 280, 100, kraft),
            NewSupply("Bolsas de papel kraft", "Empaques", "Bolsa de 2 kg para panes", "unidades", null, 450, 500, kraft));

        // ---------- Ventas: catálogo y mesas (pantallas 02.2 y 02.4) ----------
        db.Products.AddRange(
            new Product { Name = "Pan francés", Category = ProductCategory.Bakery, Price = 1_500, Description = "Horneado hoy" },
            new Product { Name = "Croissant de mantequilla", Category = ProductCategory.Pastry, Price = 4_500, Description = "Receta clásica hojaldrada" },
            new Product { Name = "Pan de bono", Category = ProductCategory.Bakery, Price = 2_200, Description = "Con queso costeño artesanal" },
            new Product { Name = "Café con leche", Category = ProductCategory.Drink, Price = 3_800, Description = "Taza de 8 oz" },
            // ⚠️ El mockup muestra $5.000 en 02.2 y $3.500 en 02.5; se tomó el de "Nuevo pedido".
            new Product { Name = "Jugo de naranja", Category = ProductCategory.Drink, Price = 5_000, Description = "Vaso 12 oz, recién exprimido" },
            new Product { Name = "Torta de chocolate", Category = ProductCategory.Dessert, Price = 7_500, Description = "Porción del día" });

        short[] capacities = [4, 4, 2, 4, 4, 6, 4, 2, 4, 4, 2, 6];
        db.DiningTables.AddRange(capacities.Select((capacity, i) => new DiningTable { Number = (short)(i + 1), Capacity = capacity }));

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        logger.LogInformation("Datos de demostración cargados. Usuarios: {Users}", string.Join(", ", people.Select(p => p.User)));
    }

    private static Customer NewCustomer(string document, string firstNames, string lastNames, string phone,
        string? email = null, DateOnly? birthday = null, bool active = true) => new()
    {
        DocumentNumber = document, FirstNames = firstNames, LastNames = lastNames, Phone = phone,
        Email = email, Birthday = birthday, IsActive = active,
    };

    private static Employee NewEmployee(string firstNames, string lastNames, string document, string position,
        string department, WorkShift shift, decimal salary, bool active = true) => new()
    {
        FirstNames = firstNames,
        LastNames = lastNames,
        DocumentType = DocumentType.CC,
        DocumentNumber = document,
        BirthDate = new DateOnly(1994, 3, 10),
        Phone = "+573001234567",
        Email = $"{Slug(firstNames)}.{Slug(lastNames)}@laespiga.co",
        Address = "Calle 12 # 9-45, Chía, Cundinamarca",
        EmergencyContact = "Familiar de contacto - +57 310 000 0000",
        Position = position,
        Department = department,
        WorkSite = "Sede principal Chía",
        ContractType = ContractType.Indefinite,
        HireDate = new DateOnly(2025, 2, 1),
        MonthlySalary = salary,
        Shift = shift,
        Eps = "Sura EPS",
        PensionFund = "Protección",
        Arl = "Sura ARL (Riesgo II)",
        CompensationFund = "Cafam",
        BankName = "Bancolombia",
        BankAccountType = BankAccountType.Savings,
        BankAccountNumber = "4091" + document[^6..],
        PayFrequency = PayFrequency.Biweekly,
        IsActive = active,
    };

    private static string Slug(string value) =>
        new string(value.Split(' ')[0].ToLowerInvariant()
            .Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray());
}

public static class DbInitializerExtensions
{
    public static async Task InitialiseDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DbInitializer>().InitialiseAsync();
    }
}
