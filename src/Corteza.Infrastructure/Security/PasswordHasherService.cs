using Corteza.Application.Common.Interfaces;
using Corteza.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Corteza.Infrastructure.Security;

/// <summary>PBKDF2 con sal (PasswordHasher de ASP.NET Core Identity). Nunca se guarda la contraseña.</summary>
public sealed class PasswordHasherService : IPasswordHasherService
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(User user, string password) => _hasher.HashPassword(user, password);

    public bool Verify(User user, string password) =>
        _hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
}
