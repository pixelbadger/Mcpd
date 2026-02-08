using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Mcpd.Api.Tests;

public sealed class IntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public IntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task FullRegistration_AndTokenFlow()
    {
        var regPayload = new
        {
            client_name = "Integration Test Client",
            redirect_uris = new[] { "https://app.contoso.com/oauth/callback" },
            grant_types = new[] { "client_credentials" },
            token_endpoint_auth_method = "client_secret_post",
            scope = new[] { "read", "write" }
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

        // Get token
        var tokenPayload = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["scope"] = "read"
        });

        var tokenResponse = await _client.PostAsync("/oauth/token", tokenPayload);
        tokenResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var tokenBody = await tokenResponse.Content.ReadFromJsonAsync<JsonDocument>();
        tokenBody!.RootElement.GetProperty("access_token").GetString().Should().NotBeNullOrWhiteSpace();
        tokenBody.RootElement.GetProperty("token_type").GetString().Should().Be("Bearer");
    }

    [Fact]
    public async Task TokenRequest_ExcessScopes_Returns400()
    {
        var regPayload = new
        {
            client_name = "Scope Test Client",
            redirect_uris = new[] { "https://app.contoso.com/oauth/callback" },
            grant_types = new[] { "client_credentials" },
            token_endpoint_auth_method = "client_secret_post",
            scope = new[] { "read" }
        };

        var regResponse = await _client.PostAsJsonAsync("/oauth/register", regPayload, JsonOptions);
        var regBody = await regResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var clientId = regBody!.RootElement.GetProperty("client_id").GetString()!;
        var clientSecret = regBody.RootElement.GetProperty("client_secret").GetString()!;

        // Request "write" scope which was not registered
        var tokenPayload = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["scope"] = "read write"
        });

        var tokenResponse = await _client.PostAsync("/oauth/token", tokenPayload);
        tokenResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetRegistration_WithRAT_Succeeds()
    {
        var regPayload = new
        {
            client_name = "RAT Test Client",
            redirect_uris = new[] { "https://app.contoso.com/oauth/callback" },
            grant_types = new[] { "client_credentials" },
            token_endpoint_auth_method = "client_secret_post",
            scope = new[] { "read" }
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
        var regPayload = new
        {
            client_name = "Delete Test Client",
            redirect_uris = new[] { "https://app.contoso.com/oauth/callback" },
            grant_types = new[] { "client_credentials" },
            token_endpoint_auth_method = "client_secret_post",
            scope = new[] { "read" }
        };

        var regResponse = await _client.PostAsJsonAsync("/oauth/register", regPayload, JsonOptions);
        var regBody = await regResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var clientId = regBody!.RootElement.GetProperty("client_id").GetString()!;
        var clientSecret = regBody.RootElement.GetProperty("client_secret").GetString()!;
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
            ["client_secret"] = clientSecret
        });
        var tokenResponse = await _client.PostAsync("/oauth/token", tokenPayload);
        tokenResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetRegistration_AfterRevoke_Returns401()
    {
        var regPayload = new
        {
            client_name = "Revoke RAT Test Client",
            redirect_uris = new[] { "https://app.contoso.com/oauth/callback" },
            grant_types = new[] { "client_credentials" },
            token_endpoint_auth_method = "client_secret_post",
            scope = new[] { "read" }
        };

        var regResponse = await _client.PostAsJsonAsync("/oauth/register", regPayload, JsonOptions);
        var regBody = await regResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var clientId = regBody!.RootElement.GetProperty("client_id").GetString()!;
        var rat = regBody.RootElement.GetProperty("registration_access_token").GetString()!;

        // Delete (revoke) the client
        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/oauth/register/{clientId}");
        deleteRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", rat);
        var deleteResponse = await _client.SendAsync(deleteRequest);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // GET with same RAT should now return 401
        var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/oauth/register/{clientId}");
        getRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", rat);
        var getResponse = await _client.SendAsync(getRequest);
        getResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TokenRequest_MultipleScopeParams_Succeeds()
    {
        var regPayload = new
        {
            client_name = "Multi Scope Test Client",
            redirect_uris = new[] { "https://app.contoso.com/oauth/callback" },
            grant_types = new[] { "client_credentials" },
            token_endpoint_auth_method = "client_secret_post",
            scope = new[] { "read", "write" }
        };

        var regResponse = await _client.PostAsJsonAsync("/oauth/register", regPayload, JsonOptions);
        var regBody = await regResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var clientId = regBody!.RootElement.GetProperty("client_id").GetString()!;
        var clientSecret = regBody.RootElement.GetProperty("client_secret").GetString()!;

        // Send multiple scope params (not space-separated)
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("client_secret", clientSecret),
            new KeyValuePair<string, string>("scope", "read"),
            new KeyValuePair<string, string>("scope", "write"),
        });

        var tokenResponse = await _client.PostAsync("/oauth/token", content);
        tokenResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TokenRequest_WrongAuthMethod_Returns401()
    {
        // Register with client_secret_post
        var regPayload = new
        {
            client_name = "Auth Method Test Client",
            redirect_uris = new[] { "https://app.contoso.com/oauth/callback" },
            grant_types = new[] { "client_credentials" },
            token_endpoint_auth_method = "client_secret_post",
            scope = new[] { "read" }
        };

        var regResponse = await _client.PostAsJsonAsync("/oauth/register", regPayload, JsonOptions);
        var regBody = await regResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var clientId = regBody!.RootElement.GetProperty("client_id").GetString()!;
        var clientSecret = regBody.RootElement.GetProperty("client_secret").GetString()!;

        // Authenticate via Basic auth (wrong method for this client)
        var credentials = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials"
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/token") { Content = content };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

        var tokenResponse = await _client.SendAsync(request);
        tokenResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RotateSecret_WithoutBody_Succeeds()
    {
        var regPayload = new
        {
            client_name = "Rotate No Body Test Client",
            redirect_uris = new[] { "https://app.contoso.com/oauth/callback" },
            grant_types = new[] { "client_credentials" },
            token_endpoint_auth_method = "client_secret_post",
            scope = new[] { "read" }
        };

        var regResponse = await _client.PostAsJsonAsync("/oauth/register", regPayload, JsonOptions);
        var regBody = await regResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var clientId = regBody!.RootElement.GetProperty("client_id").GetString()!;

        // POST with only admin key header, no body at all
        var rotateRequest = new HttpRequestMessage(HttpMethod.Post, $"/oauth/admin/clients/{clientId}/rotate-secret");
        rotateRequest.Headers.Add("X-Admin-Key", "admin-api-key-change-in-production");

        var rotateResponse = await _client.SendAsync(rotateRequest);
        rotateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var rotateBody = await rotateResponse.Content.ReadFromJsonAsync<JsonDocument>();
        rotateBody!.RootElement.GetProperty("client_secret").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SecretRotation_InvalidatesOldSecret()
    {
        // Register
        var regPayload = new
        {
            client_name = "Rotation Test Client",
            redirect_uris = new[] { "https://app.contoso.com/oauth/callback" },
            grant_types = new[] { "client_credentials" },
            token_endpoint_auth_method = "client_secret_post",
            scope = new[] { "read" }
        };

        var regResponse = await _client.PostAsJsonAsync("/oauth/register", regPayload, JsonOptions);
        var regBody = await regResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var clientId = regBody!.RootElement.GetProperty("client_id").GetString()!;
        var oldSecret = regBody.RootElement.GetProperty("client_secret").GetString()!;

        // Rotate secret via admin endpoint (no body required)
        var rotateRequest = new HttpRequestMessage(HttpMethod.Post, $"/oauth/admin/clients/{clientId}/rotate-secret");
        rotateRequest.Headers.Add("X-Admin-Key", "admin-api-key-change-in-production");

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
            ["client_secret"] = oldSecret
        });
        var oldTokenResponse = await _client.PostAsync("/oauth/token", tokenPayloadOld);
        oldTokenResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // New secret should work
        var tokenPayloadNew = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = newSecret
        });
        var newTokenResponse = await _client.PostAsync("/oauth/token", tokenPayloadNew);
        newTokenResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TokenRequest_WithAudience_Succeeds()
    {
        var regPayload = new
        {
            client_name = "Audience Test Client",
            redirect_uris = new[] { "https://app.contoso.com/oauth/callback" },
            grant_types = new[] { "client_credentials" },
            token_endpoint_auth_method = "client_secret_post",
            scope = new[] { "read" }
        };

        var regResponse = await _client.PostAsJsonAsync("/oauth/register", regPayload, JsonOptions);
        var regBody = await regResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var clientId = regBody!.RootElement.GetProperty("client_id").GetString()!;
        var clientSecret = regBody.RootElement.GetProperty("client_secret").GetString()!;

        var tokenPayload = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["resource"] = "http://localhost:5100",
            ["scope"] = "read"
        });

        var tokenResponse = await _client.PostAsync("/oauth/token", tokenPayload);
        tokenResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
