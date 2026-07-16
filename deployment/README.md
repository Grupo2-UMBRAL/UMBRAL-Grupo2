# UMBRAL demo deployment

This directory contains the deployment contract for the academic demo. It is separate from
`docker-compose.dev.yml`, which remains the local developer environment.

## Topology contract

- The only public entry point is the Edge Proxy.
- Web is served behind the Edge Proxy fallback route.
- Keycloak is internal but is presented to browsers at `${DEMO_PUBLIC_BASE_URL}/auth`.
- APIs and RabbitMQ have no public ingress.
- Neon hosts PostgreSQL externally over TLS; RabbitMQ state is ephemeral, and the services
  redeclare their exchanges and queues on connect.
- The mobile application remains local and points at `DEMO_PUBLIC_BASE_URL`.

## Files

- `.env.demo.example`: public template and list of required secrets.
- `docker-compose.demo.yml`: production-like local rehearsal.
- `docker/`: production container definitions used by the rehearsal and Azure builds.
- `azure/`: Bicep templates and example parameters for Azure Container Apps.
- `docs/`: technical documentation for the deployment.

## Local rehearsal

Copy `.env.demo.example` to `.env.demo`, replace every `REPLACE_...` value, and use a
locally reachable HTTPS hostname for `DEMO_PUBLIC_BASE_URL`. A plain `localhost` origin is
intentionally not part of the demo template because it would not exercise the production
issuer and redirect contract.

From the repository root, run the rehearsal with:

```powershell
docker compose --env-file deployment/.env.demo -f deployment/docker-compose.demo.yml up --build
```

Stop it and remove the local rehearsal volumes with:

```powershell
docker compose --env-file deployment/.env.demo -f deployment/docker-compose.demo.yml down --volumes
```

## Azure deployment

The GitHub Actions workflow `.github/workflows/deploy-demo-container-apps.yml` deploys from
`develop` after the `Validation` workflow completes successfully. It can also be started
manually for a controlled demo run.

Configure these GitHub repository variables:

- `AZURE_LOCATION`
- `AZURE_RESOURCE_GROUP`
- `AZURE_CONTAINER_APP_ENVIRONMENT`
- `AZURE_ACR_NAME`
- `DEMO_PUBLIC_BASE_URL`

Configure these GitHub repository secrets:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`
- `DEMO_NEON_HOST`
- `DEMO_NEON_DATABASE`
- `DEMO_NEON_USER`
- `DEMO_NEON_PASSWORD`
- `DEMO_RABBITMQ_PASSWORD`
- `DEMO_KEYCLOAK_ADMIN_PASSWORD`
- `DEMO_USER_MANAGEMENT_KEYCLOAK_ADMIN_PASSWORD`
- `NEXT_PUBLIC_GOOGLE_MAPS_API_KEY` when map features are enabled

The workflow runs Bicep twice. The bootstrap pass creates the resource group contents and
ACR without Container Apps, because images cannot be pushed before the registry exists. After
the SHA-tagged images are pushed, the second pass creates or updates the Container Apps.

`DEMO_PUBLIC_BASE_URL` must be the final HTTPS edge URL before building images. The Keycloak
realm import and the web bundle both bake this public URL into browser-visible configuration.

The Neon values are repository secrets. Do not use a connection-string secret: pass its host,
database, user and password through the four dedicated values so Bicep can build the Keycloak
JDBC URL and each service connection string without logging credentials.

Browser OTLP telemetry is disabled in the deployed Web image unless `VITE_OTLP_ENDPOINT` is set
at build time to a reachable collector origin. This prevents browsers from attempting to export
telemetry to `localhost` in the public demo.
