using Corteza.Application.Common.Exceptions;
using Corteza.Application.Common.Interfaces;
using Corteza.Application.Features.Inventory;
using Corteza.Application.Features.Purchasing;
using Corteza.Application.Features.Quotations;
using Corteza.Application.Features.Sales;
using Corteza.Application.Features.Suppliers;
using Corteza.Domain.Entities;
using Corteza.Domain.Exceptions;
using Corteza.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Corteza.Specs.Support;

/// <summary>
/// Estado compartido de un escenario (equivale al "context" de Behave). Reqnroll crea uno por escenario
/// y lo inyecta en las clases de pasos. Los pasos llaman a los servicios reales sobre una base en memoria.
/// </summary>
public sealed class SpecsWorld : IDisposable
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    public SpecsWorld()
    {
        Db = NewContext();
        Db.Database.EnsureCreated();

        var user = new User { Username = "diego.rojas", PasswordHash = "x", RoleId = 6 };
        Db.Users.Add(user);
        Db.SaveChanges();

        var currentUser = new FixedCurrentUser(user.Id);
        Suppliers = new SupplierService(Db);
        Supplies = new SupplyService(Db, currentUser, Time);
        Movements = new InventoryMovementService(Db, currentUser, Time);
        Orders = new PurchaseOrderService(Db, currentUser, Time);
        Quotations = new QuotationService(Db, currentUser, Time);
        Orders2 = new OrderService(Db, currentUser, Time);
    }

    public CortezaDbContext Db { get; }
    public FixedTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 2, 15, 0, 0, TimeSpan.Zero));
    public DateOnly Today => new(2026, 11, 2);

    public SupplierService Suppliers { get; }
    public SupplyService Supplies { get; }
    public InventoryMovementService Movements { get; }
    public PurchaseOrderService Orders { get; }
    public QuotationService Quotations { get; }
    public OrderService Orders2 { get; }

    public Dictionary<string, int> ProductIds { get; } = [];
    public Dictionary<int, int> TableIds { get; } = [];
    public int SalesOrderId { get; set; }
    public SalesInvoiceDetail? LastInvoice { get; set; }

    public Dictionary<string, int> SupplierIds { get; } = [];
    public Dictionary<string, int> SupplyIds { get; } = [];
    public int RequestId { get; set; }
    public int OrderId { get; set; }
    public int? CreatedOrderId { get; set; }
    public string? LastQuotationSupplier { get; set; }

    /// <summary>Lo que "respondió el sistema": la etiqueta de éxito o el mensaje del error de negocio.</summary>
    public string? LastResponse { get; private set; }
    public Exception? LastError { get; private set; }

    /// <summary>Ejecuta una acción del usuario y guarda el resultado, sin dejar que la excepción tumbe el escenario.</summary>
    public async Task RunAsync(Func<Task> action, string successLabel)
    {
        try
        {
            await action();
            LastError = null;
            LastResponse = successLabel;
        }
        catch (Exception e) when (e is DomainException or BusinessRuleException or ConflictException)
        {
            LastError = e;
            LastResponse = e.Message;
        }
        finally
        {
            Db.ChangeTracker.Clear(); // como en la API: cada petición empieza sin entidades a medio cambiar
        }
    }

    public async Task<int> CategoryIdAsync()
    {
        var category = await Db.SupplyCategories.FirstOrDefaultAsync(c => c.Name == "General");
        if (category is null)
        {
            category = Db.SupplyCategories.Add(new SupplyCategory { Name = "General" }).Entity;
            await Db.SaveChangesAsync();
        }
        return category.Id;
    }

    public async Task<int> AddSupplierAsync(string name, string taxId, decimal budget = 20_000_000)
    {
        var supplier = Supplier.Create(name, taxId, "Contacto", "3000000000", "compras@proveedor.com", budget);
        Db.Suppliers.Add(supplier);
        await Db.SaveChangesAsync();
        SupplierIds[name] = supplier.Id;
        return supplier.Id;
    }

    public async Task<int> AddSupplyAsync(string name, decimal stock, string unitLabel)
    {
        var supply = Supply.Create(name, await CategoryIdAsync(), unitLabel, unitLabel, null, stock, 0, null,
            createdByUserId: 1, Today);
        Db.Supplies.Add(supply);
        await Db.SaveChangesAsync();
        SupplyIds[name] = supply.Id;
        return supply.Id;
    }

    /// <summary>Contexto nuevo para leer lo que realmente quedó guardado.</summary>
    public CortezaDbContext Fresh() => NewContext();

    private CortezaDbContext NewContext() => new(new DbContextOptionsBuilder<CortezaDbContext>()
        .UseInMemoryDatabase(_dbName, o => o.EnableNullChecks(false))
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options);

    public void Dispose() => Db.Dispose();
}

internal sealed class FixedCurrentUser(int userId) : ICurrentUser
{
    public int? UserId => userId;
    public string? Role => "PURCHASES";
    public int RequireUserId() => userId;
}

public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

/// <summary>Estados como se leen en los escenarios.</summary>
public static class Labels
{
    public static string Of(Enum status) => status.ToString() switch
    {
        "Open" => "ABIERTA",
        "Closed" => "CERRADA",
        "Cancelled" => "CANCELADA",
        "Received" => "RECIBIDA",
        "Selected" => "SELECCIONADA",
        "Rejected" => "RECHAZADA",
        "Pending" => "PENDIENTE",
        "Approved" => "APROBADA",
        var other => other.ToUpperInvariant(),
    };
}
