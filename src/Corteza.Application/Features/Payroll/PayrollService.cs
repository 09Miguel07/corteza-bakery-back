using Corteza.Application.Common.Exceptions;
using Corteza.Application.Common.Interfaces;
using Corteza.Domain.Entities;
using Corteza.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Corteza.Application.Features.Payroll;

/// <summary>
/// Módulo 06 · Nómina (06.0 calendario de pagos y 06.3 comprobante). El cálculo vive en
/// <see cref="PayrollPeriod"/>; los valores legales llegan por <see cref="PayrollSettings"/>.
/// </summary>
public sealed class PayrollService(IAppDbContext db, TimeProvider time, PayrollSettings settings)
{
    public async Task<IReadOnlyList<PayrollPeriodListItem>> ListAsync(int? year, CancellationToken ct)
    {
        var query = db.PayrollPeriods.AsNoTracking();
        if (year is { } y) query = query.Where(p => p.StartDate.Year == y || p.EndDate.Year == y);
        return await query
            .OrderByDescending(p => p.StartDate)
            .Select(p => new PayrollPeriodListItem(p.Id, p.StartDate, p.EndDate, p.PaymentDate, p.Status,
                p.Payslips.Count, p.Payslips.Sum(s => (decimal?)s.NetPay) ?? 0))
            .ToListAsync(ct);
    }

    public async Task<PayrollPeriodDetail> GetAsync(int id, CancellationToken ct)
    {
        var period = await db.PayrollPeriods.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw NotFoundException.For("el periodo de nómina", id);

        var slips = await db.Payslips.AsNoTracking()
            .Where(s => s.PeriodId == id)
            .OrderBy(s => s.Employee.FirstNames).ThenBy(s => s.Employee.LastNames)
            .Select(s => new PayslipListItem(s.Id, s.Code, s.EmployeeId, s.Employee.FirstNames + " " + s.Employee.LastNames,
                s.Employee.Position, s.DaysWorked, s.TotalEarnings, s.TotalDeductions, s.NetPay, s.Status))
            .ToListAsync(ct);

        return new PayrollPeriodDetail(period.Id, period.StartDate, period.EndDate, period.PaymentDate, period.Status, period.Days,
            slips.Sum(s => s.TotalEarnings), slips.Sum(s => s.TotalDeductions), slips.Sum(s => s.NetPay), slips);
    }

    public async Task<PayrollPeriodDetail> CreateAsync(PayrollPeriodCreateRequest request, CancellationToken ct)
    {
        var period = PayrollPeriod.Create(request.StartDate, request.EndDate, request.PaymentDate);

        if (await db.PayrollPeriods.AnyAsync(p => p.StartDate <= request.EndDate && request.StartDate <= p.EndDate, ct))
            throw new ConflictException("Ya existe un periodo de nómina que se cruza con esas fechas.");

        db.PayrollPeriods.Add(period);
        await db.SaveChangesAsync(ct);
        return await GetAsync(period.Id, ct);
    }

    /// <summary>Calcula (o recalcula, si sigue en borrador) un comprobante por cada empleado activo.</summary>
    public async Task<PayrollPeriodDetail> GenerateAsync(int id, CancellationToken ct)
    {
        var period = await LoadAsync(id, ct);
        var employees = await db.Employees.Where(e => e.IsActive).ToListAsync(ct);

        period.Generate(employees, settings);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<PayrollPeriodDetail> ApproveAsync(int id, CancellationToken ct)
    {
        var period = await LoadAsync(id, ct);
        period.Approve();
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<PayrollPeriodDetail> PayAsync(int id, CancellationToken ct)
    {
        var period = await LoadAsync(id, ct);
        period.MarkPaid(time.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<PayslipDetail> GetPayslipAsync(int id, CancellationToken ct)
    {
        var slip = await db.Payslips.AsNoTracking()
            .Include(s => s.Period).Include(s => s.Employee).Include(s => s.Lines)
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw NotFoundException.For("el comprobante de nómina", id);

        PayslipLineDto Line(PayslipLine l) => new(l.Concept, l.Description, l.Amount);
        var e = slip.Employee;
        return new PayslipDetail(slip.Id, slip.Code, slip.Status, slip.PeriodId, slip.Period.StartDate, slip.Period.EndDate,
            slip.Period.PaymentDate, e.FullName, e.DocumentType, e.DocumentNumber, e.Position, e.Department, e.MonthlySalary,
            e.Eps, e.PensionFund, e.BankName, Mask(e.BankAccountNumber), slip.DaysWorked,
            slip.Lines.Where(l => l.Type == PayslipLineType.Earning).OrderBy(l => l.Id).Select(Line).ToList(),
            slip.Lines.Where(l => l.Type == PayslipLineType.Deduction).OrderBy(l => l.Id).Select(Line).ToList(),
            slip.TotalEarnings, slip.TotalDeductions, slip.NetPay, slip.PaidAt);
    }

    private async Task<PayrollPeriod> LoadAsync(int id, CancellationToken ct) =>
        await db.PayrollPeriods.Include(p => p.Payslips).FirstOrDefaultAsync(p => p.Id == id, ct)
        ?? throw NotFoundException.For("el periodo de nómina", id);

    /// <summary>Dato sensible: solo se muestran los últimos 4 dígitos.</summary>
    private static string Mask(string? account) =>
        string.IsNullOrEmpty(account) ? "—" : "•••• " + account[^Math.Min(4, account.Length)..];
}
