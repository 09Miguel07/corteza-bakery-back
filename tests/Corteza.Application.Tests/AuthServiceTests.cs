using Corteza.Application.Common.Exceptions;
using Corteza.Application.Common.Interfaces;
using Corteza.Application.Features.Auth;
using Corteza.Domain.Entities;
using Moq;

namespace Corteza.Application.Tests;

/// <summary>El hasher y el emisor de tokens se simulan con Moq: se prueba solo la regla del login.</summary>
public class AuthServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly Mock<IPasswordHasherService> _hasher = new();
    private readonly Mock<ITokenService> _tokens = new();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _tokens.Setup(t => t.CreateToken(It.IsAny<User>(), It.IsAny<string>()))
            .Returns(("jwt", new DateTime(2026, 11, 2, 23, 0, 0, DateTimeKind.Utc)));
        _service = new AuthService(_db.Context, _hasher.Object, _tokens.Object);
    }

    public void Dispose() => _db.Dispose();

    [Fact(DisplayName = "Con la contraseña correcta devuelve el token y el rol")]
    public async Task Login_with_valid_password_returns_token()
    {
        _hasher.Setup(h => h.Verify(It.IsAny<User>(), "Corteza2026*")).Returns(true);

        var response = await _service.LoginAsync(new LoginRequest { Username = "DIEGO.ROJAS", Password = "Corteza2026*" }, default);

        Assert.Equal("jwt", response.Token);
        Assert.Equal("PURCHASES", response.User.Role);
        _tokens.Verify(t => t.CreateToken(It.IsAny<User>(), It.IsAny<string>()), Times.Once);
    }

    [Fact(DisplayName = "Con la contraseña incorrecta no emite token")]
    public async Task Login_with_wrong_password_never_creates_token()
    {
        _hasher.Setup(h => h.Verify(It.IsAny<User>(), It.IsAny<string>())).Returns(false);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _service.LoginAsync(new LoginRequest { Username = "diego.rojas", Password = "mala" }, default));

        _tokens.Verify(t => t.CreateToken(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName = "Un usuario desactivado no entra y ni siquiera se revisa su contraseña")]
    public async Task Login_inactive_user_skips_password_check()
    {
        _db.User.IsActive = false;
        await _db.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _service.LoginAsync(new LoginRequest { Username = "diego.rojas", Password = "Corteza2026*" }, default));

        _hasher.Verify(h => h.Verify(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
        _tokens.Verify(t => t.CreateToken(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }
}
