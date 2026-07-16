# Edge Proxy

`edge-proxy` is the preferred public entry for web and mobile clients, and the only container app
with public ingress in the Azure deployment.

## Responsibilities

- expose a single base URL for client traffic
- route requests to bounded-context services and `Keycloak`
- keep cross-origin setup at the edge for browser clients
- serve the unified API reference at `/swagger`, the directory of the stack
- serve the backend coverage HTML report at `/coverage` when it has been generated

## API reference (`/swagger`)

`/` redirects to `/swagger`, one Scalar page listing the four service documents. Each document is
fetched back through this same host, which keeps it same-origin. This is the entry point in both
environments: the code path does not branch on `ASPNETCORE_ENVIRONMENT`, so the deployed demo
behaves like the local stack.

## Forwarded headers

The edge calls `UseForwardedHeaders` before anything reads the request scheme. The Azure Container
Apps ingress terminates TLS and forwards plain http, so without it the edge would see
`Request.Scheme == "http"` and YARP's `"X-Forwarded": "Set"` transform would overwrite the
ingress's `X-Forwarded-Proto: https`. Services build their OpenAPI `servers[]` from that header, so
the deployed reference would advertise `http://` URLs and the browser would block them as mixed
content.

## Coverage report (local only)

`scripts/Publish-BackendCoverageReports.ps1` produces the report on the host into
`temp/validation/backend-coverage-report`, which `docker-compose.dev.yml` mounts read-only into the
edge container at `/coverage-report`. This needs no environment check: `Hub:CoverageReportPath` is
only set by compose, so a deployed edge falls back to the empty default and serves nothing.

Machine-readable edge metadata stays available at `/edge-info`.

## Observability

Locally, services export OTLP to the Aspire dashboard (`http://localhost:19888`). It is **not**
deployed and must not be routed through the edge: it runs with
`DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS`, and the edge is the only public door. In Azure,
Container Apps ships container logs to the Log Analytics workspace declared in
`deployment/azure/main.bicep`.

## Non-goals

- do not own business authorization
- do not replace downstream `JWT` validation
- do not move domain rules out of the backend services

## Route map

- `/auth/*` -> `Keycloak`
- `/user-management/*` -> `User Management`
- `/mission-management/*` -> `Mission Management`
- `/session-management/*` -> `Session Operations`
- `/session-hub/*` -> `Session Operations` realtime hub
- `/scoring-monitoring/*` -> `Scoring and Monitoring`

## Trust model

Clients should authenticate against `Keycloak` through the proxy-facing `/auth/*` routes when they are outside the Docker network.

Each backend service still configures its own `Auth:Authority`, `Auth:Audience`, `RequireHttpsMetadata`, `UseAuthentication()`, and `UseAuthorization()`. The proxy forwards traffic, but it does not become the only trust boundary.
