# Edge Proxy

`edge-proxy` is the preferred public entry for local web and mobile clients.

## Responsibilities

- expose a single local base URL for client traffic
- route requests to bounded-context services and `Keycloak`
- keep cross-origin setup at the edge for browser clients
- serve a local **developer hub** at `/` that centralizes links to every runtime resource
- serve the backend coverage HTML report at `/coverage` when it has been generated

## Developer hub (local only)

Opening `http://localhost:7500/` renders an HTML hub linking to:

- per-service Swagger and health (`/mission-management/swagger`, `/*/health`, ...)
- Keycloak admin (`/auth/admin/`) and OIDC discovery
- RabbitMQ management UI (`Hub:RabbitMqManagementUrl`, default `http://localhost:16672`)
- Aspire dashboard (`Hub:AspireDashboardUrl`, default `http://localhost:19888`)
- the coverage report (`/coverage/`) — served via static files from `Hub:CoverageReportPath`

The coverage report is produced on the host by `scripts/Publish-BackendCoverageReports.ps1`
into `temp/validation/backend-coverage-report`, which `docker-compose.dev.yml` mounts read-only
into the edge container at `/coverage-report`. The card shows as disabled until the report exists.
Machine-readable edge metadata stays available at `/edge-info`.

These conveniences are for the local development/delivery environment only.

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
