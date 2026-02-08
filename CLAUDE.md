# Claude.md — MCP Dynamic Client Registration (DCR) Solution

## Project Identity

**Solution**: `Mcpd` (MCP DCR Daemon)
**Framework**: .NET 10 / C# 13
**Purpose**: Enterprise Dynamic Client Registration server (RFC 7591 / RFC 7592) purpose-built to serve a **suite of MCP servers**. Each MCP server is a first-class tenant within the DCR system—clients register once, are granted access to specific servers, and token issuance is gated by per-server authorization.

---

## Architecture

Clean Architecture with four projects under `src/`:

```
Mcpd.sln
├── src/
│   ├── Mcpd.Domain/            # Entities, value objects, domain events, interfaces
│   ├── Mcpd.Application/       # Commands, queries, validators, DTOs, abstractions
│   ├── Mcpd.Infrastructure/    # EF Core, token services, credential hashing
│   └── Mcpd.Api/               # FastEndpoints, middleware, composition root
├── tests/
│   ├── Mcpd.Domain.Tests/
│   ├── Mcpd.Application.Tests/
│   ├── Mcpd.Infrastructure.Tests/
│   └── Mcpd.Api.Tests/
└── CLAUDE.md
```

**Dependency flow**: `Api → Application → Domain` and `Infrastructure → Application → Domain`. The API and Infrastructure layers depend on Application; Domain depends on nothing.

---

## Domain Layer (`Mcpd.Domain`)

### Entities

#### `McpServer`
Represents one MCP server in the enterprise suite. This is the tenancy boundary for all registration and authorization logic.

```csharp
public sealed class McpServer
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }                  // e.g. "code-assist", "data-pipeline"
    public string Description { get; private set; }
    public Uri BaseUri { get; private set; }                  // The MCP server's endpoint
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DeactivatedAt { get; private set; }

    // Navigation
    public IReadOnlyCollection<CallbackWhitelistEntry> CallbackWhitelist { get; }
    public IReadOnlyCollection<ClientServerGrant> ClientGrants { get; }
}
```

#### `ClientRegistration`
An OAuth2 client registered via DCR (RFC 7591). A client exists independently of any server—server access is granted separately.

```csharp
public sealed class ClientRegistration
{
    public Guid Id { get; private set; }
    public string ClientId { get; private set; }              // Opaque, generated
    public string ClientSecretHash { get; private set; }      // Argon2id hash, never plaintext
    public string ClientName { get; private set; }
    public ClientStatus Status { get; private set; }          // Active, Suspended, Revoked
    public string TokenEndpointAuthMethod { get; private set; } // "client_secret_post" | "client_secret_basic"
    public string[] GrantTypes { get; private set; }          // ["client_credentials"]
    public string[] RedirectUris { get; private set; }        // Validated against server-scoped whitelist
    public string? RegistrationAccessToken { get; private set; } // RFC 7592 management token (hashed)
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SecretExpiresAt { get; private set; }
    public DateTimeOffset? SecretRotatedAt { get; private set; }

    // Navigation
    public IReadOnlyCollection<ClientServerGrant> ServerGrants { get; }
}
```

#### `ClientServerGrant`
The join entity that authorises a specific client to access a specific MCP server. **This is the core of server-scoped access control.** Without a grant row, a client cannot obtain tokens for that server regardless of valid credentials.

```csharp
public sealed class ClientServerGrant
{
    public Guid Id { get; private set; }
    public Guid ClientRegistrationId { get; private set; }
    public Guid McpServerId { get; private set; }
    public string[] Scopes { get; private set; }              // Server-specific scopes granted
    public bool IsActive { get; private set; }
    public DateTimeOffset GrantedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    // Navigation
    public ClientRegistration Client { get; }
    public McpServer Server { get; }
}
```

#### `CallbackWhitelistEntry`
Scoped to an individual MCP server. During registration, submitted `redirect_uris` are validated against **the whitelist of the specific server(s)** the client requests access to.

