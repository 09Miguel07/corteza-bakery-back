using Corteza.Domain.Entities;
using Corteza.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Corteza.Infrastructure.Persistence.Configurations;

internal sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> b)
    {
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Employees_DocumentType", EnumCheck.In<DocumentType>(nameof(Employee.DocumentType)));
            t.HasCheckConstraint("CK_Employees_ContractType", EnumCheck.In<ContractType>(nameof(Employee.ContractType)));
            t.HasCheckConstraint("CK_Employees_Shift", EnumCheck.In<WorkShift>(nameof(Employee.Shift)));
            t.HasCheckConstraint("CK_Employees_BankAccountType", EnumCheck.In<BankAccountType>(nameof(Employee.BankAccountType)));
            t.HasCheckConstraint("CK_Employees_PayFrequency", EnumCheck.In<PayFrequency>(nameof(Employee.PayFrequency)));
            t.HasCheckConstraint("CK_Employees_MonthlySalary", "[MonthlySalary] > 0");
        });

        b.Property(e => e.FirstNames).HasMaxLength(80);
        b.Property(e => e.LastNames).HasMaxLength(80);
        b.Property(e => e.DocumentNumber).HasMaxLength(20);
        b.Property(e => e.Phone).HasMaxLength(25);
        b.Property(e => e.Email).HasMaxLength(120);
        b.Property(e => e.Position).HasMaxLength(60);
        b.Property(e => e.Department).HasMaxLength(60);
        b.Property(e => e.WorkSite).HasMaxLength(80);
        b.Property(e => e.Eps).HasMaxLength(60);
        b.Property(e => e.PensionFund).HasMaxLength(60);
        b.Property(e => e.Arl).HasMaxLength(80);
        b.Property(e => e.CompensationFund).HasMaxLength(60);
        b.Property(e => e.BankName).HasMaxLength(60);
        b.Property(e => e.BankAccountNumber).HasMaxLength(30);

        b.HasIndex(e => e.DocumentNumber).IsUnique();
        b.HasIndex(e => new { e.IsActive, e.Position });
    }
}

internal sealed class PayrollPeriodConfiguration : IEntityTypeConfiguration<PayrollPeriod>
{
    public void Configure(EntityTypeBuilder<PayrollPeriod> b)
    {
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_PayrollPeriods_Dates", "[EndDate] >= [StartDate]");
            t.HasCheckConstraint("CK_PayrollPeriods_Status", EnumCheck.In<PayrollStatus>(nameof(PayrollPeriod.Status)));
        });
        b.HasIndex(p => new { p.StartDate, p.EndDate }).IsUnique();
    }
}

internal sealed class PayslipConfiguration : IEntityTypeConfiguration<Payslip>
{
    public void Configure(EntityTypeBuilder<Payslip> b)
    {
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Payslips_DaysWorked", "[DaysWorked] BETWEEN 0 AND 31");
            t.HasCheckConstraint("CK_Payslips_Status", EnumCheck.In<PayrollStatus>(nameof(Payslip.Status)));
        });

        b.Property(p => p.Code)
            .HasMaxLength(20)
            .HasDefaultValueSql(Sequences.CodeDefault("NOM-", Sequences.Payslip, "000000"));
        b.HasIndex(p => p.Code).IsUnique();

        b.Property(p => p.NetPay).HasComputedColumnSql("[TotalEarnings] - [TotalDeductions]", stored: true);

        b.HasOne(p => p.Period).WithMany(pp => pp.Payslips).HasForeignKey(p => p.PeriodId);
        b.HasOne(p => p.Employee).WithMany(e => e.Payslips).HasForeignKey(p => p.EmployeeId);
        b.HasMany(p => p.Lines).WithOne().HasForeignKey(l => l.PayslipId);
        b.HasIndex(p => new { p.PeriodId, p.EmployeeId }).IsUnique();
    }
}

internal sealed class PayslipLineConfiguration : IEntityTypeConfiguration<PayslipLine>
{
    public void Configure(EntityTypeBuilder<PayslipLine> b)
    {
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_PayslipLines_Type", EnumCheck.In<PayslipLineType>(nameof(PayslipLine.Type)));
            t.HasCheckConstraint("CK_PayslipLines_Amount", "[Amount] >= 0");
        });
        b.Property(l => l.Concept).HasMaxLength(80);
    }
}
