using Corteza.Domain.Enums;

namespace Corteza.Application.Features.Payroll;

public sealed record PayrollPeriodListItem(
    int Id,
    DateOnly StartDate,
    DateOnly EndDate,
    DateOnly PaymentDate,
    PayrollStatus Status,
    int EmployeesCount,
    decimal TotalNet);

public sealed record PayslipListItem(
    int Id,
    string Code,
    int EmployeeId,
    string EmployeeName,
    string Position,
    short DaysWorked,
    decimal TotalEarnings,
    decimal TotalDeductions,
    decimal NetPay,
    PayrollStatus Status);

public sealed record PayrollPeriodDetail(
    int Id,
    DateOnly StartDate,
    DateOnly EndDate,
    DateOnly PaymentDate,
    PayrollStatus Status,
    int Days,
    decimal TotalEarnings,
    decimal TotalDeductions,
    decimal TotalNet,
    IReadOnlyList<PayslipListItem> Payslips);

public sealed record PayslipLineDto(string Concept, string? Description, decimal Amount);

/// <summary>Pantalla 06.3 · Comprobante de nómina.</summary>
public sealed record PayslipDetail(
    int Id,
    string Code,
    PayrollStatus Status,
    int PeriodId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateOnly PaymentDate,
    string EmployeeName,
    DocumentType DocumentType,
    string DocumentNumber,
    string Position,
    string Department,
    decimal MonthlySalary,
    string Eps,
    string PensionFund,
    string BankName,
    string BankAccountMasked,
    short DaysWorked,
    IReadOnlyList<PayslipLineDto> Earnings,
    IReadOnlyList<PayslipLineDto> Deductions,
    decimal TotalEarnings,
    decimal TotalDeductions,
    decimal NetPay,
    DateTime? PaidAt);

public sealed class PayrollPeriodCreateRequest
{
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public DateOnly PaymentDate { get; init; }
}
