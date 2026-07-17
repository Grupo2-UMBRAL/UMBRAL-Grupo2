# Keycloak Auth Verification

Validaciones reproducibles para el bootstrap local de `User Management` usando `docker compose` y el `edge proxy`.

## Escenarios cubiertos

- `login`: verifica que los tokens emitidos para `umbral-web` y `umbral-mobile` incluyan `sub`, las audiencias de las cuatro APIs y funcionen contra `/bootstrap`.
- `invalid`: verifica que un token malformado reciba `401` al invocar una API por el proxy.
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

## Notas

- `login` ahora incluye `user-management` en el set de audiencias esperadas y en la smoke call por `edge proxy`. Si el servicio aun no existe en el worktree, este escenario no puede pasar end-to-end hasta que se agregue `src/services/user-management/UserManagement.Api/`.
