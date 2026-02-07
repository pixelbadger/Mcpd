using FastEndpoints;
using Mcpd.Application.Interfaces;
using Mcpd.Domain.Enums;
using Mcpd.Domain.Interfaces;
using Mcpd.Domain.ValueObjects;

namespace Mcpd.Api.PreProcessors;

public sealed class RegistrationAccessTokenPreProcessor<TRequest> : IPreProcessor<TRequest> where TRequest : notnull
{
    public async Task PreProcessAsync(IPreProcessorContext<TRequest> ctx, CancellationToken ct)
    {
        if (ctx.HttpContext.ResponseStarted()) return;

        var authHeader = ctx.HttpContext.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            await ctx.HttpContext.Response.SendUnauthorizedAsync(ct);
            return;
        }

        var token = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            await ctx.HttpContext.Response.SendUnauthorizedAsync(ct);
            return;
        }

        // Get the clientId from the route
        var clientId = ctx.HttpContext.Request.RouteValues["clientId"]?.ToString();
        if (string.IsNullOrWhiteSpace(clientId))
        {
            await ctx.HttpContext.Response.SendUnauthorizedAsync(ct);
            return;
        }

        var clientRepo = ctx.HttpContext.RequestServices.GetRequiredService<IClientRegistrationRepository>();
        var secretHasher = ctx.HttpContext.RequestServices.GetRequiredService<ISecretHasher>();

        var registration = await clientRepo.GetByClientIdAsync(clientId, ct);
        if (registration?.RegistrationAccessToken is null || registration.Status != ClientStatus.Active)
        {
            await ctx.HttpContext.Response.SendUnauthorizedAsync(ct);
            return;
        }

        if (!secretHasher.Verify(token, new HashedSecret(registration.RegistrationAccessToken)))
        {
            await ctx.HttpContext.Response.SendUnauthorizedAsync(ct);
        }
    }
}