```csharp
public sealed class CallbackWhitelistEntry
{
    public Guid Id { get; private set; }
    public Guid McpServerId { get; private set; }
    public string Pattern { get; private set; }               // Exact URI or pattern with wildcard subdomain
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    // Navigation
    public McpServer Server { get; }
}
```

**Pattern matching rules:**
- Exact match: `https://app.contoso.com/callback`
- Wildcard subdomain: `https://*.contoso.com/callback` (one level only, no recursive wildcards)
- HTTPS required in production; `http://localhost:*` permitted only when server is flagged development-mode
- No fragment components, no open redirectors

#### `AuditLogEntry`
Every mutation (registration, grant, revocation, rotation) produces an immutable audit record.

```csharp
public sealed class AuditLogEntry
{
    public Guid Id { get; private set; }
    public string Action { get; private set; }                // "ClientRegistered", "GrantRevoked", etc.
    public string ActorId { get; private set; }               // Client ID or admin identity
    public Guid? ClientRegistrationId { get; private set; }
    public Guid? McpServerId { get; private set; }
    public string? Detail { get; private set; }               // JSON blob of relevant changes
    public DateTimeOffset Timestamp { get; private set; }
}
```

### Value Objects

```
ClientId          — Strongly-typed wrapper, generated via URL-safe Base64 (32 bytes)
ClientSecret      — Transient; generated, returned once, then only the hash is stored
HashedSecret      — Argon2id output with embedded salt/params
RegistrationToken — RFC 7592 bearer token for client self-management
```

### Enums

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

public interface IMcpServerRepository
{
    Task<McpServer?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<McpServer?> GetByNameAsync(string name, CancellationToken ct);
    Task<IReadOnlyList<McpServer>> GetAllActiveAsync(CancellationToken ct);
}

public interface IClientServerGrantRepository
{
    Task<ClientServerGrant?> GetAsync(Guid clientId, Guid serverId, CancellationToken ct);
    Task<IReadOnlyList<ClientServerGrant>> GetGrantsForClientAsync(Guid clientId, CancellationToken ct);
    Task AddAsync(ClientServerGrant grant, CancellationToken ct);
    Task UpdateAsync(ClientServerGrant grant, CancellationToken ct);
}

public interface ICallbackWhitelistRepository
{
    Task<IReadOnlyList<CallbackWhitelistEntry>> GetForServerAsync(Guid serverId, CancellationToken ct);
}

public interface IAuditLogRepository
{
    Task AddAsync(AuditLogEntry entry, CancellationToken ct);
}
```

---

## Application Layer (`Mcpd.Application`)

Uses **CQRS with source-generated Mediator** — commands and queries implement `ICommand<T>`/`IQuery<T>`, handlers implement `ICommandHandler<T,R>`/`IQueryHandler<T,R>`. Endpoints inject `IMediator` and dispatch via `mediator.Send()`. Handler methods return `ValueTask<T>` and are named `Handle`.

### Service Interfaces (defined here, implemented in Infrastructure)

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
    string GenerateAccessToken(string clientId, Guid serverId, string[] scopes, TimeSpan lifetime);
}

public interface ICallbackValidator
{
    /// <summary>
    /// Validates that every URI in <paramref name="redirectUris"/> matches at least one
    /// active whitelist entry for the specified server.
    /// </summary>
    Task<CallbackValidationResult> ValidateAsync(Guid serverId, string[] redirectUris, CancellationToken ct);
}
```

### Commands

#### `RegisterClientCommand`
RFC 7591 registration. Accepts requested server IDs, validates callbacks against each server's whitelist, creates the client and per-server grants atomically.

