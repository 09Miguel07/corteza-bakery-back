using Corteza.Application.Features.Auth;
using Corteza.Application.Features.Catalog;
using Corteza.Application.Features.Customers;
using Corteza.Application.Features.Dashboard;
using Corteza.Application.Features.Employees;
using Corteza.Application.Features.Inventory;
using Corteza.Application.Features.Purchasing;
using Corteza.Application.Features.Quotations;
using Corteza.Application.Features.Suppliers;
using Microsoft.Extensions.DependencyInjection;

namespace Corteza.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System); // reemplazable en pruebas
        services.AddScoped<AuthService>();
        services.AddScoped<CustomerService>();
        services.AddScoped<SupplierService>();
        services.AddScoped<SupplyCategoryService>();
        services.AddScoped<SupplyService>();
        services.AddScoped<InventoryMovementService>();
        services.AddScoped<EmployeeService>();
        services.AddScoped<ProductService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<PurchaseOrderService>();
        services.AddScoped<QuotationService>();
        return services;
    }
}
