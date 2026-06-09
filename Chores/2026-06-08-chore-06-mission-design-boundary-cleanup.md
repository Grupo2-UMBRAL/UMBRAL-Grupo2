# Chore 06: Mission Design Boundary Cleanup

## Why This Slice Exists

`mission-design` tambien tiene dependencias directas desde `Application` hacia `Infrastructure`, pero en general parece menos enredado que `session-operations`. Conviene limpiarlo despues de fijar el patron en otros slices.

## Handoff From Chore 05

- `session-operations` dejo de hablar directo con `IHubContext` desde los handlers de `Application`.
- Se introdujo `ISessionRealtimeNotifier` en `Application` y `SignalRLiveSessionRealtimeNotifier` en `Infrastructure`.
- `LiveSessionStateChangedEvent` ahora lleva `SequenceNumber` y `Reason`.
- Los tests de `session-operations` usan fakes del notifier en vez de `RecordingHubContext`.
- La validacion local quedo bloqueada por restore de NuGet en este entorno; hay que rerunear `dotnet test` o el gate repo cuando haya acceso a dependencias.

## Goal

Reducir dependencias directas de `Application` hacia `Infrastructure` en `mission-design` sin sobre-ingenierizar handlers CRUD simples.

## Candidate Areas

- `Application/Missions/CreateMission.cs`
- `Application/Missions/ListMissions.cs`
- `Application/Missions/GetEligibleMissionForLiveSession.cs`
- `Application/MissionStages/*`

## Inputs

- [src/services/mission-design/CONTEXT.md](../src/services/mission-design/CONTEXT.md)
- [src/services/mission-design/Umbral.MissionDesign.Api/Application](../src/services/mission-design/Umbral.MissionDesign.Api/Application)
- [src/services/mission-design/Umbral.MissionDesign.Api/Infrastructure](../src/services/mission-design/Umbral.MissionDesign.Api/Infrastructure)
- tests bajo `src/services/mission-design/Umbral.MissionDesign.Api.Tests`

## Deliverables

1. Los handlers priorizados dejan de importar `Infrastructure` directamente cuando esa dependencia ya erosiona el boundary.
2. Se usan puertos o seams concretos solo donde aporten claridad y testabilidad real.
3. Se conserva la simplicidad de los casos que aun no justifican mas abstraccion.

## Constraints

- No convertir este slice en una cruzada de repositories para todo.
- No cambiar el modelo de dominio salvo que aparezca una inconsistencia real.
- No mezclar este trabajo con moves grandes de carpeta.

## Recommended Approach

1. Priorizar primero los handlers usados por otros servicios o que ya cargan demasiada responsabilidad.
2. Mantener consultas simples como consultas simples si no estan causando dolor estructural serio.
3. Extraer seams pequenos, con nombres de dominio y no de framework.

## Suggested Acceptance Checks

- Los handlers tocados reducen o eliminan imports a `Infrastructure`.
- El bounded context sigue usando vocabulario correcto de `Mission`, `Mission Node`, `Mission Stage`, `Hint`.
- Los tests siguen describiendo comportamiento del contexto y no detalles de EF.

## Output Format For The Agent

- handlers refactorizados
- seams nuevos
- justificacion de por que algunos handlers simples se dejaron para mas adelante
- evidencia de tests

## Can Be Combined With

- No combinar con `session-operations`.
- Puede dividirse por subarea (`Missions` y `MissionStages`) si el prompt queda demasiado ancho.
