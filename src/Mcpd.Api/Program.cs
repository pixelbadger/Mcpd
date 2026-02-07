using System.Text.Json;
using System.Threading.RateLimiting;
using FastEndpoints;
using Mcpd.Application.Commands;
using Mcpd.Application.Interfaces;
using Mcpd.Application.Queries;
using Mcpd.Domain.Interfaces;
using Mcpd.Infrastructure.Configuration;
using Mcpd.Infrastructure.Persistence;
using Mcpd.Infrastructure.Persistence.Repositories;
using Mcpd.Infrastructure.Seeding;
using Mcpd.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<McpdOptions>(builder.Configuration.GetSection(McpdOptions.SectionName));
builder.Services.Configure<RateLimitingOptions>(builder.Configuration.GetSection(RateLimitingOptions.SectionName));

// EF Core InMemory
builder.Services.AddDbContext<McpdDbContext>(options =>
    options.UseInMemoryDatabase("McpdDb"));

// Repositories
builder.Services.AddScoped<IClientRegistrationRepository, ClientRegistrationRepository>();
builder.Services.AddScoped<IMcpServerRepository, McpServerRepository>();
builder.Services.AddScoped<IClientServerGrantRepository, ClientServerGrantRepository>();
builder.Services.AddScoped<ICallbackWhitelistRepository, CallbackWhitelistRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();

// Services
builder.Services.AddSingleton<ISecretHasher, Argon2SecretHasher>();
builder.Services.AddSingleton<ITokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<ICallbackValidator, CallbackValidator>();

// Command handlers
builder.Services.AddScoped<RegisterClientCommandHandler>();
builder.Services.AddScoped<UpdateClientCommandHandler>();
builder.Services.AddScoped<RotateClientSecretCommandHandler>();
builder.Services.AddScoped<RevokeClientCommandHandler>();
builder.Services.AddScoped<GrantServerAccessCommandHandler>();
builder.Services.AddScoped<RevokeServerAccessCommandHandler>();

// Query handlers
builder.Services.AddScoped<GetClientRegistrationQueryHandler>();
builder.Services.AddScoped<ValidateTokenRequestQueryHandler>();
builder.Services.AddScoped<ListMcpServersQueryHandler>();
builder.Services.AddScoped<ListServerClientsQueryHandler>();

// Seeder
builder.Services.AddScoped<DatabaseSeeder>();

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

// Seed database
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    var serverConfigs = builder.Configuration.GetSection("McpServers").Get<McpServerConfig[]>() ?? [];
    await seeder.SeedAsync(serverConfigs);
}

// Middleware pipeline
app.UseRateLimiter();

app.UseFastEndpoints(c =>
{
    c.Endpoints.RoutePrefix = "oauth";
    c.Serializer.Options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    c.Errors.UseProblemDetails();
});

app.Run();

// Make Program accessible for WebApplicationFactory in tests
public partial class Program;
