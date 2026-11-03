using System.ComponentModel.DataAnnotations;

namespace Corteza.Application.Features.Auth;

public sealed class LoginRequest
{
    [Required(ErrorMessage = "Escribe tu usuario o correo.")]
    [MaxLength(120)]
    public string Username { get; init; } = string.Empty;

    [Required(ErrorMessage = "Escribe tu contraseña.")]
    [MaxLength(200)]
    public string Password { get; init; } = string.Empty;
}

public sealed record AuthUserDto(int Id, string Username, string FullName, string Role, string RoleName);

public sealed record LoginResponse(string Token, DateTime ExpiresAt, AuthUserDto User);