```
Input:
  - ClientName: string (required)
  - RedirectUris: string[] (required, validated per-server)
  - GrantTypes: string[] (default ["client_credentials"])
  - TokenEndpointAuthMethod: string (default "client_secret_post")
  - RequestedServerIds: Guid[] (required, at least one)
  - RequestedScopes: Dictionary<Guid, string[]> (server ID → scopes)

Output:
  - ClientId: string
  - ClientSecret: string (plaintext, returned this once only)
  - RegistrationAccessToken: string
  - ClientSecretExpiresAt: DateTimeOffset?
  - GrantedServers: ServerGrantSummary[]

Validation (FluentValidation):
  - ClientName not empty, max 256 chars
  - At least one RequestedServerId, all must reference active servers
  - Each RedirectUri is a valid absolute URI with https (or localhost exception)
  - Each RedirectUri must match at least one CallbackWhitelistEntry for EVERY requested server
  - GrantTypes must be a subset of ["client_credentials", "authorization_code"]
  - TokenEndpointAuthMethod ∈ {"client_secret_post", "client_secret_basic"}

Errors:
  - "invalid_redirect_uri" — URI doesn't match any whitelist entry for one or more servers
  - "invalid_server" — Server ID not found or inactive
  - "invalid_client_metadata" — Other validation failures
```

#### `UpdateClientCommand` (RFC 7592)
Requires the Registration Access Token. Can update metadata and request access to additional servers (callback validation re-runs).

#### `RotateClientSecretCommand`
Generates new secret, hashes and stores, returns plaintext once. Old secret is invalidated immediately.

#### `RevokeClientCommand`
Soft-deletes: sets `Status = Revoked`, deactivates all server grants, logs audit entry.

#### `GrantServerAccessCommand`
Admin-initiated: grants an existing client access to an additional MCP server with specified scopes. Validates callbacks against the new server's whitelist.

#### `RevokeServerAccessCommand`
Admin-initiated: revokes a client's access to a specific server.

### Queries

#### `GetClientRegistrationQuery`
Returns full registration metadata including granted servers. Requires Registration Access Token (RFC 7592) or admin auth.

#### `ValidateTokenRequestQuery`
**Critical path for token issuance.** Given `client_id`, `client_secret`, and the requested `server_id`:
1. Verify client exists and is Active
2. Verify secret against stored Argon2id hash
3. Verify an active `ClientServerGrant` exists for this client + server pair
4. Verify requested scopes are a subset of the grant's scopes
5. Return authorisation decision

#### `ListMcpServersQuery`
Returns all active MCP servers (admin only).

#### `ListServerClientsQuery`
Returns all clients with active grants for a given server (admin only).

### DTOs

All DTOs live in `Mcpd.Application.Contracts`. Use records:

```csharp
public sealed record ClientRegistrationResponse(
    string ClientId,
    string? ClientSecret,            // Only populated on initial registration and rotation
    string ClientName,
    string[] RedirectUris,
    string[] GrantTypes,
    string TokenEndpointAuthMethod,
    string? RegistrationAccessToken, // Only on initial registration
    DateTimeOffset? ClientSecretExpiresAt,
    ServerGrantSummary[] GrantedServers
);

public sealed record ServerGrantSummary(
    Guid ServerId,
    string ServerName,
    string[] Scopes,
    bool IsActive
);

public sealed record TokenResponse(
    string AccessToken,
    string TokenType,               // "Bearer"
    int ExpiresIn,
    string[] Scope
);

public sealed record TokenErrorResponse(
    string Error,                   // RFC 6749 error code
    string? ErrorDescription
);
```

---

## Infrastructure Layer (`Mcpd.Infrastructure`)

### Entity Framework

#### `McpdDbContext`

```csharp
public sealed class McpdDbContext : DbContext
{
    public DbSet<McpServer> McpServers => Set<McpServer>();
    public DbSet<ClientRegistration> ClientRegistrations => Set<ClientRegistration>();
    public DbSet<ClientServerGrant> ClientServerGrants => Set<ClientServerGrant>();
    public DbSet<CallbackWhitelistEntry> CallbackWhitelist => Set<CallbackWhitelistEntry>();
    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();
}
```

**Provider**: `UseInMemoryDatabase("McpdDb")` for initial implementation. The design must remain provider-agnostic — no InMemory-specific behaviour. All queries must be EF-translatable so migration to SQL Server/PostgreSQL requires only a provider swap and migrations.

