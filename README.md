# Mcpd - MCP Dynamic Client Registration Daemon

Mcpd is a .NET 10 OAuth2 Dynamic Client Registration service for MCP clients.
The current implementation supports RFC 7591-style registration, RFC 7592-style client self-management with registration access tokens, and `client_credentials` token issuance.

## Current Scope

This repository currently implements **client-level registration and scopes**.
Server-scoped grants and callback whitelists are planned but not yet implemented in code.

## Architecture

Clean Architecture with four projects:

```
src/
|- Mcpd.Domain/            # Entities, value objects, interfaces
|- Mcpd.Application/       # Commands, queries, validators, contracts
|- Mcpd.Infrastructure/    # EF Core, Argon2 hasher, JWT signing
`- Mcpd.Api/               # FastEndpoints API + preprocessors
```

Dependency flow:

- `Mcpd.Api -> Mcpd.Application -> Mcpd.Domain`
- `Mcpd.Infrastructure -> Mcpd.Application -> Mcpd.Domain`

Stack:

- .NET 10 / C#
- FastEndpoints
- Mediator (source-generated handlers)
- EF Core InMemory
- Argon2id secret hashing
- RSA-signed JWTs (`RS256`) with JWKS publishing
- FluentValidation

## Getting Started

Prerequisite: .NET 10 SDK (projects target `net10.0`).

```bash
dotnet build Mcpd.sln
dotnet run --project src/Mcpd.Api
```

If you want a fixed local URL while testing:

```bash
dotnet run --project src/Mcpd.Api --urls http://localhost:5000
```

## API Endpoints

FastEndpoints routes are under `/oauth` and JSON uses `snake_case`.

### Registration (RFC 7591 / 7592 style)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/oauth/register` | None | Register client |
| GET | `/oauth/register/{clientId}` | Bearer registration access token | Read registration |
| PUT | `/oauth/register/{clientId}` | Bearer registration access token | Update registration metadata |
| DELETE | `/oauth/register/{clientId}` | Bearer registration access token | Revoke registration |

### Token

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/oauth/token` | `client_secret_post` or `client_secret_basic` | Issue JWT access token |

Token request details:

- `grant_type` must be `client_credentials`
- Credentials may come from form fields (`client_id`, `client_secret`) or HTTP Basic auth
- `scope` is optional and must be a subset of registered scopes
- `resource` (RFC 8707) or `audience` can be supplied as token audience

### Admin

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/oauth/admin/clients/{clientId}/rotate-secret` | `X-Admin-Key` | Rotate client secret |

### Discovery / Well-Known

| Method | Path | Description |
|--------|------|-------------|
| GET | `/.well-known/jwks.json` | Public signing keys |
| GET | `/.well-known/oauth-authorization-server` | Authorization server metadata |
| GET | `/.well-known/openid-configuration` | OIDC-style metadata alias |

## Example

Register:

```bash
curl -s -X POST http://localhost:5000/oauth/register \
  -H "Content-Type: application/json" \
  -d '{
    "client_name": "my-app",
    "redirect_uris": ["https://app.contoso.com/oauth/callback"],
    "grant_types": ["client_credentials"],
    "token_endpoint_auth_method": "client_secret_post",
    "scope": ["read", "write"]
  }'
```

Issue token:

```bash
curl -s -X POST http://localhost:5000/oauth/token \
  -d "grant_type=client_credentials" \
  -d "client_id=<CLIENT_ID>" \
  -d "client_secret=<CLIENT_SECRET>" \
  -d "scope=read" \
  -d "resource=code-assist"
```

## Configuration

See `src/Mcpd.Api/appsettings.json`.

`Mcpd` section currently includes:

- `Issuer`
- `SigningKeyPath` (optional PEM private key path)
- `DefaultTokenLifetimeMinutes`
- `DefaultSecretLifetimeDays`
- `AdminApiKey`
- `AllowOpenRegistration`
- `RequireHttpsCallbacks`

`RateLimiting` section includes `Registration` and `Token` window/permit settings.

## Testing

```bash
dotnet test Mcpd.sln
```

The repository includes test projects for Domain, Application, Infrastructure, and API integration/well-known behavior.

## License

See `LICENSE`.
