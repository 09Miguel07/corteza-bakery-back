using System.Globalization;

namespace Corteza.Domain.Common;

/// <summary>Formatos colombianos para los mensajes de negocio.</summary>
public static class Formats
{
    private static readonly CultureInfo EsCo = CultureInfo.GetCultureInfo("es-CO");

    /// <summary>1250000 → "$1.250.000"</summary>
    public static string Cop(decimal value) => "$" + value.ToString("N0", EsCo);

    /// <summary>12.5 → "12,5"</summary>
    public static string Qty(decimal value) => value.ToString("0.##", EsCo);
}