**Configuration**: Use `IEntityTypeConfiguration<T>` in separate files per entity. Configure:
- Owned types for value objects
- `HasIndex` on `ClientRegistration.ClientId` (unique)
- `HasIndex` on composite `(ClientRegistrationId, McpServerId)` on `ClientServerGrant` (unique)
- Soft-delete query filters on `ClientRegistration` and `ClientServerGrant`
- Temporal columns (`CreatedAt`, `ModifiedAt`) via shadow properties

#### Seeding

The InMemory provider should be seeded on startup with a configurable set of MCP servers and their callback whitelists via `appsettings.json`:

```json
{
  "McpServers": [
    {
      "Name": "code-assist",
      "Description": "Code generation and review MCP server",
      "BaseUri": "https://mcp-code.internal.contoso.com",
      "CallbackWhitelist": [
        "https://app.contoso.com/oauth/callback",
        "https://*.contoso.com/oauth/callback",
        "http://localhost:*/oauth/callback"
      ]
    },
    {
      "Name": "data-pipeline",
      "Description": "Data transformation and query MCP server",
      "BaseUri": "https://mcp-data.internal.contoso.com",
      "CallbackWhitelist": [
        "https://app.contoso.com/oauth/callback",
        "https://data.contoso.com/callback"
      ]
    }
  ]
}
```

Each server has **its own independent callback whitelist**. A redirect URI valid for `code-assist` may be rejected for `data-pipeline`.

### Token Generation

Use `Microsoft.IdentityModel.JsonWebTokens` for JWT access tokens:

```
Claims:
  - sub: client_id
  - aud: server name (e.g. "code-assist")
  - server_id: Guid of the MCP server
  - scope: space-separated scopes from the grant
  - iat, exp, jti: standard temporal + nonce claims
  - iss: DCR server identifier from config

Signing: HMAC-SHA256 with per-environment key from config (swap to RSA/ECDSA for production)
```

### Secret Hashing

Use `Konscious.Security.Cryptography.Argon2` for Argon2id hashing of client secrets.

### Callback Validation Implementation

```csharp
public sealed class CallbackValidator : ICallbackValidator
{
    // Exact match: string equality
    // Wildcard subdomain: replace "*." prefix with regex [a-z0-9-]+\.
    // Localhost: match any port
    // ALWAYS reject: fragments, userinfo, non-https (except localhost), open redirectors
}
```

---

## API Layer (`Mcpd.Api`)

### FastEndpoints Configuration

```csharp
builder.Services.AddFastEndpoints();
builder.Services.AddAuthorization();

// ...

app.UseFastEndpoints(c =>
{
    c.Endpoints.RoutePrefix = "oauth";
    c.Serializer.Options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    c.Errors.UseProblemDetails();     // RFC 9457
});
```

**JSON serialisation**: Use `snake_case` globally to match OAuth2/RFC conventions (`client_id`, `redirect_uris`, `grant_types`, etc.).

### Endpoints

All endpoints use the FastEndpoints `Endpoint<TRequest, TResponse>` base. Endpoints inject `IMediator` and dispatch commands/queries via `mediator.Send()`.

#### `POST /oauth/register` — Dynamic Client Registration (RFC 7591)

```
Endpoint: RegisterClientEndpoint
Request:  RegisterClientRequest (bound from JSON body)
Response: ClientRegistrationResponse (201 Created)
Errors:   400 with RFC 7591 error body

Auth: None (open registration) or API key gated per deployment config
```

#### `GET /oauth/register/{clientId}` — Read Registration (RFC 7592)

```
Endpoint: GetClientRegistrationEndpoint
Request:  route param clientId
Response: ClientRegistrationResponse (200)
Auth:     Registration Access Token (Bearer) — issued at registration time
```

#### `PUT /oauth/register/{clientId}` — Update Registration (RFC 7592)

```
Endpoint: UpdateClientRegistrationEndpoint
Request:  UpdateClientRequest (JSON body) + route param
Response: ClientRegistrationResponse (200)
Auth:     Registration Access Token (Bearer)
```

