namespace Mcpd.Infrastructure.Configuration;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public RateLimitPolicy Registration { get; set; } = new();
    public RateLimitPolicy Token { get; set; } = new();
}

public sealed class RateLimitPolicy
{
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
    public int PermitLimit { get; set; } = 10;
}
