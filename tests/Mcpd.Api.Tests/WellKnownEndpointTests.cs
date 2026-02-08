using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Mcpd.Api.Tests;

public sealed class WellKnownEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public WellKnownEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Jwks_ReturnsValidJwk()
    {
        var response = await _client.GetAsync("/.well-known/jwks.json");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var keys = body!.RootElement.GetProperty("keys");
        keys.GetArrayLength().Should().Be(1);

        var key = keys[0];
        key.GetProperty("kty").GetString().Should().Be("RSA");
        key.GetProperty("alg").GetString().Should().Be("RS256");
        key.GetProperty("use").GetString().Should().Be("sig");
        key.GetProperty("kid").GetString().Should().NotBeNullOrWhiteSpace();
        key.GetProperty("n").GetString().Should().NotBeNullOrWhiteSpace();
        key.GetProperty("e").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AsMetadata_ReturnsRequiredFields()
    {
        var response = await _client.GetAsync("/.well-known/oauth-authorization-server");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var root = body!.RootElement;

        root.GetProperty("issuer").GetString().Should().NotBeNullOrWhiteSpace();
        root.GetProperty("token_endpoint").GetString().Should().Contain("/oauth/token");
        root.GetProperty("registration_endpoint").GetString().Should().Contain("/oauth/register");
        root.GetProperty("jwks_uri").GetString().Should().Contain("/.well-known/jwks.json");
        root.GetProperty("grant_types_supported").GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
        root.GetProperty("token_endpoint_auth_methods_supported").GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task OpenIdConfiguration_ReturnsRequiredFields()
    {
        var response = await _client.GetAsync("/.well-known/openid-configuration");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var root = body!.RootElement;

        root.GetProperty("issuer").GetString().Should().NotBeNullOrWhiteSpace();
        root.GetProperty("jwks_uri").GetString().Should().Contain("/.well-known/jwks.json");
    }

    [Fact]
    public async Task Jwks_KeyCanValidateAccessTokens()
    {
        // Register a client and get a token
        var regPayload = new
        {
            client_name = "JWKS Validation Test Client",
            redirect_uris = new[] { "https://app.contoso.com/oauth/callback" },
            grant_types = new[] { "client_credentials" },
            token_endpoint_auth_method = "client_secret_post",
            scope = new[] { "read" }
        };

        var regResponse = await _client.PostAsJsonAsync("/oauth/register", regPayload, JsonOptions);
        regResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var regBody = await regResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var clientId = regBody!.RootElement.GetProperty("client_id").GetString()!;
        var clientSecret = regBody.RootElement.GetProperty("client_secret").GetString()!;

        var tokenPayload = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["resource"] = "code-assist",
            ["scope"] = "read"
        });

        var tokenResponse = await _client.PostAsync("/oauth/token", tokenPayload);
        tokenResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var tokenBody = await tokenResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var accessToken = tokenBody!.RootElement.GetProperty("access_token").GetString()!;

        // Fetch JWKS and validate the token with the published key
        var jwksResponse = await _client.GetAsync("/.well-known/jwks.json");
        var jwksJson = await jwksResponse.Content.ReadAsStringAsync();
        var jwks = new JsonWebKeySet(jwksJson);

        var handler = new JsonWebTokenHandler();
        var result = await handler.ValidateTokenAsync(accessToken, new TokenValidationParameters
        {
            ValidIssuer = "https://dcr.contoso.com",
            ValidAudience = "code-assist",
            IssuerSigningKeys = jwks.GetSigningKeys(),
            ValidateLifetime = true
        });

        result.IsValid.Should().BeTrue();
        result.Claims["sub"].Should().Be(clientId);
    }
}