#### `DELETE /oauth/register/{clientId}` — Revoke Registration (RFC 7592)

```
Endpoint: RevokeClientRegistrationEndpoint
Request:  route param clientId
Response: 204 No Content
Auth:     Registration Access Token (Bearer)
```

#### `POST /oauth/token` — Token Issuance

```
Endpoint: TokenEndpoint
Request:  TokenRequest (form-encoded per RFC 6749)
Response: TokenResponse (200) or TokenErrorResponse (400/401)

Required fields:
  - grant_type: "client_credentials"
  - client_id + client_secret (via post body or Basic auth header)
  - server_id: Guid — WHICH MCP server this token is for
  - scope: (optional) requested scopes, must be subset of grant

Validation sequence:
  1. Authenticate client (id + secret)
  2. Lookup active ClientServerGrant for (client, server_id) → 401 "unauthorized_client" if missing
  3. Validate requested scopes ⊆ granted scopes → 400 "invalid_scope" if not
  4. Issue JWT with server-scoped audience claim
```

This is the **critical server-scoped auth gate**. A client with valid credentials but no grant for the requested server receives `401 unauthorized_client`.

#### `POST /oauth/admin/servers/{serverId}/grants` — Grant Server Access (Admin)

```
Endpoint: GrantServerAccessEndpoint
Request:  { clientId, scopes[] }
Response: ServerGrantSummary (201)
Auth:     Admin API key or admin JWT
```

#### `DELETE /oauth/admin/servers/{serverId}/grants/{clientId}` — Revoke Server Access (Admin)

```
Endpoint: RevokeServerAccessEndpoint
Response: 204 No Content
Auth:     Admin API key or admin JWT
```

#### `POST /oauth/admin/clients/{clientId}/rotate-secret` — Rotate Secret (Admin)

```
Endpoint: RotateClientSecretEndpoint
Response: { clientSecret, clientSecretExpiresAt } (200)
Auth:     Admin API key or Registration Access Token
```

#### `GET /oauth/admin/servers` — List MCP Servers (Admin)

#### `GET /oauth/admin/servers/{serverId}/clients` — List Server Clients (Admin)

### Middleware & Cross-Cutting

#### Registration Access Token Middleware
A custom FastEndpoints `IPreProcessor` that extracts `Bearer` tokens from the `Authorization` header on RFC 7592 management endpoints, hashes them, and validates against stored registration access token hashes.

#### Admin Auth
Separate concern from client auth. Use a simple shared API key (`X-Admin-Key` header) for initial implementation, with a clear interface to swap to JWT-based admin auth later.

#### Problem Details (RFC 9457)
All error responses use `application/problem+json` except the token endpoint, which uses RFC 6749 error format (`{"error": "...", "error_description": "..."}`).

#### Rate Limiting
Use `System.Threading.RateLimiting` with a fixed-window policy on `/oauth/register` (prevent registration spam) and `/oauth/token` (prevent brute force).

---

## Validation Architecture

Use **FluentValidation** integrated with FastEndpoints' built-in validation pipeline.

### Registration Callback Validation Flow

This is the most complex validation because it crosses multiple server boundaries:

```
1. Client submits: { redirect_uris: [...], requested_server_ids: [A, B, C] }

2. For EACH server in requested_server_ids:
   a. Load CallbackWhitelistEntry[] for that server
   b. For EACH redirect_uri:
      - Test against every whitelist entry (exact match or pattern)
      - If no match found → collect error: { server_id, uri, reason }

3. If ANY uri fails against ANY requested server → reject entire registration
   Response: 400 { error: "invalid_redirect_uri", error_description: "URI https://... not whitelisted for server 'data-pipeline'" }

4. Only if ALL uris pass ALL servers → proceed with registration
```

This ensures a client never accidentally registers with URIs that work for one server but not another it claims to need.

---

## Configuration (`appsettings.json` shape)

