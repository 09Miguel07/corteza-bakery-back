using Corteza.Domain.Enums;
using Corteza.Domain.Exceptions;

namespace Corteza.Domain.Entities;

public class Employee
{
    public int Id { get; set; }

    // 1. Datos personales
    public string FirstNames { get; set; } = null!;
    public string LastNames { get; set; } = null!;
    public DocumentType DocumentType { get; set; } = DocumentType.CC;
    public string DocumentNumber { get; set; } = null!;
    public DateOnly BirthDate { get; set; }
    public string Phone { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Address { get; set; } = null!;
    public string EmergencyContact { get; set; } = null!;

    // 2. Datos del contrato
    public string Position { get; set; } = null!;
    public string Department { get; set; } = null!;
    public string WorkSite { get; set; } = null!;
    public ContractType ContractType { get; set; }
    public DateOnly HireDate { get; set; }
    public decimal MonthlySalary { get; set; }
    public WorkShift Shift { get; set; }

    // 3. Seguridad social
    public string Eps { get; set; } = null!;
    public string PensionFund { get; set; } = null!;
    public string Arl { get; set; } = null!;
    public string CompensationFund { get; set; } = null!;

    // 4. Pago de nómina (dato sensible: nunca se devuelve completo ni se registra en logs)
    public string BankName { get; set; } = null!;
    public BankAccountType BankAccountType { get; set; }
    public string BankAccountNumber { get; set; } = null!;
    public PayFrequency PayFrequency { get; set; } = PayFrequency.Biweekly;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public User? User { get; set; }
    public ICollection<Payslip> Payslips { get; set; } = [];

    public string FullName => $"{FirstNames} {LastNames}";
}

/// <summary>Valores legales de la nómina. Por defecto, Colombia 2026 (decretos 1469 y 1470 de 2025).</summary>
public sealed record PayrollSettings(decimal MinimumWage, decimal TransportAllowance, decimal HealthRate = 0.04m, decimal PensionRate = 0.04m)
{
    public static PayrollSettings Colombia2026 => new(1_750_905m, 249_095m);
}

/// <summary>
/// Periodo de nómina (pantalla 06.0). Borrador → Aprobada → Pagada. Solo en borrador se (re)generan los comprobantes.
/// Alcance HU-RH-04: salario proporcional, auxilio de transporte y deducciones de salud y pensión.
/// </summary>
public class PayrollPeriod
{
    public int Id { get; set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public DateOnly PaymentDate { get; private set; }
    public PayrollStatus Status { get; private set; } = PayrollStatus.Draft;
    public ICollection<Payslip> Payslips { get; set; } = [];

    private PayrollPeriod() { }

    /// <summary>Días a liquidar: mes comercial de 30 días.</summary>
    public int Days => Math.Min(30, EndDate.DayNumber - StartDate.DayNumber + 1);

    public static PayrollPeriod Create(DateOnly start, DateOnly end, DateOnly paymentDate)
    {
        if (end < start) throw new InvalidPayrollException("La fecha final no puede ser anterior a la inicial.");
        if (end.DayNumber - start.DayNumber + 1 > 31) throw new InvalidPayrollException("Un periodo de nómina no puede pasar de un mes.");
        if (paymentDate < end) throw new InvalidPayrollException("La fecha de pago no puede ser anterior al fin del periodo.");
        return new PayrollPeriod { StartDate = start, EndDate = end, PaymentDate = paymentDate };
    }

    public void Generate(IEnumerable<Employee> employees, PayrollSettings settings)
    {
        if (Status != PayrollStatus.Draft) throw new InvalidPayrollException("Solo se genera la nómina de un periodo en borrador.");

        var eligible = employees.Where(e => e.IsActive && e.HireDate <= EndDate).ToList();
        if (eligible.Count == 0) throw new InvalidPayrollException("No hay empleados activos para este periodo.");

        Payslips.Clear();
        foreach (var employee in eligible) Payslips.Add(Payslip.Calculate(this, employee, settings));
    }

    public void Approve()
    {
        if (Status != PayrollStatus.Draft) throw new InvalidPayrollException("La nómina ya fue aprobada.");
        if (Payslips.Count == 0) throw new InvalidPayrollException("Genera los comprobantes antes de aprobar.");
        Status = PayrollStatus.Approved;
        foreach (var slip in Payslips) slip.SetStatus(PayrollStatus.Approved, null);
    }

    public void MarkPaid(DateTime now)
    {
        if (Status != PayrollStatus.Approved) throw new InvalidPayrollException("Primero aprueba la nómina.");
        Status = PayrollStatus.Paid;
        foreach (var slip in Payslips) slip.SetStatus(PayrollStatus.Paid, now);
    }
}

/// <summary>Comprobante de nómina (pantalla 06.3, NOM-000001).</summary>
public class Payslip
{
    public int Id { get; set; }
    public string Code { get; set; } = null!; // NOM-000001, lo genera una secuencia
    public int PeriodId { get; set; }
    public PayrollPeriod Period { get; set; } = null!;
    public int EmployeeId { get; private set; }
    public Employee Employee { get; set; } = null!;
    public short DaysWorked { get; private set; }
    public decimal TotalEarnings { get; private set; }
    public decimal TotalDeductions { get; private set; }
    public decimal NetPay { get; private set; } // columna calculada en SQL Server; el dominio la llena igual
    public PayrollStatus Status { get; private set; } = PayrollStatus.Draft;
    public DateTime? PaidAt { get; private set; }
    public ICollection<PayslipLine> Lines { get; set; } = [];

    private Payslip() { }

    private static decimal Money(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero);

    internal static Payslip Calculate(PayrollPeriod period, Employee employee, PayrollSettings s)
    {
        var from = employee.HireDate > period.StartDate ? employee.HireDate : period.StartDate;
        var days = (short)Math.Min(period.Days, period.EndDate.DayNumber - from.DayNumber + 1);

        var basic = Money(employee.MonthlySalary / 30 * days);
        var transport = employee.MonthlySalary <= 2 * s.MinimumWage ? Money(s.TransportAllowance / 30 * days) : 0;
        var health = Money(basic * s.HealthRate);
        var pension = Money(basic * s.PensionRate);

        var slip = new Payslip { EmployeeId = employee.Id, DaysWorked = days };
        slip.Lines.Add(new PayslipLine { Type = PayslipLineType.Earning, Concept = "Salario básico", Description = $"{days} días", Amount = basic });
        if (transport > 0)
            slip.Lines.Add(new PayslipLine { Type = PayslipLineType.Earning, Concept = "Auxilio de transporte", Description = $"{days} días", Amount = transport });
        slip.Lines.Add(new PayslipLine { Type = PayslipLineType.Deduction, Concept = "Salud", Description = $"{s.HealthRate:P0} del básico", Amount = health });
        slip.Lines.Add(new PayslipLine { Type = PayslipLineType.Deduction, Concept = "Pensión", Description = $"{s.PensionRate:P0} del básico", Amount = pension });

        slip.TotalEarnings = basic + transport;
        slip.TotalDeductions = health + pension;
        slip.NetPay = slip.TotalEarnings - slip.TotalDeductions;
        return slip;
    }

    internal void SetStatus(PayrollStatus status, DateTime? paidAt)
    {
        Status = status;
        PaidAt = paidAt;
    }
}

public class PayslipLine
{
    public int Id { get; set; }
    public int PayslipId { get; set; }
    public PayslipLineType Type { get; set; }
    public string Concept { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
}
