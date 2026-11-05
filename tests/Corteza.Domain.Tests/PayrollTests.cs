using Corteza.Domain.Entities;
using Corteza.Domain.Enums;
using Corteza.Domain.Exceptions;

namespace Corteza.Domain.Tests;

/// <summary>
/// Módulo 06 · Nómina (06.0 y 06.3). Alcance acordado en el Sprint 1 (HU-RH-04): salario proporcional a los días,
/// auxilio de transporte hasta 2 SMMLV y deducciones básicas de salud (4 %) y pensión (4 %).
/// </summary>
public class PayrollTests
{
    private static readonly PayrollSettings Settings = PayrollSettings.Colombia2026;

    private static Employee Employee(int id, decimal salary, DateOnly? hired = null, bool active = true) => new()
    {
        Id = id, FirstNames = "Ana", LastNames = "Torres", MonthlySalary = salary, IsActive = active,
        HireDate = hired ?? new DateOnly(2024, 1, 15),
    };

    private static PayrollPeriod November() =>
        PayrollPeriod.Create(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), new DateOnly(2026, 11, 30));

    [Fact(DisplayName = "Un periodo nuevo queda en borrador")]
    public void Create_starts_draft()
    {
        var period = November();

        Assert.Equal(PayrollStatus.Draft, period.Status);
        Assert.Equal(30, period.Days);
    }

    [Fact(DisplayName = "Las fechas del periodo deben tener sentido")]
    public void Create_validates_dates()
    {
        Assert.Throws<InvalidPayrollException>(() =>
            PayrollPeriod.Create(new DateOnly(2026, 11, 30), new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30)));
        Assert.Throws<InvalidPayrollException>(() =>
            PayrollPeriod.Create(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), new DateOnly(2026, 11, 29)));
        Assert.Throws<InvalidPayrollException>(() =>
            PayrollPeriod.Create(new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 15), new DateOnly(2026, 12, 15)));
    }

    [Fact(DisplayName = "Un mes de 31 días se liquida como 30")]
    public void Days_are_capped_at_thirty()
    {
        var october = PayrollPeriod.Create(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), new DateOnly(2026, 10, 31));
        Assert.Equal(30, october.Days);
    }

    [Fact(DisplayName = "Salario mínimo: básico + auxilio de transporte − salud − pensión")]
    public void Generate_minimum_wage_payslip()
    {
        var period = November();

        period.Generate([Employee(1, 1_750_905)], Settings);

        var slip = period.Payslips.Single();
        Assert.Equal(30, slip.DaysWorked);
        Assert.Equal(2_000_000, slip.TotalEarnings);   // 1.750.905 + 249.095
        Assert.Equal(140_072, slip.TotalDeductions);   // 70.036 + 70.036
        Assert.Equal(1_859_928, slip.NetPay);
        Assert.Equal(4, slip.Lines.Count);
    }

    [Fact(DisplayName = "Quien gana más de 2 salarios mínimos no recibe auxilio de transporte")]
    public void Generate_without_transport_allowance()
    {
        var period = November();

        period.Generate([Employee(1, 4_000_000)], Settings);

        var slip = period.Payslips.Single();
        Assert.Equal(4_000_000, slip.TotalEarnings);
        Assert.DoesNotContain(slip.Lines, l => l.Concept == "Auxilio de transporte");
        Assert.Equal(320_000, slip.TotalDeductions);
    }

    [Fact(DisplayName = "Quien entró a mitad de periodo cobra solo los días trabajados")]
    public void Generate_prorates_new_hires()
    {
        var period = November();

        period.Generate([Employee(1, 3_000_000, hired: new DateOnly(2026, 11, 16))], Settings);

        var slip = period.Payslips.Single();
        Assert.Equal(15, slip.DaysWorked);
        Assert.Equal(1_500_000 + 124_548, slip.TotalEarnings); // auxilio: 249.095 / 30 × 15 = 124.547,5 → 124.548
    }

    [Fact(DisplayName = "Solo entran empleados activos contratados antes del fin del periodo")]
    public void Generate_skips_inactive_and_future_hires()
    {
        var period = November();

        period.Generate([
            Employee(1, 2_000_000),
            Employee(2, 2_000_000, active: false),
            Employee(3, 2_000_000, hired: new DateOnly(2026, 12, 1)),
        ], Settings);

        Assert.Single(period.Payslips);
        Assert.Equal(1, period.Payslips.Single().EmployeeId);
    }

    [Fact(DisplayName = "Sin empleados activos no hay nómina que generar")]
    public void Generate_requires_employees()
    {
        Assert.Throws<InvalidPayrollException>(() => November().Generate([], Settings));
    }

    [Fact(DisplayName = "Generar otra vez en borrador reemplaza los comprobantes")]
    public void Generate_again_replaces()
    {
        var period = November();
        period.Generate([Employee(1, 2_000_000)], Settings);

        period.Generate([Employee(1, 2_000_000), Employee(2, 3_000_000)], Settings);

        Assert.Equal(2, period.Payslips.Count);
    }

    [Fact(DisplayName = "Aprobar y pagar: borrador → aprobada → pagada, también en cada comprobante")]
    public void Approve_then_pay()
    {
        var period = November();
        period.Generate([Employee(1, 2_000_000)], Settings);

        period.Approve();
        Assert.Equal(PayrollStatus.Approved, period.Status);
        Assert.Throws<InvalidPayrollException>(() => period.Generate([Employee(1, 2_000_000)], Settings));

        period.MarkPaid(TestData.Now);
        Assert.Equal(PayrollStatus.Paid, period.Status);
        Assert.All(period.Payslips, p => Assert.Equal(PayrollStatus.Paid, p.Status));
        Assert.All(period.Payslips, p => Assert.Equal(TestData.Now, p.PaidAt));
    }

    [Fact(DisplayName = "No se aprueba sin comprobantes ni se paga sin aprobar")]
    public void Approve_and_pay_guards()
    {
        var period = November();
        Assert.Throws<InvalidPayrollException>(() => period.Approve());

        period.Generate([Employee(1, 2_000_000)], Settings);
        var error = Assert.Throws<InvalidPayrollException>(() => period.MarkPaid(TestData.Now));
        Assert.Equal("Primero aprueba la nómina.", error.Message);
    }
}
