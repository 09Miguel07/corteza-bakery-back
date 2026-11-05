using Corteza.Application.Common.Exceptions;
using Corteza.Application.Features.Payroll;
using Corteza.Domain.Entities;
using Corteza.Domain.Enums;

namespace Corteza.Application.Tests;

public class PayrollServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly PayrollService _service;

    public PayrollServiceTests() => _service = new PayrollService(_db.Context, _db.Time, PayrollSettings.Colombia2026);

    public void Dispose() => _db.Dispose();

    private static PayrollPeriodCreateRequest November() => new()
    {
        StartDate = new DateOnly(2026, 11, 1),
        EndDate = new DateOnly(2026, 11, 30),
        PaymentDate = new DateOnly(2026, 11, 30),
    };

    [Fact(DisplayName = "No se crean dos periodos que se cruzan")]
    public async Task Create_rejects_overlap()
    {
        await _service.CreateAsync(November(), default);

        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(new PayrollPeriodCreateRequest
        {
            StartDate = new DateOnly(2026, 11, 16),
            EndDate = new DateOnly(2026, 12, 15),
            PaymentDate = new DateOnly(2026, 12, 15),
        }, default));
    }

    [Fact(DisplayName = "Generar, aprobar y pagar la nómina del periodo")]
    public async Task Generate_approve_pay()
    {
        await _db.AddEmployeeAsync("Ana", 1_750_905);
        await _db.AddEmployeeAsync("Luis", 4_000_000);
        await _db.AddEmployeeAsync("Retirado", 2_000_000, active: false);
        var period = await _service.CreateAsync(November(), default);

        var generated = await _service.GenerateAsync(period.Id, default);
        Assert.Equal(2, generated.Payslips.Count);
        Assert.Equal(1_859_928 + 3_680_000, generated.TotalNet);

        await _service.ApproveAsync(period.Id, default);
        var paid = await _service.PayAsync(period.Id, default);

        Assert.Equal(PayrollStatus.Paid, paid.Status);
        Assert.All(paid.Payslips, p => Assert.Equal(PayrollStatus.Paid, p.Status));
    }

    [Fact(DisplayName = "El comprobante muestra devengados, deducciones y la cuenta enmascarada")]
    public async Task Payslip_detail()
    {
        await _db.AddEmployeeAsync("Ana", 1_750_905);
        var period = await _service.CreateAsync(November(), default);
        var generated = await _service.GenerateAsync(period.Id, default);

        var slip = await _service.GetPayslipAsync(generated.Payslips.Single().Id, default);

        Assert.Equal(2, slip.Earnings.Count);
        Assert.Equal(2, slip.Deductions.Count);
        Assert.Equal(1_859_928, slip.NetPay);
        Assert.Equal("•••• 8901", slip.BankAccountMasked);
    }
}
