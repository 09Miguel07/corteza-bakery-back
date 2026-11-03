using Corteza.Domain.Common;

namespace Corteza.Domain.Exceptions;

/// <summary>
/// Regla de negocio violada. La API la traduce a 422 con el mensaje tal cual,
/// por eso los mensajes van en español y dicen qué corregir.
/// </summary>
public abstract class DomainException(string message) : Exception(message);

public sealed class InvalidSupplierException(string message) : DomainException(message);

public sealed class SupplierHasActiveOrdersException()
    : DomainException("No se puede desactivar: el proveedor tiene órdenes activas.");

public sealed class InactiveSupplierException(string supplierName)
    : DomainException($"El proveedor {supplierName} está inactivo.");

public sealed class BudgetExceededException(decimal requested, decimal available)
    : DomainException($"Presupuesto insuficiente: la orden vale {Formats.Cop(requested)} y el proveedor tiene {Formats.Cop(available)} disponibles.")
{
    public decimal Requested { get; } = requested;
    public decimal Available { get; } = available;
}

public sealed class InvalidAmountException(string message = "Monto inválido: debe ser mayor que cero.")
    : DomainException(message);

public sealed class InvalidQuantityException(string message = "La cantidad debe ser mayor que cero.")
    : DomainException(message);
