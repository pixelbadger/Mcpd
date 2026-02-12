using System.Text.Json;
using System.Threading.RateLimiting;
using FastEndpoints;
using Mcpd.Api.Configuration;
using Mcpd.Application.Components;
using Mcpd.Application.Configuration;
using Mcpd.Domain.Interfaces;
using Mcpd.Infrastructure.Persistence;
using Mcpd.Infrastructure.Persistence.Repositories;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<McpdOptions>(builder.Configuration.GetSection(McpdOptions.SectionName));
builder.Services.Configure<RateLimitingOptions>(builder.Configuration.GetSection(RateLimitingOptions.SectionName));

// RSA Signing Key Manager
var mcpdConfig = builder.Configuration.GetSection(McpdOptions.SectionName).Get<McpdOptions>() ?? new();
var signingKeyManager = string.IsNullOrWhiteSpace(mcpdConfig.SigningKeyPath)
    ? new SigningKeyManager()
    : new SigningKeyManager(mcpdConfig.SigningKeyPath);
builder.Services.AddSingleton(signingKeyManager);

// EF Core InMemory
builder.Services.AddDbContext<McpdDbContext>(options =>
    options.UseInMemoryDatabase("McpdDb"));

// Repositories
builder.Services.AddScoped<IClientRegistrationRepository, ClientRegistrationRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();

// Services
builder.Services.AddSingleton<ISecretHasher, Argon2SecretHasher>();
builder.Services.AddSingleton<ITokenGenerator, JwtTokenGenerator>();

// Mediator (source-generated handler discovery)
builder.Services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;
});

// FastEndpoints
builder.Services.AddFastEndpoints();
builder.Services.AddAuthorization();

// Rate Limiting
var rateLimitConfig = builder.Configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>() ?? new();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("registration", _ => RateLimitPartition.GetFixedWindowLimiter(
        "registration",
        _ => new FixedWindowRateLimiterOptions
        {
            Window = rateLimitConfig.Registration.Window,
            PermitLimit = rateLimitConfig.Registration.PermitLimit
        }));
    options.AddPolicy("token", _ => RateLimitPartition.GetFixedWindowLimiter(
        "token",
        _ => new FixedWindowRateLimiterOptions
        {
            Window = rateLimitConfig.Token.Window,
            PermitLimit = rateLimitConfig.Token.PermitLimit
        }));
    options.RejectionStatusCode = 429;
});

var app = builder.Build();

// Middleware pipeline
app.UseRateLimiter();

app.UseFastEndpoints(c =>
{
    c.Endpoints.RoutePrefix = "oauth";
    c.Serializer.Options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    c.Errors.UseProblemDetails();
});

// Well-known endpoints (outside FastEndpoints route prefix)
app.MapGet("/.well-known/jwks.json", (SigningKeyManager skm) =>
{
    var jwk = skm.GetPublicJwk();
    return Results.Json(new
    {
        keys = new[]
        {
            new
            {
                kty = jwk.Kty,
                kid = jwk.Kid,
                alg = jwk.Alg,
                use = jwk.Use,
                n = jwk.N,
                e = jwk.E
            }
        }
    });
});

app.MapGet("/.well-known/oauth-authorization-server", (IOptions<McpdOptions> opts) =>
{
    var issuer = opts.Value.Issuer.TrimEnd('/');
    return Results.Json(new
    {
        issuer,
        token_endpoint = $"{issuer}/oauth/token",
        registration_endpoint = $"{issuer}/oauth/register",
        jwks_uri = $"{issuer}/.well-known/jwks.json",
        response_types_supported = Array.Empty<string>(),
        grant_types_supported = new[] { "client_credentials" },
        token_endpoint_auth_methods_supported = new[] { "client_secret_post", "client_secret_basic" },
        scopes_supported = new[] { "read", "write", "admin" }
    });
});

// OpenID Configuration (alias for JwtBearer middleware compatibility)
app.MapGet("/.well-known/openid-configuration", (IOptions<McpdOptions> opts) =>
{
    var issuer = opts.Value.Issuer.TrimEnd('/');
    return Results.Json(new
    {
        issuer,
        token_endpoint = $"{issuer}/oauth/token",
        registration_endpoint = $"{issuer}/oauth/register",
        jwks_uri = $"{issuer}/.well-known/jwks.json",
        response_types_supported = Array.Empty<string>(),
        grant_types_supported = new[] { "client_credentials" },
        token_endpoint_auth_methods_supported = new[] { "client_secret_post", "client_secret_basic" },
        scopes_supported = new[] { "read", "write", "admin" }
    });
});

app.Run();

// Make Program accessible for WebApplicationFactory in tests
public partial class Program;
