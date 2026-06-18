# Authorization Matrix

`UMB-31` moves authorization to the MediatR request boundary so each command/query declares its allowed roles explicitly, beyond HTTP route guards.

The public `edge-proxy` may be the preferred client entry, but this matrix is still enforced inside each backend service after that routing step. The gateway does not replace per-service `JWT` validation, `audience` checks, or request-level authorization.

## Role rules

- `Administrator` can manage `Mission Design`, read bootstrap metadata, and administer `Operator` users.
- `Operator` can consume `Mission Design` snapshots needed to create a `LiveSession` and can create/read `LiveSession` data.
- `Participant` is authenticated but has no current command/query with cross-team read or write access. Future participant requests must validate `Team Participation` scope.

## Current matrix

| Bounded context | Request | Allowed roles | Scope |
| --- | --- | --- | --- |
| Mission Design | `GetMissionManagementBootstrapDetailsQuery` | Any authenticated role | None |
| Mission Design | `ListMissionsQuery` | `Administrator` | None |
| Mission Design | `GetMissionByIdQuery` | `Administrator` | None |
| Mission Design | `CreateMissionCommand` | `Administrator` | None |
| Mission Design | `UpdateMissionCommand` | `Administrator` | None |
| Mission Design | `ActivateMissionCommand` | `Administrator` | None |
| Mission Design | `DeactivateMissionCommand` | `Administrator` | None |
| Mission Design | `ListEligibleMissionsForLiveSessionQuery` | `Administrator`, `Operator` | None |
| Mission Design | `GetEligibleMissionForLiveSessionQuery` | `Administrator`, `Operator` | None |
| Mission Design | `ListMissionStagesQuery` | `Administrator` | None |
| Mission Design | `GetMissionStageByIdQuery` | `Administrator` | None |
| Mission Design | `CreateMissionStageCommand` | `Administrator` | None |
| Mission Design | `CreateMissionStageHintCommand` | `Administrator` | None |
| Mission Design | `DeactivateMissionStageCommand` | `Administrator` | None |
| Session Operations | `GetSessionOperationsBootstrapDetailsQuery` | Any authenticated role | None |
| Session Operations | `ListLiveSessionsQuery` | `Administrator`, `Operator` | None |
| Session Operations | `GetLiveSessionByIdQuery` | `Administrator`, `Operator` | None |
| Session Operations | `CreateLiveSessionCommand` | `Administrator`, `Operator` | None |
| Identity and Access | `ListOperatorsQuery` | `Administrator` | None |
| Identity and Access | `CreateOperatorCommand` | `Administrator` | None |
| Identity and Access | `DeactivateOperatorCommand` | `Administrator` | None |
| Identity and Access | `RotateOperatorPasswordCommand` | `Administrator` | None |
| Scoring and Audit | `GetScoringAuditBootstrapDetailsQuery` | Any authenticated role | None |

## Scope rule for future participant requests

- A participant request must add an `IRequestScopeValidator<TRequest>` that checks `Team Participation` before business logic executes.
- Scope failures must reuse the generic forbidden response so the service does not reveal whether another `Session Team` exists, progressed, or scored.
