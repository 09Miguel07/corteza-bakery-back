using Corteza.Domain.Enums;

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

public class PayrollPeriod
{
    public int Id { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateOnly PaymentDate { get; set; }
    public PayrollStatus Status { get; set; } = PayrollStatus.Draft;

    public ICollection<Payslip> Payslips { get; set; } = [];
}

public class Payslip
{
    public int Id { get; set; }
    public string Code { get; set; } = null!; // NOM-000001, lo genera una secuencia
    public int PeriodId { get; set; }
    public PayrollPeriod Period { get; set; } = null!;
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public short DaysWorked { get; set; }
    public decimal TotalEarnings { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; private set; } // columna calculada en SQL Server
    public PayrollStatus Status { get; set; } = PayrollStatus.Draft;
    public DateTime? PaidAt { get; set; }

    public ICollection<PayslipLine> Lines { get; set; } = [];
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
