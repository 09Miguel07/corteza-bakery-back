using Corteza.Application.Common.Exceptions;
using Corteza.Application.Common.Interfaces;
using Corteza.Application.Common.Models;
using Corteza.Application.Common.Validation;
using Corteza.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Corteza.Application.Features.Employees;

public sealed class EmployeeService(IAppDbContext db)
{
    private const int MinimumAge = 15; // edad mínima laboral en Colombia (con permiso hasta los 18)

    public async Task<PagedResult<EmployeeListItem>> ListAsync(EmployeeQuery q, CancellationToken ct)
    {
        var query = db.Employees.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var search = Normalize.Text(q.Search);
            var document = Normalize.Document(q.Search);
            query = query.Where(e =>
                (e.FirstNames + " " + e.LastNames).Contains(search)
                || (document != "" && e.DocumentNumber.Contains(document)));
        }

        if (!string.IsNullOrWhiteSpace(q.Position))
            query = query.Where(e => e.Position == q.Position);

        if (q.Shift is { } shift)
            query = query.Where(e => e.Shift == shift);

        if (q.Active is not null)
            query = query.Where(e => e.IsActive == q.Active);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.IsActive).ThenBy(e => e.FirstNames).ThenBy(e => e.LastNames)
            .Skip(q.Skip).Take(q.PageSize)
            .Select(e => new EmployeeListItem(
                e.Id, e.FirstNames, e.LastNames, e.DocumentType, e.DocumentNumber, e.Position, e.Shift, e.IsActive, e.User != null))
            .ToListAsync(ct);

        return new PagedResult<EmployeeListItem>(items, total, q.Page, q.PageSize);
    }

    public async Task<EmployeeSummary> SummaryAsync(CancellationToken ct)
    {
        var total = await db.Employees.CountAsync(ct);
        var active = await db.Employees.CountAsync(e => e.IsActive, ct);
        var percentage = total == 0 ? 0 : Math.Round(active * 100m / total, 1);
        return new EmployeeSummary(total, active, total - active, percentage);
    }

    public async Task<IReadOnlyList<string>> PositionsAsync(CancellationToken ct) =>
        await db.Employees.AsNoTracking().Select(e => e.Position).Distinct().OrderBy(p => p).ToListAsync(ct);

    public async Task<EmployeeDetail> GetAsync(int id, CancellationToken ct)
    {
        var employee = await db.Employees.AsNoTracking()
            .Include(e => e.User)
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw NotFoundException.For("el empleado", id);

        return ToDetail(employee);
    }

    public async Task<EmployeeDetail> CreateAsync(EmployeeCreateRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.BankAccountNumber))
            throw new BusinessRuleException("El número de cuenta es obligatorio para pagar la nómina.");

        var document = NormalizeDocument(request);
        if (await db.Employees.AnyAsync(e => e.DocumentNumber == document, ct))
            throw new ConflictException($"Ya existe un empleado con el documento {document}.");

        var employee = new Employee { DocumentNumber = document };
        Apply(employee, request);

        db.Employees.Add(employee);
        await db.SaveChangesAsync(ct);
        return await GetAsync(employee.Id, ct);
    }

    public async Task<EmployeeDetail> UpdateAsync(int id, EmployeeUpdateRequest request, CancellationToken ct)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw NotFoundException.For("el empleado", id);

        var document = NormalizeDocument(request);
        if (await db.Employees.AnyAsync(e => e.DocumentNumber == document && e.Id != id, ct))
            throw new ConflictException($"Ya existe un empleado con el documento {document}.");

        employee.DocumentNumber = document;
        Apply(employee, request);
        employee.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>
    /// Desvincular: el empleado queda inactivo y su usuario pierde el acceso de inmediato (RF-RH-006, RNF-14).
    /// El token vigente deja de servir en el siguiente /auth/me o al vencer.
    /// </summary>
    public async Task DeactivateAsync(int id, CancellationToken ct)
    {
        var employee = await db.Employees.Include(e => e.User).FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw NotFoundException.For("el empleado", id);

        employee.IsActive = false;
        employee.UpdatedAt = DateTime.UtcNow;
        if (employee.User is not null) employee.User.IsActive = false;

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Reactivar el empleado no reactiva su usuario: el acceso se devuelve aparte, a propósito.</summary>
    public async Task ActivateAsync(int id, CancellationToken ct)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw NotFoundException.For("el empleado", id);

        employee.IsActive = true;
        employee.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static string NormalizeDocument(EmployeeUpdateRequest request) =>
        request.DocumentType is Domain.Enums.DocumentType.PP
            ? Normalize.TaxId(request.DocumentNumber).ToUpperInvariant() // el pasaporte puede tener letras
            : Normalize.Document(request.DocumentNumber);

    private static void Apply(Employee e, EmployeeUpdateRequest r)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (r.BirthDate == default || r.BirthDate > today.AddYears(-MinimumAge))
            throw new BusinessRuleException($"La fecha de nacimiento no es válida: el empleado debe tener al menos {MinimumAge} años.");
        if (r.HireDate == default)
            throw new BusinessRuleException("La fecha de ingreso es obligatoria.");
        if (r.HireDate < r.BirthDate)
            throw new BusinessRuleException("La fecha de ingreso no puede ser anterior al nacimiento.");

        e.FirstNames = Normalize.Text(r.FirstNames);
        e.LastNames = Normalize.Text(r.LastNames);
        e.DocumentType = r.DocumentType;
        e.BirthDate = r.BirthDate;
        e.Phone = Normalize.Phone(r.Phone);
        e.Email = Normalize.Email(r.Email)!;
        e.Address = Normalize.Text(r.Address);
        e.EmergencyContact = Normalize.Text(r.EmergencyContact);

        e.Position = Normalize.Text(r.Position);
        e.Department = Normalize.Text(r.Department);
        e.WorkSite = Normalize.Text(r.WorkSite);
        e.ContractType = r.ContractType;
        e.HireDate = r.HireDate;
        e.MonthlySalary = r.MonthlySalary;
        e.Shift = r.Shift;

        e.Eps = Normalize.Text(r.Eps);
        e.PensionFund = Normalize.Text(r.PensionFund);
        e.Arl = Normalize.Text(r.Arl);
        e.CompensationFund = Normalize.Text(r.CompensationFund);

        e.BankName = Normalize.Text(r.BankName);
        e.BankAccountType = r.BankAccountType;
        if (!string.IsNullOrWhiteSpace(r.BankAccountNumber))
            e.BankAccountNumber = Normalize.Document(r.BankAccountNumber);
        e.PayFrequency = r.PayFrequency;
    }

    private static EmployeeDetail ToDetail(Employee e) => new(
        e.Id, e.FirstNames, e.LastNames, e.DocumentType, e.DocumentNumber, e.BirthDate, e.Phone, e.Email, e.Address,
        e.EmergencyContact, e.Position, e.Department, e.WorkSite, e.ContractType, e.HireDate, e.MonthlySalary, e.Shift,
        e.Eps, e.PensionFund, e.Arl, e.CompensationFund, e.BankName, e.BankAccountType, Mask(e.BankAccountNumber),
        e.PayFrequency, e.IsActive, e.User?.Username, e.CreatedAt, e.UpdatedAt);

    /// <summary>Solo los últimos 4 dígitos: "****4521".</summary>
    private static string Mask(string account) =>
        account.Length <= 4 ? new string('*', account.Length) : "****" + account[^4..];
}
