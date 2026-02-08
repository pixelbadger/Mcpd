using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Mcpd.Application.Interfaces;
using Mcpd.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Mcpd.Api.Tests;

public sealed class TestUserTokenValidator : IUserTokenValidator
{
    public UserTokenValidationResult NextResult { get; set; }
        = new(false, null, null, [], "Not configured.");

    public Task<UserTokenValidationResult> ValidateAsync(string token, CancellationToken ct)
    {
        if (token == "valid-admin-token")
            return Task.FromResult(new UserTokenValidationResult(
                true, "admin-user-id", "admin@contoso.com",
                ["Mcpd.Admin", "Mcpd.Server.CodeAssist"], null));

        if (token == "valid-user-token")
            return Task.FromResult(new UserTokenValidationResult(
                true, "regular-user-id", "user@contoso.com",
                ["Mcpd.Server.CodeAssist"], null));

        if (token == "valid-datapipeline-user-token")
            return Task.FromResult(new UserTokenValidationResult(
                true, "dp-user-id", "dpuser@contoso.com",
                ["Mcpd.Server.DataPipeline"], null));

        if (token == "no-role-token")
            return Task.FromResult(new UserTokenValidationResult(
                true, "norole-user-id", "norole@contoso.com",
                [], null));

        return Task.FromResult(new UserTokenValidationResult(
            false, null, null, [], "Invalid token."));
    }
}

public sealed class UserTokenIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public UserTokenIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace IUserTokenValidator with test implementation
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IUserTokenValidator));
                if (descriptor is not null) services.Remove(descriptor);
                services.AddSingleton<IUserTokenValidator>(new TestUserTokenValidator());
            });
        });
        _client = _factory.CreateClient();
    }

    private Guid GetServerId(string serverName)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<McpdDbContext>();
        return db.McpServers.First(s => s.Name == serverName).Id;
    }

    [Fact]
    public async Task JwtBearerGrant_ValidUserToken_ReturnsAccessToken()
    {
        var serverId = GetServerId("code-assist");

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = "valid-user-token",
            ["server_id"] = serverId.ToString()
        });

        var response = await _client.PostAsync("/oauth/token", content);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("access_token").GetString().Should().NotBeNullOrWhiteSpace();
        body.RootElement.GetProperty("token_type").GetString().Should().Be("Bearer");
        body.RootElement.GetProperty("expires_in").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task JwtBearerGrant_InvalidToken_Returns401()
    {
        var serverId = GetServerId("code-assist");

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = "invalid-token-garbage",
            ["server_id"] = serverId.ToString()
        });

        var response = await _client.PostAsync("/oauth/token", content);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("error").GetString().Should().Be("invalid_grant");
    }

    [Fact]
    public async Task JwtBearerGrant_UserLacksServerRole_Returns401()
    {
        var serverId = GetServerId("data-pipeline");

        // valid-user-token only has Mcpd.Server.CodeAssist, not DataPipeline
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = "valid-user-token",
            ["server_id"] = serverId.ToString()
        });

        var response = await _client.PostAsync("/oauth/token", content);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("error").GetString().Should().Be("unauthorized_client");
    }

    [Fact]
    public async Task JwtBearerGrant_MissingAssertion_Returns400()
    {
        var serverId = GetServerId("code-assist");

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["server_id"] = serverId.ToString()
        });

        var response = await _client.PostAsync("/oauth/token", content);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("error").GetString().Should().Be("invalid_request");
    }

    [Fact]
    public async Task JwtBearerGrant_InvalidServerId_Returns400()
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = "valid-user-token",
            ["server_id"] = "not-a-guid"
        });

        var response = await _client.PostAsync("/oauth/token", content);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task JwtBearerGrant_WithRequestedScopes_ReturnsSubset()
    {
        var serverId = GetServerId("code-assist");

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = "valid-user-token",
            ["server_id"] = serverId.ToString(),
            ["scope"] = "read"
        });

        var response = await _client.PostAsync("/oauth/token", content);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var scopes = body!.RootElement.GetProperty("scope");
        scopes.GetArrayLength().Should().Be(1);
        scopes[0].GetString().Should().Be("read");
    }

    [Fact]
    public async Task JwtBearerGrant_NoRoleUser_Returns401()
    {
        var serverId = GetServerId("code-assist");

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = "no-role-token",
            ["server_id"] = serverId.ToString()
        });

        var response = await _client.PostAsync("/oauth/token", content);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminEndpoint_WithUserBearerToken_Succeeds()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/oauth/admin/servers");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "valid-admin-token");

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AdminEndpoint_WithNonAdminBearerToken_Falls_BackToApiKey()
    {
        // Non-admin Bearer token without API key should fail
        var request = new HttpRequestMessage(HttpMethod.Get, "/oauth/admin/servers");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "valid-user-token");

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminEndpoint_WithApiKey_StillWorks()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/oauth/admin/servers");
        request.Headers.Add("X-Admin-Key", "admin-api-key-change-in-production");

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ClientCredentialsGrant_StillWorks()
    {
        var serverId = GetServerId("code-assist");

        // Register client first
        var regPayload = new
        {
            client_name = "User Token Compat Test Client",
            redirect_uris = new[] { "https://app.contoso.com/oauth/callback" },
            grant_types = new[] { "client_credentials" },
            token_endpoint_auth_method = "client_secret_post",
            requested_server_ids = new[] { serverId },
            requested_scopes = new Dictionary<string, string[]>
            {
                [serverId.ToString()] = ["read"]
            }
        };

        var regResponse = await _client.PostAsJsonAsync("/oauth/register", regPayload, JsonOptions);
        regResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var regBody = await regResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var clientId = regBody!.RootElement.GetProperty("client_id").GetString()!;
        var clientSecret = regBody.RootElement.GetProperty("client_secret").GetString()!;

        // Token request via client_credentials should still work
        var tokenPayload = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["server_id"] = serverId.ToString(),
            ["scope"] = "read"
        });

        var tokenResponse = await _client.PostAsync("/oauth/token", tokenPayload);
        tokenResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnsupportedGrantType_Returns400()
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code"
        });

        var response = await _client.PostAsync("/oauth/token", content);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetProperty("error").GetString().Should().Be("unsupported_grant_type");
    }
}
