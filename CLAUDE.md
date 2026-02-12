# Claude.md - MCP Dynamic Client Registration (DCR) Solution

## Project Identity

**Solution**: `Mcpd`  
**Framework**: .NET 10 / C#  
**Purpose**: OAuth2 Dynamic Client Registration service for MCP clients.

The current codebase implements client registration and client credential token issuance with client-level scopes.
Server-specific grant modeling is not implemented yet.

## Repository Workflow (Required)

1. **Checkout a new feature branch before starting new work.**
2. **Use the `gh` CLI to raise a pull request when work is complete.**

## Architecture

Clean Architecture with four projects under `src/`:

```
Mcpd.sln
|- src/
|  |- Mcpd.Domain/          # Entities, value objects, interfaces
|  |- Mcpd.Application/     # Commands, queries, validators, contracts, components
|  |- Mcpd.Infrastructure/  # External adapters (EF Core persistence)
|  `- Mcpd.Api/             # FastEndpoints API and preprocessors
`- tests/
   |- Mcpd.Domain.Tests/
   |- Mcpd.Application.Tests/
   |- Mcpd.Infrastructure.Tests/
   `- Mcpd.Api.Tests/
```

Dependency flow (project references):

- `Mcpd.Domain` depends on nothing.
- `Mcpd.Application -> Mcpd.Domain`
- `Mcpd.Infrastructure -> Mcpd.Domain` (repository implementations)
- `Mcpd.Api -> Mcpd.Application` and `Mcpd.Api -> Mcpd.Infrastructure`

## Domain Layer (`Mcpd.Domain`)

### Entities

#### `ClientRegistration`

Represents one registered OAuth client.

```csharp
public sealed class ClientRegistration
{
    public Guid Id { get; private set; }
    public string ClientId { get; private set; }
    public string ClientSecretHash { get; private set; }
    public string ClientName { get; private set; }
    public ClientStatus Status { get; private set; }
    public string TokenEndpointAuthMethod { get; private set; }
    public string[] GrantTypes { get; private set; }
    public string[] RedirectUris { get; private set; }
    public string[] Scope { get; private set; }
    public string? RegistrationAccessToken { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SecretExpiresAt { get; private set; }
    public DateTimeOffset? SecretRotatedAt { get; private set; }

    public void SetSecretExpiry(DateTimeOffset expiresAt);
    public void RotateSecret(string newSecretHash, DateTimeOffset? newExpiresAt);
    public void UpdateMetadata(string clientName, string[] redirectUris, string tokenEndpointAuthMethod, string[] grantTypes, string[] scope);
    public void Revoke();
    public void Suspend();
}
```

#### `AuditLogEntry`

Immutable event log entry for registration mutations.

```csharp
public sealed class AuditLogEntry
{
    public Guid Id { get; private set; }
    public string Action { get; private set; }
    public string ActorId { get; private set; }
    public Guid? ClientRegistrationId { get; private set; }
    public Guid? McpServerId { get; private set; }
    public string? Detail { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }
}
```

### Value Objects

- `ClientId`
- `ClientSecret`
- `HashedSecret`
- `RegistrationToken`

### Enum

```csharp
public enum ClientStatus { Active, Suspended, Revoked }
```

### Domain Interfaces

```csharp
public interface IClientRegistrationRepository
{
    Task<ClientRegistration?> GetByClientIdAsync(string clientId, CancellationToken ct);
    Task<ClientRegistration?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(ClientRegistration registration, CancellationToken ct);
    Task UpdateAsync(ClientRegistration registration, CancellationToken ct);
}

public interface IAuditLogRepository
{
    Task AddAsync(AuditLogEntry entry, CancellationToken ct);
}
```

## Application Layer (`Mcpd.Application`)

Uses source-generated Mediator with CQRS-style records and handlers.

### Components

Component interfaces and concrete implementations live together in Application.

```csharp
public interface ISecretHasher
{
    HashedSecret Hash(string plaintext);
    bool Verify(string plaintext, HashedSecret hash);
}

public interface ITokenGenerator
{
    string GenerateClientId();
    string GenerateClientSecret();
    string GenerateRegistrationAccessToken();
    string GenerateAccessToken(string clientId, string[] scopes, TimeSpan lifetime, string? audience);
}
```

- `Argon2SecretHasher` (implements `ISecretHasher`)
- `JwtTokenGenerator` (implements `ITokenGenerator`)
- `SigningKeyManager` (RSA key management + JWKS materialization)

### Commands

- `RegisterClientCommand`
- `UpdateClientCommand`
- `RevokeClientCommand`
- `RotateClientSecretCommand`

### Queries

- `GetClientRegistrationQuery`
- `ValidateTokenRequestQuery`

`ValidateTokenRequestQuery` performs:

1. Verify client exists and is active.
2. Verify request auth method matches registered method.
3. Verify client secret hash.
4. Verify requested scopes are subset of registered scopes.
5. Issue JWT access token.

### Contracts

```csharp
public sealed record ClientRegistrationResponse(
    string ClientId,
    string? ClientSecret,
    string ClientName,
    string[] RedirectUris,
    string[] GrantTypes,
    string TokenEndpointAuthMethod,
    string[] Scope,
    string? RegistrationAccessToken,
    DateTimeOffset? ClientSecretExpiresAt);

public sealed record TokenResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    string[] Scope);

