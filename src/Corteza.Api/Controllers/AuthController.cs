using Corteza.Application.Common.Interfaces;
using Corteza.Application.Features.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Corteza.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService auth, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Inicia sesión con usuario (o correo) y contraseña. Devuelve el JWT.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public Task<LoginResponse> Login(LoginRequest request, CancellationToken ct) => auth.LoginAsync(request, ct);

    /// <summary>Usuario de la sesión actual. Falla con 401 si fue desactivado.</summary>
    [HttpGet("me")]
    [Authorize]
    public Task<AuthUserDto> Me(CancellationToken ct) => auth.GetCurrentAsync(currentUser.RequireUserId(), ct);
}
