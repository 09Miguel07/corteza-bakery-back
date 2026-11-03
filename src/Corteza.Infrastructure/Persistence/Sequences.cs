using Microsoft.EntityFrameworkCore;

namespace Corteza.Infrastructure.Persistence;

/// <summary>
/// Secuencias de SQL Server para los códigos de negocio (P-0152, FV-0231, OC-1042, CLI-042, NOM-000001).
/// El código se arma en un DEFAULT de la columna, separado del Id técnico.
/// </summary>
internal static class Sequences
{
    public const string Customer = "CustomerCodeSeq";
    public const string Order = "OrderCodeSeq";
    public const string SalesInvoice = "SalesInvoiceSeq";
    public const string PurchaseOrder = "PurchaseOrderSeq";
    public const string Payslip = "PayslipCodeSeq";

    public static void Register(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<int>(Customer).StartsAt(1);
        modelBuilder.HasSequence<int>(Order).StartsAt(1);
        modelBuilder.HasSequence<int>(SalesInvoice).StartsAt(1);
        modelBuilder.HasSequence<int>(PurchaseOrder).StartsAt(1000);
        modelBuilder.HasSequence<int>(Payslip).StartsAt(1);
    }

    /// <summary>SQL del DEFAULT: CONCAT('P-', FORMAT(NEXT VALUE FOR dbo.OrderCodeSeq, '0000')).</summary>
    public static string CodeDefault(string prefix, string sequence, string format) =>
        $"CONCAT('{prefix}', FORMAT(NEXT VALUE FOR dbo.{sequence}, '{format}'))";
}
