using FastEndpoints;
using Mcpd.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Mcpd.Api.PreProcessors;

public sealed class AdminApiKeyPreProcessor<TRequest> : IPreProcessor<TRequest> where TRequest : notnull
{
    public async Task PreProcessAsync(IPreProcessorContext<TRequest> ctx, CancellationToken ct)
    {
        if (ctx.HttpContext.ResponseStarted()) return;

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
