using Corteza.Application.Common.Interfaces;
using Corteza.Domain.Entities;
using Corteza.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;

namespace Corteza.Application.Tests;

/// <summary>
/// Base de datos en memoria por prueba (EF Core InMemory). Cuenta las llamadas a SaveChanges
/// para comprobar que una regla rota no guarda nada, como el "verify(repo, never()).save" del taller.
/// </summary>
internal sealed class TestDb : IDisposable
{
    private readonly string _name = Guid.NewGuid().ToString();

    public SaveChangesCounter Saves { get; } = new();
    public CortezaDbContext Context { get; }
    public FixedTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 2, 15, 0, 0, TimeSpan.Zero)); // 10:00 a. m. en Colombia
    public Mock<ICurrentUser> CurrentUser { get; } = new();
    public User User { get; }

    public TestDb()
    {
        Context = Create(withCounter: true);
        Context.Database.EnsureCreated(); // carga los roles de HasData

        User = new User { Username = "diego.rojas", PasswordHash = "x", RoleId = 6 };
        Context.Users.Add(User);
        Context.SaveChanges();
        Saves.Reset();

        CurrentUser.Setup(u => u.RequireUserId()).Returns(User.Id);
        CurrentUser.Setup(u => u.UserId).Returns(User.Id);
    }

    public DateOnly Today => new(2026, 11, 2);

    /// <summary>Contexto nuevo sobre la misma base: lee lo que realmente quedó guardado.</summary>
    public CortezaDbContext Fresh() => Create(withCounter: false);

    public async Task<Supplier> AddSupplierAsync(string name = "Molinos del Valle", string taxId = "900123456-7",
        decimal budget = 10_000_000, bool active = true)
    {
        var supplier = Supplier.Create(name, taxId, "Carlos Mendoza", "3104458890", "ventas@molinos.com", budget);
        if (!active) supplier.Deactivate(hasActiveOrders: false);
        Context.Suppliers.Add(supplier);
        await Context.SaveChangesAsync();
        Saves.Reset();
        return supplier;
    }

    public async Task<Supply> AddSupplyAsync(string name = "Harina de trigo", decimal stock = 40, decimal minStock = 20)
    {
        var category = await Context.SupplyCategories.FirstOrDefaultAsync(c => c.Name == "Harinas")
            ?? Context.SupplyCategories.Add(new SupplyCategory { Name = "Harinas" }).Entity;
        await Context.SaveChangesAsync();

        var supply = Supply.Create(name, category.Id, "Bulto de 50 kg", "bultos", 50, stock, minStock, null, User.Id, Today);
        Context.Supplies.Add(supply);
        await Context.SaveChangesAsync();
        Saves.Reset();
        return supply;
    }

    private CortezaDbContext Create(bool withCounter)
    {
        var builder = new DbContextOptionsBuilder<CortezaDbContext>()
            // Los códigos (OC-1042, SC-001...) los genera SQL Server; en memoria quedan vacíos.
            .UseInMemoryDatabase(_name, o => o.EnableNullChecks(false))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        if (withCounter) builder.AddInterceptors(Saves);
        return new CortezaDbContext(builder.Options);
    }

    public void Dispose() => Context.Dispose();
}

/// <summary>Cuenta cuántas veces se llamó SaveChanges.</summary>
internal sealed class SaveChangesCounter : SaveChangesInterceptor
{
    public int Count { get; private set; }

    public void Reset() => Count = 0;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Count++;
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Count++;
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}

/// <summary>Reloj fijo para que "hoy" no dependa del día en que corren las pruebas.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
