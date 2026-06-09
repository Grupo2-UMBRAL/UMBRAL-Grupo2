# Chore 07: Scoring Audit Boundary Cleanup

## Why This Slice Exists

`scoring-audit` parece siguiente candidato natural. Aun tiene imports directos `Application -> Infrastructure`, pero el scope visible es mas chico que `session-operations` y buen lugar para repetir patron simple validado en `mission-design`.

## Handoff From Chore 06

- `mission-design` dejo de importar `Infrastructure.MissionDesignDbContext` desde handlers de `Application`.
- Se introdujo `IMissionDesignDbContext` como seam pequeno en `Application`, sin meter repositories genericos.
- `MissionDesignDbContext` ahora implementa esa interfaz y `Program.cs` hace binding DI al concreto.
- El patron sirve cuando handler necesita seguir usando LINQ/`DbSet`, pero conviene cortar coupling nominal con `Infrastructure`.
- Validacion `./scripts/Invoke-RepositoryValidation.ps1 -Scope Backend -SkipComposeSmoke`: builds pasaron; tests quedaron bloqueados porque host solo tiene runtime `10.0.8` y falta `Microsoft.NETCore.App 8.0.0` para `testhost.exe`.
- Para siguiente slice, no asumir rojo funcional si vuelve a fallar igual: primero distinguir fallo de codigo vs fallo de host runtime.

## Goal

Reducir dependencias directas de `Application` hacia `Infrastructure` en `scoring-audit`, manteniendo casos de uso pequenos y sin sobre-abstraer.

## Candidate Areas

- `Application/Rankings/GetRanking.cs`
- `Application/Audit/GetSessionEventLog.cs`
- `Application/Audit/LogSessionEvent.cs`
- `Application/Scoreboards/RecordStageCredit.cs`

## Inputs

- [src/services/scoring-audit/CONTEXT.md](../src/services/scoring-audit/CONTEXT.md)
- [src/services/scoring-audit/Umbral.ScoringAudit.Api/Application](../src/services/scoring-audit/Umbral.ScoringAudit.Api/Application)
- [src/services/scoring-audit/Umbral.ScoringAudit.Api/Infrastructure](../src/services/scoring-audit/Umbral.ScoringAudit.Api/Infrastructure)
- tests bajo `src/services/scoring-audit/Umbral.ScoringAudit.Api.Tests`

## Deliverables

1. Los handlers priorizados dejan de importar `Infrastructure` directamente.
2. Los seams nuevos son concretos y chicos.
3. No se agregan repositories genericos si una interfaz de contexto o puerto puntual basta.

## Constraints

- No mezclar con nuevos cambios de `mission-design`.
- No mover contratos realtime o HTTP si no son parte del problema real.
- No convertir CRUD/query simple en ceremonia innecesaria.

## Recommended Approach

1. Confirmar si basta un seam tipo contexto, como en `mission-design`, o si algun caso necesita puerto mas fino.
2. Priorizar primero handlers que combinan persistencia con efectos externos.
3. Mantener nombres en lenguaje ubicuo: `Scoreboard`, `Ranking`, `Session Event Log`, `Penalty`, `Stage Credit`.

## Suggested Acceptance Checks

- Los handlers tocados ya no importan `Infrastructure`.
- Los tests siguen describiendo comportamiento observable del bounded context.
- El diff queda chico y revisable.
