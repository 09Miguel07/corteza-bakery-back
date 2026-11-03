using Corteza.Application.Common.Exceptions;
using Corteza.Application.Common.Interfaces;
using Corteza.Application.Common.Models;
using Corteza.Application.Common.Validation;
using Corteza.Domain.Entities;
using Corteza.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Corteza.Application.Features.Customers;

public sealed class CustomerService(IAppDbContext db)
{
    public async Task<PagedResult<CustomerListItem>> ListAsync(CustomerQuery q, CancellationToken ct)
    {
        var query = db.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q.Document))
        {
            var document = Normalize.Document(q.Document);
            query = query.Where(c => c.DocumentNumber.Contains(document));
        }

        if (!string.IsNullOrWhiteSpace(q.Name))
        {
            var name = Normalize.Text(q.Name);
            query = query.Where(c => (c.FirstNames + " " + c.LastNames).Contains(name));
        }

        if (q.Active is not null)
            query = query.Where(c => c.IsActive == q.Active);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(c => c.FirstNames).ThenBy(c => c.LastNames)
            .Skip(q.Skip).Take(q.PageSize)
            .Select(c => new CustomerListItem(c.Id, c.Code, c.DocumentNumber, c.FirstNames, c.LastNames, c.Phone, c.Email, c.IsActive))
            .ToListAsync(ct);

        return new PagedResult<CustomerListItem>(items, total, q.Page, q.PageSize);
    }

    public async Task<CustomerDetail> GetAsync(int id, CancellationToken ct)
    {
        return await db.Customers.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CustomerDetail(
                c.Id, c.Code, c.DocumentNumber, c.FirstNames, c.LastNames, c.Phone, c.Email, c.Birthday,
                c.IsActive, c.CreatedAt, c.UpdatedAt,
                c.Orders.Count(o => o.Status != OrderStatus.Cancelled),
                c.SalesInvoices.Where(i => i.Status == SalesInvoiceStatus.Paid).Sum(i => (decimal?)i.Total) ?? 0))
            .FirstOrDefaultAsync(ct)
            ?? throw NotFoundException.For("el cliente", id);
    }

    public async Task<CustomerDetail> CreateAsync(CustomerCreateRequest request, CancellationToken ct)
    {
        var document = Normalize.Document(request.DocumentNumber);
        if (await db.Customers.AnyAsync(c => c.DocumentNumber == document, ct))
            throw new ConflictException($"Ya existe un cliente con la cédula {document}.");

        var customer = new Customer { DocumentNumber = document };
        Apply(customer, request);

        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);
        return await GetAsync(customer.Id, ct);
    }

    public async Task<CustomerDetail> UpdateAsync(int id, CustomerUpdateRequest request, CancellationToken ct)
    {
        var customer = await FindAsync(id, ct);
        Apply(customer, request);
        customer.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Desactivar no borra: el cliente deja de aparecer al crear pedidos, pero conserva su historial.</summary>
    public async Task SetActiveAsync(int id, bool active, CancellationToken ct)
    {
        var customer = await FindAsync(id, ct);
        if (customer.IsActive == active) return;

        customer.IsActive = active;
        customer.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task<Customer> FindAsync(int id, CancellationToken ct) =>
        await db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct)
        ?? throw NotFoundException.For("el cliente", id);

    private static void Apply(Customer customer, CustomerUpdateRequest request)
    {
        if (request.Birthday is { } birthday && birthday > DateOnly.FromDateTime(DateTime.Today))
            throw new BusinessRuleException("La fecha de cumpleaños no puede ser futura.");

        customer.FirstNames = Normalize.Text(request.FirstNames);
        customer.LastNames = Normalize.Text(request.LastNames);
        customer.Phone = Normalize.Phone(request.Phone);
        customer.Email = Normalize.Email(request.Email);
        customer.Birthday = request.Birthday;
    }
}
