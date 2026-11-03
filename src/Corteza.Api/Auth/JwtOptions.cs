using System.ComponentModel.DataAnnotations;

namespace Corteza.Api.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required] public string Issuer { get; init; } = string.Empty;
    [Required] public string Audience { get; init; } = string.Empty;

    /// <summary>Clave HMAC de al menos 32 caracteres. En producción va en variables de entorno o Key Vault.</summary>
    [Required, MinLength(32, ErrorMessage = "Jwt:Key debe tener al menos 32 caracteres.")]
    public string Key { get; init; } = string.Empty;

    [Range(5, 1440)] public int ExpiresMinutes { get; init; } = 480; // un turno
}
