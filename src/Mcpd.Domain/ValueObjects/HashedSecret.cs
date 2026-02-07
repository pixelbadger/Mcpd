namespace Mcpd.Domain.ValueObjects;

public sealed record HashedSecret(string Value)
{
    public override string ToString() => "***HASHED***";
}
