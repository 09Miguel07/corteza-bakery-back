using Corteza.Domain.Entities;

namespace Corteza.Application.Common.Interfaces;

/// <summary>Usuario autenticado de la petición actual (lo implementa la API a partir del JWT).</summary>
public interface ICurrentUser
{
    int? UserId { get; }
    string? Role { get; }

    /// <summary>Id del usuario o excepción si la petición no está autenticada.</summary>
    int RequireUserId();
}

public interface IPasswordHasherService
{
    string Hash(User user, string password);
    bool Verify(User user, string password);
}

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(User user, string fullName);
}
