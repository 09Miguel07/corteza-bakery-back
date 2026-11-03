using Corteza.Application.Common.Interfaces;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Corteza.Api.Auth;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public int? UserId =>
        int.TryParse(accessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : null;

    public string? Role => accessor.HttpContext?.User.FindFirst(CortezaClaims.Role)?.Value;

    public int RequireUserId() =>
        UserId ?? throw new UnauthorizedAccessException("La petición no tiene un usuario autenticado.");
}
