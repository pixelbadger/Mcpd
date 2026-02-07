using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Mcpd.Domain.Entities;
using Mcpd.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Mcpd.Api.Tests;

public sealed class IntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public IntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private Guid GetServerId(string serverName)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<McpdDbContext>();
        return db.McpServers.First(s => s.Name == serverName).Id;
    }

    [Fact]
    public async Task FullRegistration_AndTokenFlow()
    {
        var serverId = GetServerId("code-assist");

        // Register client
        var regPayload = new
        {
            client_name = "Integration Test Client",
            redirect_uris = new[] { "https://app.contoso.com/oauth/callback" },
            grant_types = new[] { "client_credentials" },
            token_endpoint_auth_method = "client_secret_post",
            requested_server_ids = new[] { serverId },
            requested_scopes = new Dictionary<string, string[]>
            {
                [serverId.ToString()] = ["read", "write"]
            }
        };

        var regResponse = await _client.PostAsJsonAsync("/oauth/register", regPayload, JsonOptions);
        regResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var regBody = await regResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var clientId = regBody!.RootElement.GetProperty("client_id").GetString()!;
        var clientSecret = regBody.RootElement.GetProperty("client_secret").GetString()!;
        var rat = regBody.RootElement.GetProperty("registration_access_token").GetString()!;

        clientId.Should().NotBeNullOrWhiteSpace();
        clientSecret.Should().NotBeNullOrWhiteSpace();
        rat.Should().NotBeNullOrWhiteSpace();

        // Get token for granted server
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

        var tokenBody = await tokenResponse.Content.ReadFromJsonAsync<JsonDocument>();
        tokenBody!.RootElement.GetProperty("access_token").GetString().Should().NotBeNullOrWhiteSpace();
        tokenBody.RootElement.GetProperty("token_type").GetString().Should().Be("Bearer");
    }

    [Fact]
    public async Task TokenRequest_WithoutGrant_Returns401()
    {
        var codeAssistId = GetServerId("code-assist");
        var dataPipelineId = GetServerId("data-pipeline");

        // Register client for code-assist only
        var regPayload = new
        {
            client_name = "Single Server Client",
            redirect_uris = new[] { "https://app.contoso.com/oauth/callback" },
            grant_types = new[] { "client_credentials" },
            token_endpoint_auth_method = "client_secret_post",
            requested_server_ids = new[] { codeAssistId },
            requested_scopes = new Dictionary<string, string[]>()
        };

        var regResponse = await _client.PostAsJsonAsync("/oauth/register", regPayload, JsonOptions);
        regResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var regBody = await regResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var clientId = regBody!.RootElement.GetProperty("client_id").GetString()!;
        var clientSecret = regBody.RootElement.GetProperty("client_secret").GetString()!;

        // Try to get token for data-pipeline (no grant)
        var tokenPayload = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["server_id"] = dataPipelineId.ToString()
        });

        var tokenResponse = await _client.PostAsync("/oauth/token", tokenPayload);
        tokenResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TokenRequest_ExcessScopes_Returns400()
    {
        var serverId = GetServerId("code-assist");

        var regPayload = new
        {
            client_name = "Scope Test Client",
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
        var regBody = await regResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var clientId = regBody!.RootElement.GetProperty("client_id").GetString()!;
        var clientSecret = regBody.RootElement.GetProperty("client_secret").GetString()!;

        // Request "write" scope which was not granted
        var tokenPayload = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["server_id"] = serverId.ToString(),
            ["scope"] = "read write"
        });

        var tokenResponse = await _client.PostAsync("/oauth/token", tokenPayload);
        tokenResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetRegistration_WithRAT_Succeeds()
    {
        var serverId = GetServerId("code-assist");

        var regPayload = new
        {
            client_name = "RAT Test Client",
            redirect_uris = new[] { "https://app.contoso.com/oauth/callback" },
            grant_types = new[] { "client_credentials" },
            token_endpoint_auth_method = "client_secret_post",
            requested_server_ids = new[] { serverId },
            requested_scopes = new Dictionary<string, string[]>()
        };

        var regResponse = await _client.PostAsJsonAsync("/oauth/register", regPayload, JsonOptions);
        var regBody = await regResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var clientId = regBody!.RootElement.GetProperty("client_id").GetString()!;
        var rat = regBody.RootElement.GetProperty("registration_access_token").GetString()!;

        // GET with RAT
        var request = new HttpRequestMessage(HttpMethod.Get, $"/oauth/register/{clientId}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", rat);

        var getResponse = await _client.SendAsync(request);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetRegistration_WithoutRAT_Returns401()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/oauth/register/nonexistent");
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteRegistration_WithRAT_Succeeds()
    {
        var serverId = GetServerId("code-assist");

        var regPayload = new
        {
            client_name = "Delete Test Client",
            redirect_uris = new[] { "https://app.contoso.com/oauth/callback" },
            grant_types = new[] { "client_credentials" },
            token_endpoint_auth_method = "client_secret_post",
            requested_server_ids = new[] { serverId },
            requested_scopes = new Dictionary<string, string[]>()
        };

        var regResponse = await _client.PostAsJsonAsync("/oauth/register", regPayload, JsonOptions);
        var regBody = await regResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var clientId = regBody!.RootElement.GetProperty("client_id").GetString()!;
        var rat = regBody.RootElement.GetProperty("registration_access_token").GetString()!;

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/oauth/register/{clientId}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", rat);

        var deleteResponse = await _client.SendAsync(request);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Token should now fail since client is revoked
        var tokenPayload = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = "any-secret",
            ["server_id"] = serverId.ToString()
        });
        var tokenResponse = await _client.PostAsync("/oauth/token", tokenPayload);
        tokenResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminEndpoints_RequireApiKey()
    {
        var response = await _client.GetAsync("/oauth/admin/servers");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminListServers_WithApiKey_Succeeds()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/oauth/admin/servers");
        request.Headers.Add("X-Admin-Key", "admin-api-key-change-in-production");

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        body!.RootElement.GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task SecretRotation_InvalidatesOldSecret()
    {
        var serverId = GetServerId("code-assist");

        // Register
        var regPayload = new
        {
            client_name = "Rotation Test Client",
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
        var regBody = await regResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var clientId = regBody!.RootElement.GetProperty("client_id").GetString()!;
        var oldSecret = regBody.RootElement.GetProperty("client_secret").GetString()!;

        // Rotate secret via admin endpoint
        var rotateRequest = new HttpRequestMessage(HttpMethod.Post, $"/oauth/admin/clients/{clientId}/rotate-secret");
        rotateRequest.Headers.Add("X-Admin-Key", "admin-api-key-change-in-production");
        rotateRequest.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

        var rotateResponse = await _client.SendAsync(rotateRequest);
        rotateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var rotateBody = await rotateResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var newSecret = rotateBody!.RootElement.GetProperty("client_secret").GetString()!;
        newSecret.Should().NotBe(oldSecret);

        // Old secret should no longer work
        var tokenPayloadOld = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = oldSecret,
            ["server_id"] = serverId.ToString()
        });
        var oldTokenResponse = await _client.PostAsync("/oauth/token", tokenPayloadOld);
        oldTokenResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // New secret should work
        var tokenPayloadNew = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = newSecret,
            ["server_id"] = serverId.ToString()
        });
        var newTokenResponse = await _client.PostAsync("/oauth/token", tokenPayloadNew);
        newTokenResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
