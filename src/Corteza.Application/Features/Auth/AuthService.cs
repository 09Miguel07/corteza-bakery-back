using Corteza.Application.Common.Exceptions;
using Corteza.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Corteza.Application.Features.Auth;

public sealed class AuthService(IAppDbContext db, IPasswordHasherService hasher, ITokenService tokens)
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var login = request.Username.Trim().ToLowerInvariant();

        var user = await db.Users
            .Include(u => u.Role)
            .Include(u => u.Employee)
            .FirstOrDefaultAsync(u => u.Username == login || u.Email == login, ct);

        // Mismo mensaje si no existe, está inactivo o la clave falla: no revela qué usuarios existen.
        if (user is null || !user.IsActive || !hasher.Verify(user, request.Password))
            throw new InvalidCredentialsException();

        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var fullName = user.Employee?.FullName ?? user.Username;
        var (token, expiresAt) = tokens.CreateToken(user, fullName);
        return new LoginResponse(token, expiresAt, ToDto(user, fullName));
    }

    public async Task<AuthUserDto> GetCurrentAsync(int userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking()
            .Include(u => u.Role)
            .Include(u => u.Employee)
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, ct)
            ?? throw new InvalidCredentialsException();

        return ToDto(user, user.Employee?.FullName ?? user.Username);
    }

    private static AuthUserDto ToDto(Domain.Entities.User user, string fullName) =>
        new(user.Id, user.Username, fullName, user.Role.Code, user.Role.Name);
}
