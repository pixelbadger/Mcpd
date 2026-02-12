using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Mcpd.Application.Components;

public sealed class SigningKeyManager : IDisposable
{
    private readonly RSA _rsa;
    public RsaSecurityKey SecurityKey { get; }
    public string KeyId { get; }

    public SigningKeyManager()
    {
        _rsa = RSA.Create(2048);
        KeyId = Guid.NewGuid().ToString("N")[..16];
        SecurityKey = new RsaSecurityKey(_rsa) { KeyId = KeyId };
    }

    public SigningKeyManager(string pemFilePath)
    {
        _rsa = RSA.Create();
        _rsa.ImportFromPem(File.ReadAllText(pemFilePath));
        KeyId = Guid.NewGuid().ToString("N")[..16];
        SecurityKey = new RsaSecurityKey(_rsa) { KeyId = KeyId };
    }

    public SigningCredentials GetSigningCredentials() =>
        new(SecurityKey, SecurityAlgorithms.RsaSha256);

    public JsonWebKey GetPublicJwk()
    {
        var parameters = _rsa.ExportParameters(includePrivateParameters: false);
        var jwk = new JsonWebKey
        {
            Kty = "RSA",
            Kid = KeyId,
            Alg = SecurityAlgorithms.RsaSha256,
            Use = "sig",
            N = Base64UrlEncoder.Encode(parameters.Modulus!),
            E = Base64UrlEncoder.Encode(parameters.Exponent!)
        };
        return jwk;
    }

    public void Dispose() => _rsa.Dispose();
}