```json
{
  "Mcpd": {
    "Issuer": "https://dcr.contoso.com",
    "TokenSigningKey": "...",
    "DefaultTokenLifetimeMinutes": 60,
    "DefaultSecretLifetimeDays": 90,
    "AdminApiKey": "...",
    "AllowOpenRegistration": true,
    "RequireHttpsCallbacks": true
  },
  "McpServers": [ ... ],
  "RateLimiting": {
    "Registration": { "Window": "00:01:00", "PermitLimit": 10 },
    "Token": { "Window": "00:01:00", "PermitLimit": 60 }
  }
}
```

Bind to strongly-typed `McpdOptions`, `RateLimitingOptions` via `IOptions<T>`.

---

## Testing Strategy

### Unit Tests
- Domain entity behaviour (state transitions, invariant enforcement)
- Callback pattern matching (exact, wildcard subdomain, localhost, rejection cases)
- Secret hashing round-trip
- Validator logic for each command

### Integration Tests
- Full registration → token flow against InMemory EF
- Multi-server registration with mixed whitelist outcomes
- Token request rejected when client lacks grant for requested server
- Token request rejected when scopes exceed grant
- Secret rotation invalidates old secret
- RFC 7592 management endpoints with registration access tokens

### Test Organisation
Use `WebApplicationFactory<Program>` with the InMemory provider. Each test class seeds its own MCP server configuration to avoid cross-test contamination.

---

## Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| Server as first-class entity, not just a scope | Enables independent callback whitelists, independent grant lifecycle, and clear admin UX per server |
| Callback whitelist per server, not global | Different servers may be consumed by different client populations with different infrastructure |
| `ClientServerGrant` as explicit join | Makes the "is this client allowed to talk to this server?" query a single indexed lookup, not a scope string parse |
| Argon2id for secrets | Industry standard for password/secret hashing; bcrypt is acceptable fallback |
| Registration Access Token (RFC 7592) | Allows clients to self-manage without admin intervention |
| JWT access tokens with `aud` = server name | MCP servers can validate tokens independently using shared signing key or JWKS |
| snake_case JSON | OAuth2 RFCs use snake_case; fighting this creates mapping pain for every client |
| Source-generated Mediator | Provides automatic handler discovery and DI registration via compile-time source generation; avoids MediatR's runtime reflection overhead |
| InMemory EF with provider-agnostic queries | Zero-friction startup; swap to real provider by changing one line and adding migrations |

---

## Coding Conventions

- **Nullable reference types**: enabled, no suppressions
- **Primary constructors**: use for DI in services and endpoints
- **File-scoped namespaces**: always
- **Records**: for DTOs, commands, queries — never for entities
- **Sealed**: all classes unless explicitly designed for inheritance
- **No public setters on entities**: use domain methods that enforce invariants
- **CancellationToken**: propagate through every async call
- **ConfigureAwait(false)**: not needed in ASP.NET Core; omit for clarity

---

## Dependency Summary

```xml
<!-- Domain: no dependencies -->

<!-- Application -->
<PackageReference Include="FluentValidation" />
<PackageReference Include="Mediator.Abstractions" />

<!-- Infrastructure -->
<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" />
<PackageReference Include="Microsoft.IdentityModel.JsonWebTokens" />
<PackageReference Include="Konscious.Security.Cryptography.Argon2" />

<!-- Api -->
<PackageReference Include="FastEndpoints" />
<PackageReference Include="FastEndpoints.Security" />
<PackageReference Include="Mediator.SourceGenerator" />
```

---

## Startup Sequence

1. Bind configuration (`McpdOptions`, `McpServers[]`)
2. Register EF `McpdDbContext` with InMemory provider
3. Register repositories, services, validators
4. `AddMediator()` with scoped lifetime (source-generated handler discovery)
5. `AddFastEndpoints()` + `AddAuthorization()`
6. Build app
7. Seed MCP servers and callback whitelists from config
8. `UseFastEndpoints(...)` with snake_case and ProblemDetails
9. `UseRateLimiter()`
10. Run
