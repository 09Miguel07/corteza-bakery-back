using Corteza.Application.Features.Auth;
using Corteza.Application.Features.Catalog;
using Corteza.Application.Features.Customers;
using Corteza.Application.Features.Dashboard;
using Corteza.Application.Features.Employees;
using Corteza.Application.Features.Inventory;
using Corteza.Application.Features.Payroll;
using Corteza.Application.Features.Purchasing;
using Corteza.Application.Features.Quotations;
using Corteza.Application.Features.Reports;
using Corteza.Application.Features.Sales;
using Corteza.Application.Features.Suppliers;
using Corteza.Domain.Entities;
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
        services.AddScoped<PurchaseInvoiceService>();
        services.AddScoped<OrderService>();
        services.AddScoped<SalesInvoiceService>();
        services.AddSingleton(PayrollSettings.Colombia2026); // ⚠️ actualizar cada enero con el decreto del salario mínimo
        services.AddScoped<PayrollService>();
        services.AddScoped<ReportService>();
        return services;
    }
}
