using Corteza.Application.Common.Interfaces;
using Corteza.Infrastructure.Persistence;
using Corteza.Infrastructure.Persistence.Seed;
using Corteza.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Corteza.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Corteza")
            ?? throw new InvalidOperationException(
                "Falta ConnectionStrings:Corteza. Revisa appsettings.Development.json o los user-secrets de Corteza.Api.");

        // Sin EnableRetryOnFailure a propósito: choca con las transacciones manuales (movimientos de inventario).
        // Para Azure SQL se activa y las transacciones se envuelven en CreateExecutionStrategy().ExecuteAsync.
        services.AddDbContext<CortezaDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<CortezaDbContext>());

        services.AddScoped<IPasswordHasherService, PasswordHasherService>();
        services.AddScoped<DbInitializer>();
        return services;
    }
}
