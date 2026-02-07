using Mcpd.Domain.ValueObjects;

namespace Mcpd.Application.Interfaces;

public interface ISecretHasher
{
    HashedSecret Hash(string plaintext);
    bool Verify(string plaintext, HashedSecret hash);
}
