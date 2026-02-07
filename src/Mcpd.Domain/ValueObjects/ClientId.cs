namespace Mcpd.Domain.ValueObjects;

public sealed record ClientId(string Value)
{
    public override string ToString() => Value;
}
