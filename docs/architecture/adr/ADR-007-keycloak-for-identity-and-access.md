# ADR-007: Usar Keycloak para Identity and Access

## Status

Accepted

## Context

UMBRAL necesita autenticación, autorización por roles y permisos, y manejo consistente de credenciales para web y móvil. El proyecto ya requiere tokens `JWT` y separación clara entre identidad y dominio operativo.

Resolver identidad dentro de la aplicación agregaría complejidad innecesaria y duplicaría capacidades que ya pertenecen a un proveedor especializado.

## Decision

- `Keycloak` será el proveedor de identidad y acceso del proyecto.
- `Keycloak` administrará usuarios, credenciales, roles y permisos.
- La aplicación consumirá tokens `JWT` emitidos por `Keycloak`.
- El backend autorizará acciones usando claims del token.

## Consequences

- La autenticación queda externalizada respecto al dominio central.
- `Identity and Access` modela consumo y uso de identidad, no implementación casera de un proveedor de auth.
- La app web y la app móvil deben integrarse con el flujo de autenticación de `Keycloak`.
- Los roles y permisos deben definirse de forma consistente entre `Keycloak` y el modelo operativo de la aplicación.
