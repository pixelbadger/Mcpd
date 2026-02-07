using FluentAssertions;
using Mcpd.Infrastructure.Services;
using Xunit;

namespace Mcpd.Infrastructure.Tests.Services;

public sealed class Argon2SecretHasherTests
{
    private readonly Argon2SecretHasher _hasher = new();

    [Fact]
    public void Hash_And_Verify_RoundTrip()
    {
        var secret = "my-super-secret-client-secret";
        var hash = _hasher.Hash(secret);

        hash.Value.Should().StartWith("$argon2id$");
        _hasher.Verify(secret, hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_WrongSecret_ReturnsFalse()
    {
        var hash = _hasher.Hash("correct-secret");
        _hasher.Verify("wrong-secret", hash).Should().BeFalse();
    }

    [Fact]
    public void Hash_ProducesDifferentResults_ForSameInput()
    {
        var hash1 = _hasher.Hash("same-secret");
        var hash2 = _hasher.Hash("same-secret");

        hash1.Value.Should().NotBe(hash2.Value, "salts should differ");
        _hasher.Verify("same-secret", hash1).Should().BeTrue();
        _hasher.Verify("same-secret", hash2).Should().BeTrue();
    }
}
