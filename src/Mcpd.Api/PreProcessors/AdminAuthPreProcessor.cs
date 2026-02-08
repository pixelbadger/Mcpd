using FastEndpoints;
using Mcpd.Application.Interfaces;
using Mcpd.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Mcpd.Api.PreProcessors;

public sealed class AdminAuthPreProcessor<TRequest> : IPreProcessor<TRequest> where TRequest : notnull
{
    public async Task PreProcessAsync(IPreProcessorContext<TRequest> ctx, CancellationToken ct)
    {
        if (ctx.HttpContext.ResponseStarted()) return;

        // Try Bearer token auth first
        var authHeader = ctx.HttpContext.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authHeader) &&
            authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader["Bearer ".Length..].Trim();
            if (!string.IsNullOrWhiteSpace(token))
            {
                var validator = ctx.HttpContext.RequestServices.GetRequiredService<IUserTokenValidator>();
                var authService = ctx.HttpContext.RequestServices.GetRequiredService<IUserServerAuthorizationService>();

                var result = await validator.ValidateAsync(token, ct);
                if (result.IsValid && authService.IsAdmin(result.Claims))
                {
                    ctx.HttpContext.Items["AdminSubject"] = result.Subject;
                    ctx.HttpContext.Items["AdminUsername"] = result.PreferredUsername;
                    return;
                }
            }
        }

        // Fall back to API key auth
        var options = ctx.HttpContext.RequestServices.GetRequiredService<IOptions<McpdOptions>>();
        var expectedKey = options.Value.AdminApiKey;

        if (string.IsNullOrWhiteSpace(expectedKey))
        {
            await ctx.HttpContext.Response.SendUnauthorizedAsync(ct);
            return;
        }

        var apiKey = ctx.HttpContext.Request.Headers["X-Admin-Key"].ToString();
        if (!string.Equals(apiKey, expectedKey, StringComparison.Ordinal))
        {
            await ctx.HttpContext.Response.SendUnauthorizedAsync(ct);
        }
    }
}
