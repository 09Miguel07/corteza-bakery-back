using System.ComponentModel.DataAnnotations;
using Corteza.Application.Common.Models;
using Corteza.Domain.Enums;

namespace Corteza.Application.Features.Employees;

public sealed class EmployeeQuery : PageQuery
{
    /// <summary>Busca por nombre, apellido o cédula.</summary>
    public string? Search { get; init; }
    public string? Position { get; init; }
    public WorkShift? Shift { get; init; }
    public bool? Active { get; init; }
}

public sealed record EmployeeListItem(
    int Id,
    string FirstNames,
    string LastNames,
    DocumentType DocumentType,
    string DocumentNumber,
    string Position,
    WorkShift Shift,
    bool IsActive,
    bool HasUser);

public sealed record EmployeeSummary(int Total, int Active, int Inactive, decimal ActivePercentage);

public sealed record EmployeeDetail(
    int Id,
    string FirstNames,
    string LastNames,
    DocumentType DocumentType,
    string DocumentNumber,
    DateOnly BirthDate,
    string Phone,
    string Email,
    string Address,
    string EmergencyContact,
    string Position,
    string Department,
    string WorkSite,
    ContractType ContractType,
    DateOnly HireDate,
    decimal MonthlySalary,
    WorkShift Shift,
    string Eps,
    string PensionFund,
    string Arl,
    string CompensationFund,
    string BankName,
    BankAccountType BankAccountType,
    string BankAccountMasked,
    PayFrequency PayFrequency,
    bool IsActive,
    string? Username,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>Formulario "Nuevo empleado" (pantalla 06.2). Todos los campos con * son obligatorios.</summary>
public class EmployeeUpdateRequest
{
    // 1. Datos personales
    [Required(ErrorMessage = "Los nombres son obligatorios.")]
    [StringLength(80)]
    public string FirstNames { get; init; } = string.Empty;

    [Required(ErrorMessage = "Los apellidos son obligatorios.")]
    [StringLength(80)]
    public string LastNames { get; init; } = string.Empty;

    [EnumDataType(typeof(DocumentType), ErrorMessage = "Tipo de documento no válido.")]
    public DocumentType DocumentType { get; init; } = DocumentType.CC;

    [Required(ErrorMessage = "El número de documento es obligatorio.")]
    [RegularExpression(@"^[\dA-Za-z.\s]{5,20}$", ErrorMessage = "El número de documento no es válido.")]
    public string DocumentNumber { get; init; } = string.Empty;

    public DateOnly BirthDate { get; init; }

    [Required(ErrorMessage = "El teléfono de contacto es obligatorio.")]
    [RegularExpression(@"^\+?[\d\s()-]{7,25}$", ErrorMessage = "Escribe un teléfono válido.")]
    public string Phone { get; init; } = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "Escribe un correo válido.")]
    [StringLength(120)]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "La dirección de residencia es obligatoria.")]
    [StringLength(200)]
    public string Address { get; init; } = string.Empty;

    [Required(ErrorMessage = "El contacto de emergencia es obligatorio.")]
    [StringLength(200)]
    public string EmergencyContact { get; init; } = string.Empty;

    // 2. Datos del contrato
    [Required(ErrorMessage = "El cargo es obligatorio.")]
    [StringLength(60)]
    public string Position { get; init; } = string.Empty;

    [Required(ErrorMessage = "El área es obligatoria.")]
    [StringLength(60)]
    public string Department { get; init; } = string.Empty;

    [Required(ErrorMessage = "La sede de trabajo es obligatoria.")]
    [StringLength(80)]
    public string WorkSite { get; init; } = string.Empty;

    [EnumDataType(typeof(ContractType))]
    public ContractType ContractType { get; init; }

    public DateOnly HireDate { get; init; }

    [Range(1, 1_000_000_000, ErrorMessage = "El salario debe ser mayor que cero.")]
    public decimal MonthlySalary { get; init; }

    [EnumDataType(typeof(WorkShift))]
    public WorkShift Shift { get; init; }

    // 3. Seguridad social
    [Required(ErrorMessage = "La EPS es obligatoria.")]
    [StringLength(60)]
    public string Eps { get; init; } = string.Empty;

    [Required(ErrorMessage = "El fondo de pensiones es obligatorio.")]
    [StringLength(60)]
    public string PensionFund { get; init; } = string.Empty;

    [Required(ErrorMessage = "La ARL es obligatoria.")]
    [StringLength(80)]
    public string Arl { get; init; } = string.Empty;

    [Required(ErrorMessage = "La caja de compensación es obligatoria.")]
    [StringLength(60)]
    public string CompensationFund { get; init; } = string.Empty;

    // 4. Pago de nómina
    [Required(ErrorMessage = "La entidad bancaria es obligatoria.")]
    [StringLength(60)]
    public string BankName { get; init; } = string.Empty;

    [EnumDataType(typeof(BankAccountType))]
    public BankAccountType BankAccountType { get; init; }

    /// <summary>En edición es opcional: si llega vacío se conserva el número guardado.</summary>
    [RegularExpression(@"^[\d\s-]{6,30}$", ErrorMessage = "El número de cuenta solo puede tener números y guiones.")]
    public string? BankAccountNumber { get; init; }

    [EnumDataType(typeof(PayFrequency))]
    public PayFrequency PayFrequency { get; init; } = PayFrequency.Biweekly;
}

public sealed class EmployeeCreateRequest : EmployeeUpdateRequest;
