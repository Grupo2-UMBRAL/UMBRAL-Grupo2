# Identity and Access Bootstrap

La capacidad logica `Identity and Access` se implementa inicialmente con `Keycloak`, no con un servicio `.NET` adicional.

## Artefactos activos

- `infra/keycloak/import/umbral-realm.json`
- `infra/keycloak/verify/verify_auth.py`
- servicio `keycloak` en `docker-compose.yml`
- servicio `auth-smoke-tests` en `docker-compose.utils.yml`

## Alcance actual

- realm `umbral`
- roles `Administrator`, `Operator`, `Participant`
- clients iniciales para `web`, `mobile`, APIs y `edge proxy`
- `client scope` `umbral-api-audiences` para que `umbral-web` y `umbral-mobile` emitan `aud` hacia `mission-design`, `session-operations` y `scoring-audit`
- client `umbral-web-shortlived` para validar expiracion de token dentro de `docker compose`
- usuarios semilla para pruebas locales

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

## Siguiente evolucion

Si luego hace falta encapsular administracion, federation o politicas propias, se puede agregar un host `.NET` delante de `Keycloak` sin renombrar el bounded context.
