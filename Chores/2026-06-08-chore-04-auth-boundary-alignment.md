# Chore 04: Auth Boundary Alignment

## Handoff From Chore 03

- `Umbral.ServiceDefaults` y sus tests ya no viven bajo `src/services`; fueron movidos a `src/shared/Umbral.ServiceDefaults` y `src/shared/Umbral.ServiceDefaults.Tests`.
- `identity-access`, `mission-design`, `session-operations` y `scoring-audit` ya referencian el proyecto compartido desde `src/shared`, asi que este chore no necesita seguir arrastrando rutas legacy hacia `building-blocks`.
- `scripts/Invoke-RepositoryValidation.ps1`, `docs/architecture/validation-pipeline.md`, `docs/architecture/repo-structure.md` y `src/services/README.md` ya quedaron alineados con `src/shared`.
- Follow-up detectado: `Umbral.ServiceDefaults` sigue agrupando auth boundary helpers, exception mapping y bootstrap tecnico en un solo paquete. No se partio ahora para mantener el diff mecanico, pero conviene que este chore evite ampliar ese paquete sin clarificar ownership.

## Why This Slice Exists

El refactor quiere consolidar `edge-proxy` como entrada publica unica, pero sin degradar la seguridad ni contradecir la matriz de autorizacion ya definida en el repo.

## Goal

Alinear gateway y servicios para que el borde de auth sea claro: el gateway simplifica el acceso de clientes, pero cada servicio sigue validando identidad y reglas de autorizacion.

## Inputs

- [src/apps/edge-proxy/Umbral.EdgeProxy/Program.cs](../src/apps/edge-proxy/Umbral.EdgeProxy/Program.cs)
- [src/apps/edge-proxy/Umbral.EdgeProxy/appsettings.json](../src/apps/edge-proxy/Umbral.EdgeProxy/appsettings.json)
- [docs/architecture/authorization-matrix.md](../docs/architecture/authorization-matrix.md)
- [docs/architecture/local-infrastructure.md](../docs/architecture/local-infrastructure.md)
- [docs/architecture/infrastructure-outline.md](../docs/architecture/infrastructure-outline.md)

## Deliverables

1. El repo deja claro por docs o config que `edge-proxy` es la entrada publica preferida.
2. Los servicios siguen manteniendo validacion JWT, audience, issuer y autorizacion por request.
3. Si hay wiring repetido o confuso de auth, se documenta o simplifica sin romper ese principio.

## Constraints

- No quitar validacion JWT de los servicios.
- No mover autorizacion de negocio al gateway.
- No introducir un sistema nuevo de internal tokens en este slice.

## Recommended Approach

1. Revisar y aclarar la documentacion operativa y de arquitectura.
2. Verificar si el proxy necesita pequenos ajustes de naming o comments para reflejar su rol.
3. Dejar el repo preparado para que clientes vayan por gateway sin cambiar el trust model interno.

## Suggested Acceptance Checks

- La decision no contradice [authorization-matrix.md](../docs/architecture/authorization-matrix.md).
- Ningun servicio queda en modo trust-only hacia el proxy.
- El gateway no absorbe reglas de dominio ni de scope.

## Output Format For The Agent

- resumen de cambios de docs o configuracion
- confirmacion explicita de que los servicios siguen validando JWT
- riesgos abiertos si aparece un problema de `issuer` o `audience`

## Can Be Combined With

- Puede combinarse con pequenos ajustes de `repo-structure.md` solo si `Chore 01` ya dejo el target escrito.
- No combinar con refactors grandes de handlers.
