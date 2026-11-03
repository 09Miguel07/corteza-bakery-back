using System.Text;
using System.Text.Json.Serialization;
using Corteza.Api.Auth;
using Corteza.Api.Errors;
using Corteza.Application;
using Corteza.Application.Common.Interfaces;
using Corteza.Infrastructure;
using Corteza.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ---------- Capas ----------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ---------- Autenticación (JWT) y autorización por rol ----------
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Falta la sección Jwt en la configuración.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // conserva "sub" y "role" tal cual
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            NameClaimType = CortezaClaims.Name,
            RoleClaimType = CortezaClaims.Role,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });

builder.Services.AddCortezaAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddSingleton<ITokenService, TokenService>();

// ---------- API ----------
builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(o =>
    {
        // Errores de validación con título en español; el detalle por campo lo pinta el formulario de Angular.
        o.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(
            new ValidationProblemDetails(context.ModelState)
            {
                Title = "Hay datos por corregir.",
                Status = StatusCodes.Status400BadRequest,
            });
    });

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();

// Solo para despliegue: en desarrollo el front usa el proxy de Angular y no necesita CORS.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(); // http://localhost:5080/scalar
    await app.Services.InitialiseDatabaseAsync(); // migra y carga datos de demostración
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection(); // en desarrollo no: el proxy de Angular llama por http
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
