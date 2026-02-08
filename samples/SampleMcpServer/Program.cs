using Microsoft.AspNetCore.Authentication.JwtBearer;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

var mcpdIssuer = builder.Configuration["Mcpd:Issuer"] ?? "https://dcr.contoso.com";
var resourceUri = builder.Configuration["Resource:Uri"] ?? "http://localhost:5100";

// Authentication: validate JWTs issued by the Mcpd daemon
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = mcpdIssuer;
        options.RequireHttpsMetadata = mcpdIssuer.StartsWith("https");
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidIssuer = mcpdIssuer,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });
builder.Services.AddAuthorization();

// MCP Server with HTTP transport and tools from this assembly
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

// Protected Resource Metadata (RFC 9728)
app.MapGet("/.well-known/oauth-protected-resource", () => Results.Json(new
{
    resource = resourceUri,
    authorization_servers = new[] { mcpdIssuer },
    scopes_supported = new[] { "read", "write", "admin" }
}));

app.UseAuthentication();
app.UseAuthorization();

app.MapMcp().RequireAuthorization();

app.Run();