public sealed record TokenErrorResponse(
    string Error,
    string? ErrorDescription);
```

### Validation

`RegisterClientCommandValidator` validates:

- `client_name` required, max 256, no control characters
- at least one `redirect_uri`
- each redirect URI must be absolute and either:
  - `https://...`, or
  - `http://localhost...`
- redirect URIs cannot contain fragments
- grant types currently restricted to `client_credentials`
- auth method must be `client_secret_post` or `client_secret_basic`

## Infrastructure Layer (`Mcpd.Infrastructure`)

Infrastructure is reserved for external-system adapters. In this codebase, that currently means EF Core persistence and repository implementations of Domain contracts consumed by Application.

### Persistence

`McpdDbContext` currently has:

```csharp
public DbSet<ClientRegistration> ClientRegistrations => Set<ClientRegistration>();
public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();
```

Provider: EF Core InMemory (`UseInMemoryDatabase("McpdDb")`).

Entity configurations:

- unique index on `ClientRegistration.ClientId`
- array conversions for `GrantTypes`, `RedirectUris`, and `Scope`
- audit log index on `Timestamp`

## API Layer (`Mcpd.Api`)

### FastEndpoints Setup

```csharp
builder.Services.AddFastEndpoints();
builder.Services.AddAuthorization();

app.UseFastEndpoints(c =>
{
    c.Endpoints.RoutePrefix = "oauth";
    c.Serializer.Options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    c.Errors.UseProblemDetails();
});
```

### Implemented Endpoints

#### Registration Management

- `POST /oauth/register`
- `GET /oauth/register/{clientId}`
- `PUT /oauth/register/{clientId}`
- `DELETE /oauth/register/{clientId}`

`GET/PUT/DELETE` require a Bearer registration access token via `RegistrationAccessTokenPreProcessor`.

#### Token Endpoint

- `POST /oauth/token`

Supports:

- `grant_type=client_credentials`
- client auth via form (`client_secret_post`) or Basic auth (`client_secret_basic`)
- optional `scope`
- optional audience input via `resource` (preferred) or `audience`

#### Admin

- `POST /oauth/admin/clients/{clientId}/rotate-secret`

Protected by `X-Admin-Key` via `AdminApiKeyPreProcessor`.

#### Well-Known Endpoints

- `GET /.well-known/jwks.json`
- `GET /.well-known/oauth-authorization-server`
- `GET /.well-known/openid-configuration`

### Rate Limiting

Rate limiter policies are registered from configuration (`registration`, `token`) and middleware is enabled with `app.UseRateLimiter()`.

## Configuration (`appsettings.json`)

```json
{
  "Mcpd": {
    "Issuer": "https://dcr.contoso.com",
    "SigningKeyPath": null,
    "DefaultTokenLifetimeMinutes": 60,
    "DefaultSecretLifetimeDays": 90,
    "AdminApiKey": "...",
    "AllowOpenRegistration": true,
    "RequireHttpsCallbacks": true
  },
  "RateLimiting": {
    "Registration": { "Window": "00:01:00", "PermitLimit": 10 },
    "Token": { "Window": "00:01:00", "PermitLimit": 60 }
  }
}
```

Bound via `IOptions<McpdOptions>` and `IOptions<RateLimitingOptions>`.

## Startup Sequence

1. Bind `McpdOptions` and `RateLimitingOptions`
2. Initialize `SigningKeyManager`
3. Register InMemory EF `McpdDbContext`
4. Register repositories and services
5. Register Mediator source-generated handlers
6. Register FastEndpoints and authorization
7. Register rate limiter policies
8. Build app
9. `UseRateLimiter()`
10. `UseFastEndpoints(...)`
11. Map well-known metadata/JWKS endpoints
12. Run

## Testing Strategy

Test projects cover:

- Domain entity and value object behavior
- Application validator, token-validation rules, and crypto/JWT components
- Infrastructure persistence behavior
- API integration flows, registration access token checks, secret rotation, and well-known metadata endpoints

Use:

```bash
dotnet test Mcpd.sln
```

## Coding Conventions

- Nullable reference types enabled
- File-scoped namespaces
- Records for contracts/commands/queries
- Sealed classes unless inheritance is intentional
- No public setters on entities
- Pass `CancellationToken` through async boundaries
