# Tech Plan: API-Key Authentication for the MCP HTTP Server

## Requirement

As an MCP server user, I can provide an API key in an HTTP header so that
only clients holding the administrator-configured key can access the MCP
server's tools and data.

- The administrator supplies the key through configuration, including an
  environment variable or another .NET configuration provider.
- MCP clients connecting over HTTP must send the key in a request header.

## Current state

- `src/FileRepo.Mcp/Program.cs` hosts either stdio or stateless Streamable HTTP
  based on `Mcp:Transport`; stdio is the default.
- HTTP MCP is mapped at `Mcp:HttpPath` (default `/mcp`) with no authentication
  middleware or authorization policy.
- The HTTP server uses the same tools and services as stdio. Those tools can
  read and update the shared vector index, so an exposed HTTP endpoint grants
  access to the server's configured data.
- `scripts/start-mcp-http.sh` binds to `0.0.0.0` by default and sets the
  transport to HTTP. It currently has no authentication configuration.
- `FileRepo.Mcp/appsettings.json` is tracked and contains non-secret server
  settings. It must not receive a real API key.

## Design decisions

| Topic | Decision |
|---|---|
| Header | Require `X-API-Key` on every HTTP request to the MCP endpoint. Do not accept credentials in a URL or request body. |
| Credential | One shared, administrator-managed secret for this server instance. It grants the same MCP tool access to every holder; it does not identify individual users. |
| Configuration | Read `Mcp:ApiKey` through the normal .NET configuration system. Environment variable form: `Mcp__ApiKey`. Other providers, such as deployment secret stores or .NET user secrets, can supply the same setting. Do not check a real key into source control or log it. |
| Missing configuration | Fail startup when `Mcp:Transport` is `http` and the key is unset, empty, or whitespace. Do not silently run an unauthenticated HTTP server. The stdio transport does not require this setting. |
| Authentication boundary | Protect every HTTP method/request under the configured MCP path, including initialization, tool calls, notifications, and Streamable HTTP GET/SSE requests. Leave stdio and the separate `FileRepo.Web` REST API unchanged. |
| Failure response | Missing, duplicated, empty, or incorrect header returns HTTP `401 Unauthorized` before MCP handling. Do not reveal the configured key or whether a supplied key was close to correct. |
| Transport security | Require HTTPS for non-loopback/network deployments, normally terminated by a trusted reverse proxy. A static API key sent over plain HTTP is not secure in transit. |
| Scope limits | No per-user accounts, roles, OAuth, key persistence, rate limiting, or automatic rotation in this change. Rotate by updating the configured secret and restarting/redeploying. |

## Implementation

### 1. Configuration and startup validation

- Add an MCP authentication options type or equivalent validation for
  `Mcp:ApiKey`.
- Bind and validate the setting only for the HTTP transport. Preserve the
  existing stdio startup path and its stdout-only-for-MCP behavior.
- Validate before accepting requests; reject whitespace-only configuration.
  Do not supply a development default.
- Keep the value out of tracked configuration. Document the setting and
  environment-variable spelling; administrators may also use any configured
  .NET secret provider.

Example for a local shell:

```bash
export Mcp__ApiKey='replace-with-a-long-random-secret'
./scripts/start-mcp-http.sh
```

For deployment, inject the secret through the platform's secret-management
facility rather than a committed settings file or command-line argument.

### 2. HTTP request authentication

- Add a small middleware or endpoint filter in the HTTP branch of
  `src/FileRepo.Mcp/Program.cs`, before `MapMcp` handles requests.
- Scope protection to the configured `Mcp:HttpPath` and its path descendants;
  do not accidentally authenticate unrelated middleware endpoints or apply
  this logic to stdio.
- Require exactly one non-empty `X-API-Key` value and compare it to the
  configured key using a constant-time byte comparison. Reject invalid input
  before invoking MCP handlers.
- Return `401 Unauthorized` for all credential failures. Avoid logging the
  supplied or configured key; if failures are logged, log only a generic
  event without credential contents.
- Ensure auth applies uniformly across all HTTP methods supported by
  Streamable HTTP. Clients must attach the header to every request, not only
  initialization.
- Do not add CORS as a substitute for authentication. If a browser client is
  later supported, CORS policy is a separate deployment decision.

### 3. HTTP launch and client documentation

- Update `scripts/start-mcp-http.sh` documentation/help context to make clear
  that `Mcp__ApiKey` is required. Keep the script from printing or forwarding
  the key in arguments.
- Update the MCP section of `README.md` with:
  - how to configure the key;
  - that HTTP MCP requests use `X-API-Key`;
  - a client configuration example for clients that support custom headers;
  - the requirement to use HTTPS when connecting across a network;
  - the fact that stdio is unaffected.
- Do not put a usable key in `src/FileRepo.Mcp/appsettings.json` or any
  checked-in example.

### 4. Tests

Add focused coverage using the existing NUnit conventions, preferably in a
dedicated MCP test project if needed to host the ASP.NET Core application:

- HTTP transport refuses to start when the key is missing, empty, or
  whitespace.
- Stdio transport starts without an API key and retains its current behavior.
- Missing, empty, duplicated, and incorrect `X-API-Key` values return 401 and
  do not reach MCP handling.
- A correct key reaches the MCP endpoint.
- Authentication covers POST and any supported GET/SSE request paths,
  including nested paths under a custom `Mcp:HttpPath`.
- Requests outside the MCP path are not affected by the MCP auth middleware.
- No failure response or log output includes either key value.

Where middleware-level tests cannot exercise protocol handling cheaply, use a
small test endpoint behind the same middleware for path/method behavior and
add a focused HTTP smoke test with an MCP client or MCP Inspector.

## Acceptance criteria

- HTTP MCP startup fails clearly if no non-empty `Mcp:ApiKey` is configured.
- Every HTTP request handled under the MCP path requires a matching
  `X-API-Key`; missing or invalid credentials receive 401 without reaching a
  tool.
- Valid credentials permit normal MCP initialization and tool calls.
- Stdio MCP continues to work without an API key.
- Credentials are not committed, included in URLs, echoed in responses, or
  written to logs.
- README explains configuration, client header setup, and HTTPS deployment.
- Targeted tests and the solution build pass.

## Rollout and risks

- This is a breaking change for existing HTTP MCP clients: they must configure
  the header, and HTTP deployments must configure a server-side key before
  starting. Stdio clients are unaffected.
- A single shared key is simple to deploy but cannot provide individual
  attribution or per-user revocation. Anyone who obtains the key has access to
  every tool available through this MCP server.
- If the server is reachable over an untrusted network without HTTPS, the key
  can be intercepted. Deployment guidance must make the TLS requirement
  explicit.
- The launch script binds to all interfaces by default, making explicit
  configuration and HTTPS especially important. Consider a later, separately
  approved change to bind to loopback by default; changing that default is not
  required for this implementation.

## Task order

1. Add HTTP-only key configuration and fail-fast startup validation.
2. Add path-scoped request authentication before MCP endpoint handling.
3. Add tests for startup, valid/invalid credentials, methods, and path scope.
4. Document environment/configuration and client header setup; clarify HTTPS.
5. Build and run targeted tests, then verify stdio and authenticated HTTP
   behavior.
