# Sample MCP Server

A minimal MCP Resource Server that validates JWTs issued by Mcpd (the Authorization Server).

## Architecture

```
MCP Client ──► Mcpd (AS) ──► External IdP (e.g., Entra ID, Okta)
    │               │
    │  access_token  │
    ▼               ▼
Sample MCP Server   JWKS endpoint
(Resource Server)   (public keys)
```

## Running

```bash
# Start Mcpd (Authorization Server) on port 5000
cd ../../
dotnet run --project src/Mcpd.Api

# Start Sample MCP Server on port 5100
cd samples/SampleMcpServer
dotnet run
```

## Full OAuth 2.1 Flow

### 1. Discovery

```bash
# MCP client discovers the Resource Server's authorization server
curl http://localhost:5100/.well-known/oauth-protected-resource

# MCP client fetches AS metadata
curl https://dcr.contoso.com/.well-known/oauth-authorization-server

# MCP client fetches public keys for token verification
curl https://dcr.contoso.com/.well-known/jwks.json
```

### 2. Dynamic Client Registration (RFC 7591)

```bash
curl -X POST https://dcr.contoso.com/oauth/register \
  -H "Content-Type: application/json" \
  -d '{
    "client_name": "My MCP Client",
    "redirect_uris": ["http://localhost:3000/callback"],
    "grant_types": ["authorization_code"],
    "token_endpoint_auth_method": "client_secret_post",
    "requested_server_ids": ["<server-id>"],
    "requested_scopes": { "<server-id>": ["read", "write"] }
  }'
```

### 3. Authorization (OAuth 2.1 + PKCE)

```bash
# Generate PKCE code verifier and challenge
CODE_VERIFIER=$(openssl rand -base64 32 | tr -d '=+/' | head -c 43)
CODE_CHALLENGE=$(echo -n "$CODE_VERIFIER" | openssl dgst -sha256 -binary | base64 | tr -d '=' | tr '+/' '-_')

# Open in browser - user authenticates at IdP, gets redirected back with code
open "https://dcr.contoso.com/oauth/authorize?\
response_type=code&\
client_id=<client_id>&\
redirect_uri=http://localhost:3000/callback&\
code_challenge=$CODE_CHALLENGE&\
code_challenge_method=S256&\
scope=read write&\
state=random-state-value"
```

### 4. Token Exchange

```bash
# Exchange authorization code for tokens
curl -X POST https://dcr.contoso.com/oauth/token \
  -d "grant_type=authorization_code" \
  -d "code=<authorization_code>" \
  -d "redirect_uri=http://localhost:3000/callback" \
  -d "client_id=<client_id>" \
  -d "code_verifier=$CODE_VERIFIER"
```

Response:
```json
{
  "access_token": "eyJ...",
  "token_type": "Bearer",
  "expires_in": 3600,
  "refresh_token": "...",
  "scope": ["read", "write"]
}
```

### 5. Call MCP Server Tools

```bash
# Echo tool
curl -X POST http://localhost:5100/tools/echo \
  -H "Authorization: Bearer <access_token>" \
  -H "Content-Type: application/json" \
  -d '{"message": "Hello from MCP!"}'

# Time tool
curl -X POST http://localhost:5100/tools/time \
  -H "Authorization: Bearer <access_token>"
```

### 6. Refresh Token

```bash
curl -X POST https://dcr.contoso.com/oauth/token \
  -d "grant_type=refresh_token" \
  -d "refresh_token=<refresh_token>" \
  -d "client_id=<client_id>"
```

### Client Credentials Flow (machine-to-machine)

```bash
curl -X POST https://dcr.contoso.com/oauth/token \
  -d "grant_type=client_credentials" \
  -d "client_id=<client_id>" \
  -d "client_secret=<client_secret>" \
  -d "server_id=<server_id>" \
  -d "scope=read"
```

## Protected Endpoints

| Endpoint | Method | Required Scope | Description |
|----------|--------|---------------|-------------|
| `/tools/echo` | POST | `read` | Echoes the provided message |
| `/tools/time` | POST | `read` | Returns current UTC time |
| `/.well-known/oauth-protected-resource` | GET | none | Resource metadata (RFC 9728) |

## Token Validation

The server validates JWTs by:
1. Fetching the JWKS from Mcpd's `/.well-known/jwks.json`
2. Verifying the RSA signature
3. Checking issuer matches Mcpd
4. Checking audience matches this server's name
5. Checking token is not expired
6. Checking required scopes are present
