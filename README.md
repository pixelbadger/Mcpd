# Mcpd — MCP Dynamic Client Registration Daemon

Enterprise Dynamic Client Registration server (RFC 7591 / RFC 7592) purpose-built to serve a suite of MCP servers. Each MCP server is a first-class tenant — clients register once, are granted access to specific servers, and token issuance is gated by per-server authorization.

## Architecture

Clean Architecture with four projects:

```
src/
├── Mcpd.Domain/            # Entities, value objects, interfaces
├── Mcpd.Application/       # Commands, queries, validators, DTOs
├── Mcpd.Infrastructure/    # EF Core, Argon2, JWT, callback validation
└── Mcpd.Api/               # FastEndpoints, middleware, composition root
```

**Stack**: .NET 10, C# 13, FastEndpoints, Mediator (source-generated), EF Core InMemory, Argon2id, HMAC-SHA256 JWTs, FluentValidation

## Getting Started

```bash
dotnet build Mcpd.sln
dotnet run --project src/Mcpd.Api
```

The server starts on `http://localhost:5000` by default. Two MCP servers (`code-assist` and `data-pipeline`) are seeded from `appsettings.json`.

## API Endpoints

All endpoints are prefixed with `/oauth`. JSON uses `snake_case` per OAuth2 conventions.

### Client Registration (RFC 7591 / 7592)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/oauth/register` | None | Register a new client |
| GET | `/oauth/register/{clientId}` | Bearer RAT | Read registration |
| PUT | `/oauth/register/{clientId}` | Bearer RAT | Update registration |
| DELETE | `/oauth/register/{clientId}` | Bearer RAT | Revoke registration |

### Token Issuance

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/oauth/token` | Client credentials (form-encoded) | Issue a server-scoped JWT |

### Admin

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/oauth/admin/servers` | `X-Admin-Key` | List MCP servers |
| GET | `/oauth/admin/servers/{serverId}/clients` | `X-Admin-Key` | List clients for a server |
| POST | `/oauth/admin/servers/{serverId}/grants` | `X-Admin-Key` | Grant server access |
| DELETE | `/oauth/admin/servers/{serverId}/grants/{clientId}` | `X-Admin-Key` | Revoke server access |
| POST | `/oauth/admin/clients/{clientId}/rotate-secret` | `X-Admin-Key` | Rotate client secret |

## Usage Example

### Register a client

```bash
# Get a server ID first
SERVER_ID=$(curl -s -H "X-Admin-Key: admin-api-key-change-in-production" \
  http://localhost:5000/oauth/admin/servers | jq -r '.[0].id')

# Register
curl -s -X POST http://localhost:5000/oauth/register \
  -H "Content-Type: application/json" \
  -d "{
    \"client_name\": \"my-app\",
    \"redirect_uris\": [\"https://app.contoso.com/oauth/callback\"],
    \"grant_types\": [\"client_credentials\"],
    \"token_endpoint_auth_method\": \"client_secret_post\",
    \"requested_server_ids\": [\"$SERVER_ID\"],
    \"requested_scopes\": { \"$SERVER_ID\": [\"read\", \"write\"] }
  }"
```

The response includes `client_id`, `client_secret` (returned once), and `registration_access_token`.

### Obtain a token

```bash
curl -s -X POST http://localhost:5000/oauth/token \
  -d "grant_type=client_credentials" \
  -d "client_id=<CLIENT_ID>" \
  -d "client_secret=<CLIENT_SECRET>" \
  -d "server_id=<SERVER_ID>" \
  -d "scope=read"
```

Returns a JWT with `aud` set to the server name and `server_id` in claims.

## Key Design Decisions

- **Server as first-class entity** — enables independent callback whitelists and grant lifecycles per server
- **Callback whitelist per server** — different servers may serve different client populations
- **`ClientServerGrant` as explicit join** — single indexed lookup for "is this client allowed to talk to this server?"
- **Argon2id for secrets** — industry standard; secrets are never stored in plaintext
- **Registration Access Token (RFC 7592)** — clients can self-manage without admin intervention
- **JWT access tokens with `aud` = server name** — MCP servers validate tokens independently
- **`snake_case` JSON** — matches OAuth2 RFC conventions

## Configuration

See `src/Mcpd.Api/appsettings.json` for the full configuration shape including MCP server definitions, callback whitelists, token signing keys, and rate limiting.

## Tests

```bash
dotnet test Mcpd.sln
```

57 tests across 4 projects:

- **Domain** (15) — entity state transitions, value object equality
- **Application** (14) — validator edge cases
- **Infrastructure** (15) — Argon2 round-trip, callback pattern matching, JWT claim validation
- **API Integration** (13) — full registration→token flow, grant enforcement, secret rotation, auth checks

## License

See [LICENSE](LICENSE).
