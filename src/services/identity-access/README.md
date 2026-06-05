# Identity and Access Bootstrap

La capacidad `Identity and Access` usa `Keycloak` como proveedor de identidad local y expone un servicio propio detras del `edge proxy` para flujos del bounded context.

## Artefactos activos

- `infra/keycloak/import/umbral-realm.json`
- `infra/keycloak/docker/identity-access.Dockerfile`
- `infra/keycloak/verify/verify_auth.py`
- servicio `keycloak` en `docker-compose.dev.yml`
- servicio `identity-access-service` en `docker-compose.dev.yml`
- servicio `auth-smoke-tests` en `docker-compose.utils.yml`
- ruta `/identity-access/*` en `src/apps/edge-proxy/Umbral.EdgeProxy/appsettings.json`

## Alcance actual

- realm `umbral`
- roles `Administrator`, `Operator`, `Participant`
- clients iniciales para `web`, `mobile`, APIs y `edge proxy`
- `client scope` `umbral-api-audiences` para que `umbral-web` y `umbral-mobile` emitan `aud` hacia `identity-access`, `mission-design`, `session-operations` y `scoring-audit`
- `client` confidencial `umbral-identity-access-api` y su audience mapper dentro de `umbral-api-audiences`
- client `umbral-web-shortlived` para validar expiracion de token dentro de `docker compose`
- usuarios semilla para pruebas locales
- puerto local `IDENTITY_ACCESS_PORT=7100`

## Contrato local esperado del servicio

- contenedor `identity-access-service` escuchando en `8080`
- audience configurada como `umbral-identity-access-api`
- base path autenticada esperada por las smokes: `/api/identity-access/*`
- bootstrap smoke esperada: `/api/identity-access/bootstrap`
- endpoints de Operator Users:
  - `GET /api/identity-access/operators`
  - `POST /api/identity-access/operators`
  - `POST /api/identity-access/operators/{userId}/deactivate`
  - `POST /api/identity-access/operators/{userId}/reset-password`
- payload de provision: `username`, `email`, `firstName`, `lastName`, `password`

## Configuracion requerida para admin facade

El servicio usa credenciales admin de `Keycloak` para listar, crear y desactivar `Operator Users`.

- `IdentityAccess__Keycloak__BaseUrl`
- `IdentityAccess__Keycloak__Realm`
- `IdentityAccess__Keycloak__AdminRealm`
- `IdentityAccess__Keycloak__AdminClientId`
- `IdentityAccess__Keycloak__AdminUsername`
- `IdentityAccess__Keycloak__AdminPassword`

En `docker-compose.dev.yml` esas variables salen de `.env.example` con defaults locales para `Keycloak`.

## Verificacion local reproducible

Levantar el entorno:

```bash
docker compose --env-file .env.example -f docker-compose.dev.yml up -d
```

Ejecutar todas las validaciones:

```bash
docker compose --env-file .env.example -f docker-compose.dev.yml -f docker-compose.utils.yml run --rm auth-smoke-tests
```

Ejecutar un escenario puntual:

```bash
docker compose --env-file .env.example -f docker-compose.dev.yml -f docker-compose.utils.yml run --rm -e AUTH_SMOKE_SCENARIO=invalid auth-smoke-tests
```

Escenarios disponibles:

- `login`
- `invalid`
- `expired`
- `insufficient-role`

Detalles operativos en `infra/keycloak/verify/README.md`.

## Estado de esta branch

La branch ya incluye host `Umbral.IdentityAccess.Api`, rutas autenticadas para administracion de `Operator Users`, tests de aplicacion y wiring de `edge-proxy` + `docker compose`. Falta validar todo el slice con `docker compose config`, `dotnet test` y smokes/lint segun disponibilidad del entorno.
