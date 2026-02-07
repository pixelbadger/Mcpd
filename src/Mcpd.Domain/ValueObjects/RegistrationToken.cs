namespace Mcpd.Domain.ValueObjects;

public sealed record RegistrationToken(string Value)
{
    public override string ToString() => "***REDACTED***";
}
