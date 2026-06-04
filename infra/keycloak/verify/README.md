# Keycloak Auth Verification

Validaciones reproducibles para el bootstrap local de `Identity and Access` usando `docker compose` y el `edge proxy`.

## Escenarios cubiertos

- `login`: verifica que los tokens emitidos para `umbral-web` y `umbral-mobile` incluyan `sub`, las audiencias de las cuatro APIs y funcionen contra `/bootstrap`.
- `invalid`: verifica que un token malformado reciba `401` al invocar una API por el proxy.
- `expired`: emite un token desde `umbral-web-shortlived`, espera su expiracion dentro del contenedor y verifica `401`.
- `insufficient-role`: usa un token de `Participant` contra una smoke route administrativa para obtener `403` por falta de rol.

## Comandos

Levantar la pila:

```bash
docker compose --env-file .env.example -f docker-compose.dev.yml up -d
```

Ejecutar todas las validaciones:

```bash
docker compose --env-file .env.example -f docker-compose.dev.yml -f docker-compose.utils.yml run --rm auth-smoke-tests
```

Ejecutar un escenario puntual:

```bash
docker compose --env-file .env.example -f docker-compose.dev.yml -f docker-compose.utils.yml run --rm -e AUTH_SMOKE_SCENARIO=expired auth-smoke-tests
```

## Notas

- El caso `expired` no depende del reloj del host. El contenedor espera internamente hasta que el `exp` del JWT quede atras.
- `login` ahora incluye `identity-access` en el set de audiencias esperadas y en la smoke call por `edge proxy`. Si el servicio aun no existe en el worktree, este escenario no puede pasar end-to-end hasta que se agregue `src/services/identity-access/Umbral.IdentityAccess.Api/`.
