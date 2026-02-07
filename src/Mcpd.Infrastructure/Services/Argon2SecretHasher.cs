using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Mcpd.Application.Interfaces;
using Mcpd.Domain.ValueObjects;

namespace Mcpd.Infrastructure.Services;

public sealed class Argon2SecretHasher : ISecretHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int DegreeOfParallelism = 1;
    private const int MemorySize = 65536; // 64 MB
    private const int Iterations = 3;

    public HashedSecret Hash(string plaintext)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = ComputeHash(plaintext, salt);

        // Format: $argon2id$salt$hash (all base64)
        var encoded = $"$argon2id${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        return new HashedSecret(encoded);
    }

    public bool Verify(string plaintext, HashedSecret hash)
    {
        var parts = hash.Value.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 || parts[0] != "argon2id")
            return false;

        var salt = Convert.FromBase64String(parts[1]);
        var storedHash = Convert.FromBase64String(parts[2]);
        var computedHash = ComputeHash(plaintext, salt);

        return CryptographicOperations.FixedTimeEquals(storedHash, computedHash);
    }

    private static byte[] ComputeHash(string plaintext, byte[] salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(plaintext));
        argon2.Salt = salt;
        argon2.DegreeOfParallelism = DegreeOfParallelism;
        argon2.MemorySize = MemorySize;
        argon2.Iterations = Iterations;
        return argon2.GetBytes(HashSize);
    }
}
