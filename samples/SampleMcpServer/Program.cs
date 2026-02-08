using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var mcpdIssuer = builder.Configuration["Mcpd:Issuer"] ?? "https://dcr.contoso.com";
var resourceName = builder.Configuration["Resource:Name"] ?? "code-assist";
var resourceUri = builder.Configuration["Resource:Uri"] ?? "http://localhost:5100";

// JWKS configuration retrieval from Mcpd
var jwksUri = $"{mcpdIssuer.TrimEnd('/')}/.well-known/jwks.json";
var configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
    $"{mcpdIssuer.TrimEnd('/')}/.well-known/openid-configuration",
    new OpenIdConnectConfigurationRetriever(),
    new HttpDocumentRetriever { RequireHttps = mcpdIssuer.StartsWith("https") });

// Pre-load signing keys from JWKS endpoint directly
var httpClient = new HttpClient();

var app = builder.Build();

// Protected Resource Metadata (RFC 9728)
app.MapGet("/.well-known/oauth-protected-resource", () => Results.Json(new
{
    resource = resourceUri,
    authorization_servers = new[] { mcpdIssuer },
    scopes_supported = new[] { "read", "write", "admin" }
}));

// JWT validation middleware
async Task<(bool valid, JsonWebToken? token)> ValidateToken(HttpContext ctx)
{
    var authHeader = ctx.Request.Headers.Authorization.ToString();
    if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        ctx.Response.StatusCode = 401;
        ctx.Response.Headers["WWW-Authenticate"] = $"Bearer resource_metadata=\"{resourceUri}/.well-known/oauth-protected-resource\"";
        await ctx.Response.WriteAsJsonAsync(new { error = "missing_token", error_description = "Bearer token required." });
        return (false, null);
    }

    var tokenString = authHeader["Bearer ".Length..];

    try
    {
        // Fetch JWKS keys
        var jwksJson = await httpClient.GetStringAsync(jwksUri);
        var jwks = new JsonWebKeySet(jwksJson);

        var handler = new JsonWebTokenHandler();
        var result = await handler.ValidateTokenAsync(tokenString, new TokenValidationParameters
        {
            ValidIssuer = mcpdIssuer,
            ValidAudience = resourceName,
            IssuerSigningKeys = jwks.GetSigningKeys(),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        });

        if (!result.IsValid)
        {
            ctx.Response.StatusCode = 401;
            ctx.Response.Headers["WWW-Authenticate"] = $"Bearer error=\"invalid_token\", resource_metadata=\"{resourceUri}/.well-known/oauth-protected-resource\"";
            await ctx.Response.WriteAsJsonAsync(new { error = "invalid_token", error_description = result.Exception?.Message ?? "Token validation failed." });
            return (false, null);
        }

        return (true, result.SecurityToken as JsonWebToken);
    }
    catch (Exception ex)
    {
        ctx.Response.StatusCode = 401;
        ctx.Response.Headers["WWW-Authenticate"] = $"Bearer error=\"invalid_token\"";
        await ctx.Response.WriteAsJsonAsync(new { error = "invalid_token", error_description = ex.Message });
        return (false, null);
    }
}

// Helper to check scopes
bool HasScope(JsonWebToken token, string requiredScope)
{
    var scopeClaim = token.Claims.FirstOrDefault(c => c.Type == "scope")?.Value;
    if (scopeClaim is null) return false;
    var scopes = scopeClaim.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    return scopes.Contains(requiredScope);
}

// Sample MCP Tool: Echo
app.MapPost("/tools/echo", async (HttpContext ctx) =>
{
    var (valid, token) = await ValidateToken(ctx);
    if (!valid) return;

    if (!HasScope(token!, "read"))
    {
        ctx.Response.StatusCode = 403;
        await ctx.Response.WriteAsJsonAsync(new { error = "insufficient_scope", error_description = "Requires 'read' scope." });
        return;
    }

    var body = await ctx.Request.ReadFromJsonAsync<JsonElement>();
    var message = body.TryGetProperty("message", out var msg) ? msg.GetString() : "No message provided";

    await ctx.Response.WriteAsJsonAsync(new
    {
        tool = "echo",
        result = message,
        user = token!.Subject,
        timestamp = DateTimeOffset.UtcNow
    });
});

// Sample MCP Tool: Time
app.MapPost("/tools/time", async (HttpContext ctx) =>
{
    var (valid, token) = await ValidateToken(ctx);
    if (!valid) return;

    if (!HasScope(token!, "read"))
    {
        ctx.Response.StatusCode = 403;
        await ctx.Response.WriteAsJsonAsync(new { error = "insufficient_scope", error_description = "Requires 'read' scope." });
        return;
    }

    await ctx.Response.WriteAsJsonAsync(new
    {
        tool = "time",
        result = new
        {
            utc = DateTimeOffset.UtcNow.ToString("o"),
            unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        },
        user = token!.Subject
    });
});

app.Run();
