# Chore 05: Session Operations Boundary Cleanup

## Why This Slice Exists

`session-operations` es el bounded context con mas mezcla de concerns: `DbContext`, integraciones HTTP, identidad actual, realtime y casos de uso viven muy cerca en `Application`. Es el siguiente candidato natural despues del patron ejemplar de `scoring-audit`.

## Goal

Reducir acoplamiento entre `Application` e `Infrastructure` en `session-operations`, empezando por los casos de uso con mas mezcla de dependencias.

## Candidate Areas

- `Application/LiveSessions/CreateLiveSession.cs`
- `Application/Penalties/ApplyPenalty.cs`
- `Application/EvidenceSubmissions/*`
- `Application/Hints/ReleaseHint.cs`
- `Application/SessionLifecycle/TransitionLiveSessionState.cs`

## Inputs

- [src/services/session-operations/CONTEXT.md](../src/services/session-operations/CONTEXT.md)
- [src/services/session-operations/Umbral.SessionOperations.Api/Application](../src/services/session-operations/Umbral.SessionOperations.Api/Application)
- [src/services/session-operations/Umbral.SessionOperations.Api/Infrastructure](../src/services/session-operations/Umbral.SessionOperations.Api/Infrastructure)
- tests bajo `src/services/session-operations/Umbral.SessionOperations.Api.Tests`

## Deliverables

1. Los casos de uso priorizados dejan de depender directamente de adapters HTTP, realtime o `DbContext` cuando esa dependencia mezcla demasiadas responsabilidades.
2. Las colaboraciones externas quedan modeladas como puertos finos en `Application`.
3. Los adapters concretos quedan en `Infrastructure`.
4. Hubs o contratos realtime que correspondan al borde quedan encaminados hacia `Presentation`.

## Constraints

- No reescribir todo `session-operations` en un solo prompt.
- No introducir repositorios genericos globales si el problema es local a un use case.
- No mezclar cambios de bounded contexts distintos.

## Recommended Approach

1. Elegir uno o dos casos de uso de alto valor por prompt.
2. Extraer primero integraciones externas y notificaciones.
3. Luego reducir dependencia directa de `DbContext` donde el handler ya mezcla demasiadas cosas.
4. Mantener el vocabulario del `CONTEXT.md`.

## Suggested Acceptance Checks

- Los handlers tocados ya no importan `Infrastructure`.
- La logica de Session Operations sigue clara en lenguaje ubicuo.
- Los tests del slice cubren comportamiento observable y no solo wiring.

## Output Format For The Agent

- casos de uso tocados
- puertos nuevos
- adapters nuevos o movidos
- evidencia de validacion local

## Can Be Combined With

- No combinar con `mission-design`.
- Dividir en varios prompts pequenos si el primer diff supera un slice revisable.

## Handoff From Chore 04

- El `edge-proxy` queda documentado como entrada publica preferida para clientes locales en `http://localhost:7500`.
- La frontera de auth no se movio al gateway: cada servicio sigue configurando `Auth:Authority`, `Auth:Audience`, `UseAuthentication()` y `UseAuthorization()` desde `src/shared/Umbral.ServiceDefaults`.
- `docs/architecture/authorization-matrix.md` ahora explicita que la matriz se sigue aplicando dentro de cada backend despues del enrutamiento del proxy.
- Hay una prueba nueva en `src/shared/Umbral.ServiceDefaults.Tests/JwtBearerConfigurationTests.cs` que deja evidencia de que el wiring compartido sigue validando `authority` y `audience` por servicio.
- Para el chore 05 conviene preservar esa frontera: si extraes puertos desde `Application`, no metas decisiones de autorizacion ni parsing de identidad fuera del borde del servicio.
