# Identity and Access Bootstrap

La capacidad logica `Identity and Access` se implementa inicialmente con `Keycloak`, no con un servicio `.NET` adicional.

## Artefactos activos

- `infra/keycloak/import/umbral-realm.json`
- servicio `keycloak` en `docker-compose.yml`

## Alcance actual

- realm `umbral`
- roles `Administrator`, `Operator`, `Participant`
- clients iniciales para `web`, `mobile`, APIs y `edge proxy`
- usuarios semilla para pruebas locales

## Siguiente evolucion

Si luego hace falta encapsular administracion, federation o politicas propias, se puede agregar un host `.NET` delante de `Keycloak` sin renombrar el bounded context.
