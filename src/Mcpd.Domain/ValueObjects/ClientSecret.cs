namespace Mcpd.Domain.ValueObjects;

public sealed record ClientSecret(string Value)
{
    public override string ToString() => "***REDACTED***";
}
