# Edge Proxy

`edge-proxy` is the preferred public entry for local web and mobile clients.

## Responsibilities

- expose a single local base URL for client traffic
- route requests to bounded-context services and `Keycloak`
- keep cross-origin setup at the edge for browser clients
- expose coverage artifacts and simple health metadata for local workflows

## Non-goals

- do not own business authorization
- do not replace downstream `JWT` validation
- do not move domain rules out of the backend services

## Route map

- `/auth/*` -> `Keycloak`
- `/identity-access/*` -> `Identity and Access`
- `/mission-management/*` -> `Mission Management`
- `/session-management/*` -> `Session Operations`
- `/session-hub/*` -> `Session Operations` realtime hub
- `/scoring-monitoring/*` -> `Scoring and Monitoring`

## Trust model

Clients should authenticate against `Keycloak` through the proxy-facing `/auth/*` routes when they are outside the Docker network.

Each backend service still configures its own `Auth:Authority`, `Auth:Audience`, `RequireHttpsMetadata`, `UseAuthentication()`, and `UseAuthorization()`. The proxy forwards traffic, but it does not become the only trust boundary.
