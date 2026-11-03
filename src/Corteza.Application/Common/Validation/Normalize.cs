using System.Text.RegularExpressions;

namespace Corteza.Application.Common.Validation;

public static partial class Normalize
{
    /// <summary>Cédula: solo dígitos. "1.020.456.789" → "1020456789".</summary>
    public static string Document(string value) => NonDigits().Replace(value ?? string.Empty, string.Empty);

    /// <summary>NIT: sin puntos ni espacios, conserva el guion del dígito de verificación. "900.123.456-7" → "900123456-7".</summary>
    public static string TaxId(string value) => DotsAndSpaces().Replace(value ?? string.Empty, string.Empty);

    /// <summary>Teléfono: solo dígitos y el + inicial. "+57 312 458 9012" → "+573124589012".</summary>
    public static string Phone(string value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        var digits = NonDigits().Replace(trimmed, string.Empty);
        return trimmed.StartsWith('+') ? "+" + digits : digits;
    }

    public static string Text(string value) => MultipleSpaces().Replace((value ?? string.Empty).Trim(), " ");

    public static string? OptionalText(string? value) => string.IsNullOrWhiteSpace(value) ? null : Text(value);

    public static string? Email(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigits();

    [GeneratedRegex(@"[.\s]")]
    private static partial Regex DotsAndSpaces();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex MultipleSpaces();
}
