# Handoff — Branch coverage, Tier 1 (Application handlers) + Tier 2 (Domain aggregates)

**Goal:** raise **branch** coverage by testing the *guard / error / state-transition* paths that today are only ever reached on the happy path (or not at all). This is the follow-up to the Infrastructure-tests work (commit `3905a3d`, see `docs/testing/infrastructure-tests-handoff.md`). That work fixed the Infrastructure line-coverage hole; the remaining debt is **branch** coverage in the **Domain** and **Application** layers.

**Numbers going in** (from `temp/validation/backend-coverage-report/Summary.txt` after the infra work):

- Line coverage: **76.7%**
- **Branch coverage: 65.4% (784 / 1197)** ← this is the target metric
- 413 branches uncovered total

The biggest concentrations of uncovered branches are **not** in Infrastructure — they are in domain state machines and untested command/query handlers. Ranked by *absolute uncovered branches* (the order to attack):

| # | Assembly | Class | Uncovered branches | Branch % today |
|---|---|---|--:|--:|
| Tier 2 | SessionManagement.Domain | `LiveSession` | **63** | 71% |
| Tier 1 | SessionManagement.Application | `OverrideValidationOutcomeHandler` | 30 | **0%** |
| Tier 1 | SessionManagement.Application | `SubmitTriviaAnswerHandler` | 20 | **0%** |
| Tier 1 | SessionManagement.Application | `GetSessionTeamDetailQueryHandler` | 16 | 50% |
| Tier 1 | SessionManagement.Application | `SubmitEvidenceCommandHandler` | 13 | 35% |
| Tier 1 | MissionManagement.Application | `MissionMappings` | 13 | 66% |
| Tier 1 | SessionManagement.Application | `ReleaseHintHandler` | 11 | 63% |
| Tier 1 | SessionManagement.Application | `GetLiveSessionOverviewQueryHandler` | 10 | 44% |
| Tier 2 | SessionManagement.Domain | `LiveSessionStage` | 9 | 72% |
| Tier 1 | SessionManagement.Application | `GetSessionTeamSnapshotQueryHandler` | 6 | 73% |
| Tier 2 | ScoringMonitoring.Domain | `Scoreboard` | 6 | 81% |
| Tier 2 | ScoringMonitoring.Domain | `Penalty` | 6 | 62% |
| Tier 2 | MissionManagement.Domain | `Challenge` | 6 | 57% |
| Tier 2 | MissionManagement.Domain | `DomainText` | 6 | 40% |
| Tier 2 | SessionManagement.Domain | `ReleasedHint` | 6 | 57% |
| Tier 2 | ScoringMonitoring.Domain | `ScoreEntry` | 5 | 50% |
| Tier 2 | MissionManagement.Domain | `Mission` | 6 | 88% |
| Tier 1 | MissionManagement.Application | `UpdateMissionCommandHandler` | 4 | 50% |

**Tier 1 + Tier 2 ≈ 250 of the 413 uncovered branches → would lift branch coverage from 65% to ~85%.** `LiveSession` alone is 63 of them.

---

## Where these tests go — this is **UnitTests**, not IntegrationTests

Everything in this handoff is **Domain + Application** logic. It belongs in the per-service **`*.UnitTests`** projects (which reference only Domain + Application — **no Infrastructure, no Docker, no `WebApplicationFactory`, no Testcontainers**). Do **not** put these in IntegrationTests.

- `src/services/session-management/tests/SessionManagement.UnitTests/`
- `src/services/scoring-monitoring/tests/ScoringMonitoring.UnitTests/`
- `src/services/mission-management/tests/MissionManagement.UnitTests/`

**Why so much of this is uncovered:** the happy paths of these handlers/aggregates *are* exercised — but indirectly, through the `*QaTests` end-to-end flows in the IntegrationTests projects (e.g. `SessionSnapshotsQaTests`, `HintReleaseQaTests`, `EvidenceSubmissionAuditEventTests`). Those flows drive one nominal path each, so every `throw new UmbralDomainException(...)` guard and every alternate `if/else` branch stays cold. Direct unit tests over the aggregate + handler are the cheapest way to light them up.

### Style rules (match the repo — same as the infra work)
- **Raw `Assert.*` only.** FluentAssertions is in CPM but **0 usages** in the repo — do **not** introduce it. Use `Assert.Throws<T>` / `await Assert.ThrowsAsync<T>` and assert `ex.Code` on `UmbralDomainException` / `UmbralTechnicalException` (both expose `.Code`; category is `UmbralFailureCategory`).
- File-scoped namespaces; `public sealed class XxxTests`; `[Fact]` / `[Theory]`; C# 12 (primary ctors, collection expressions `[]`). Target framework is **net10.0** (not net8 — ignore any stale "net8" note).
- No new NuGet packages. Moq, EFCore.InMemory, Mvc.Testing, Testcontainers are already in CPM.

---

## Test helpers & patterns

**Tier 1 (handlers).** Every handler is a `public sealed class XxxHandler(...)` with a **primary constructor whose parameters are the ports it depends on** (repositories, identity accessors, notifiers, clocks). Open the handler, read the ctor params, and mock each with **Moq**. Pattern:

```csharp
var repo = new Mock<ILiveSessionRepository>();      // confirm exact port interface names from the ctor
repo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(liveSession);
var sut = new SubmitTriviaAnswerHandler(repo.Object, /* other ports */);
// happy path: assert it loaded, mutated the aggregate, persisted (repo.Verify(... SaveChanges/Update)), mapped the result
// guard paths: repo returns null -> Assert.ThrowsAsync<UmbralDomainException> with the not-found code, etc.
```
Handler-level branches to cover are typically: **aggregate-not-found → throw**, identity/authorization guards, mapping conditionals (null/optional fields), and the propagation of domain `UmbralDomainException`s thrown by the aggregate.

**Tier 2 (aggregates).** Construct via the real factory methods (private ctors + `static Create(...)`). There is **no `LiveSession` builder in UnitTests today** — add one. Mirror `MissionManagement.UnitTests/SampleMissions.cs`. A `LiveSession` reaches its interesting states like this:

```csharp
var session = LiveSession.Create(id, missionId, "Mission", "Session", scheduledStartAtUtc: null, createdAtUtc, stageFlow);
session.AssignJoinCode(joinCode);
session.OpenEnrollmentWindow(t0);
session.RegisterTeam(teamId, "Team A", participantUserId, joinCode, t1);   // adds a SessionTeam + enrolls participant
session.Start(t2);                                                          // Scheduled -> Active
```
You will need small factories for `LiveSessionStage`, `JoinCode`, and `ParticipantUserId` — confirm their public surface in `SessionManagement.Domain/LiveSessions/`. Put a `SampleLiveSessions` (or `LiveSessionBuilder`) helper in `SessionManagement.UnitTests` so the many cases below stay readable. For scoring/mission, **extend the existing domain test files** (`ScoringDomainTests.cs`, `MissionDomainTests.cs`) and reuse their existing builders.

---

## How to measure branch coverage

```
# fast unit lane (no Docker) — this is all UnitTests, so this is enough to run them:
pwsh ./scripts/Invoke-RepositoryValidation.ps1 -Scope BackendUnit -SkipComposeSmoke

# or collect coverage directly and read the branch numbers:
dotnet test Umbral.sln --collect:"XPlat Code Coverage" --results-directory temp/validation/TestResults
pwsh ./scripts/Publish-BackendCoverageReports.ps1 -ResultsDirectory temp/validation/TestResults -OutputDirectory temp/validation/backend-coverage-report
# Summary.txt top block shows "Branch coverage: NN% (x of y)"
```

Rank what's left with this snippet (lists classes by absolute uncovered branches):

```powershell
[xml]$cov = Get-Content temp/validation/backend-coverage-report/Cobertura.xml
$rows = foreach ($pkg in $cov.coverage.packages.package) {
  foreach ($cls in $pkg.classes.class) {
    $cb = 0; $tb = 0
    foreach ($ln in $cls.lines.line) {
      if ($ln.branch -eq 'true' -and $ln.'condition-coverage' -match '\((\d+)/(\d+)\)') { $cb += [int]$Matches[1]; $tb += [int]$Matches[2] }
    }
    if ($tb -gt 0) { [pscustomobject]@{ Class=$cls.name.Split('.')[-1]; Uncov=($tb-$cb); Pct=[math]::Round(100*$cb/$tb,0) } }
  }
}
$rows | Where-Object Uncov -ge 3 | Sort-Object Uncov -Descending | Select-Object -First 25 | Format-Table -AutoSize
```

---

## Tier 2 — Domain aggregates (do `LiveSession` first; it is 63 of the wins)

### `LiveSession` — `src/services/session-management/SessionManagement.Domain/LiveSessions/LiveSession.cs`
State machine over `LiveSessionStates` (`Scheduled → Active ⇄ Paused → Finalized`, plus `Canceled`). Almost every public method has an `Ensure…` guard that throws `UmbralDomainException` (most with `UmbralFailureCategory.Conflict`). Write one `[Fact]` per **throwing branch** plus the happy transition. **The guard map (verify codes against source as you go):**

- **`Create` / `ReplaceSessionStageFlow` / `NormalizeSessionStageFlow`:** `missionId == Guid.Empty` → `live_session_mission_required`; blank `missionName` / `name` → `*_required`; over-120-char → `*_required_too_long`; empty stage flow → `live_session_stage_flow_required`; duplicate `MissionStageId` → `live_session_stage_flow_duplicate_stage`; non-contiguous order → `live_session_stage_flow_order_invalid`. (`id == Guid.Empty` takes the `NewGuid()` branch.)
- **`Start`:** not `Scheduled` → `live_session_cannot_start`; zero teams → `live_session_requires_session_teams`; open-but-unclosed enrollment window → auto-closes (cover that branch); happy → `Active`.
- **`Pause` / `Resume` / `Cancel`:** wrong-state throw + happy for each (`live_session_cannot_pause` / `_resume`; `Cancel` allowed from Scheduled/Active/Paused, else `live_session_cannot_cancel`).
- **`FinalizeSession`:** already-`Finalized` → early return; not Active/Paused → `live_session_cannot_finalize`; happy. **`FinalizeAndRevealAllHints`:** already-released-hint `continue` branch; `releasedHints.Count > 0` → `SequenceNumber++` vs no-op.
- **`AssignJoinCode`:** null arg; first assign; same value → no-op; different value → `live_session_join_code_already_assigned`.
- **`OpenEnrollmentWindow`:** not scheduled; `JoinCodeValue is null` → `live_session_join_code_required_before_enrollment`; already-closed → `live_session_enrollment_window_closed`; already-opened → no-op; happy.
- **`CloseEnrollmentWindow`:** not opened → `live_session_enrollment_window_not_opened`; already closed → no-op; `closedAt < openedAt` → `live_session_enrollment_window_close_time_invalid`; happy.
- **`RegisterTeam` / `EnrollParticipantInTeam` / `EnsureEnrollmentAllowed`:** not scheduled; join code not generated (`live_session_join_code_not_generated`); wrong join code (`join_code_invalid_for_live_session`); window not open (`live_session_enrollment_window_not_active`); duplicate team name (`session_team_name_duplicate`); team not found (`session_team_not_found`); new vs existing-same-team (no-op) vs existing-different-team (`MoveToTeam`).
- **`SubmitEvidence`:** `EnsureEvidenceSubmissionAllowed` (Paused/Finalized/Canceled → `live_session_not_accepting_evidence`); team-not-found; progress already completed (`session_team_progress_already_completed`); stage-index invalid (`session_team_progress_stage_index_invalid`); `EnsureTreasureHuntStage` (not TreasureHunt → `evidence_submission_stage_not_treasure_hunt`; missing expected hash → `evidence_submission_expected_qr_hash_required`); stage already resolved (`session_stage_already_resolved_by_team`); **accepted vs rejected** hash; `AcceptCurrentStage` last-stage (→ `Complete` + `Finalized`) vs advance.
- **`SubmitTriviaAnswer`:** same shell + `EnsureTriviaStage` (not Trivia → `evidence_submission_stage_not_trivia`; no `CorrectChoiceId` → `evidence_submission_trivia_answer_required`; choice not in play → `evidence_submission_choice_not_in_play`); accepted vs rejected; advance vs finalize.
- **`OverrideValidationOutcome` / `TryAcceptOverriddenStage`:** submission not found (`evidence_submission_not_found`); `previousOutcome != Accepted && newOutcome == Accepted` triggers stage acceptance; within `TryAcceptOverriddenStage`: already-accepted-for-stage → return; stage not in flow (`validation_override_stage_not_in_flow`); progress completed/ahead → return; `current < stage` (`validation_override_stage_not_current`) → throw; else accept.
- **`ReleaseHint` / `AddOperationalHint`:** `EnsureHintReleaseAllowed` (must be Active/Paused else `live_session_not_accepting_hint_release`); no current stage (`released_hint_current_stage_required`); hint not in stage (`released_hint_not_in_current_stage`); duplicate (`released_hint_duplicate`); operational-hint stage not in flow (`operational_hint_stage_not_in_flow`); `ConvertCoordinate` NaN/Infinity → `operational_hint_latitude/longitude_invalid`, null → null.
- **`GetCurrentStageForTeam`:** no stages → null; no progress → first stage; completed → null; index ≥ length → null vs stage. **`IsStagePending` / `DeactivateStage`:** stage not in flow (`session_stage_not_in_flow`); `≤1` stage (`session_stage_flow_last_pending_stage`); not pending (`session_stage_not_pending`); last-pending-after-removal guard; plus the `RecalculateTeamProgressionsAfterStageDeactivation` branches (completed-skip / index-shift / advance-to-removed / complete).

### `LiveSessionStage` (9) · `ReleasedHint` (6) — same folder
Record validation/factory guards. Read each, cover the `throw` branches and any optional-field conditionals.

### `Scoreboard` (6) · `Penalty` (6) · `ScoreEntry` (5) — `ScoringMonitoring.Domain`
Extend `ScoringMonitoring.UnitTests/ScoringDomainTests.cs`. Cover the remaining guard/branch paths (penalty bounds, scoreboard `RebuildState`/dedup edge cases, score-entry validation). Read source for exact codes.

### `Challenge` (6) · `DomainText` (6) · `Mission` (6) — `MissionManagement.Domain`
Extend `MissionManagement.UnitTests/MissionDomainTests.cs`, reuse `SampleMissions.cs`. `DomainText` is an `internal static` normalizer (paths: null/blank → throw, trim, max-length → throw) — its 40% is the cheapest single class.

---

## Tier 1 — Application handlers

For each: open the handler (paths below), read its primary-ctor ports, mock them with Moq, and cover **happy path + every guard branch**. The two **0%** handlers have *no* tests at all — start there.

- `OverrideValidationOutcomeHandler` (30, **0%**) — `…/Features/EvidenceSubmissions/Commands/OverrideValidationOutcome/OverrideValidationOutcomeCommandHandler.cs`
- `SubmitTriviaAnswerHandler` (20, **0%**) — `…/Features/EvidenceSubmissions/Commands/SubmitTriviaAnswer/SubmitTriviaAnswerCommandHandler.cs`
- `SubmitEvidenceCommandHandler` (13, 35%) — `…/Features/EvidenceSubmissions/Commands/SubmitEvidence/SubmitEvidenceCommandHandler.cs`
- `ReleaseHintHandler` (11, 63%) — `…/Features/Hints/Commands/ReleaseHint/ReleaseHintCommandHandler.cs`
- `GetSessionTeamDetailQueryHandler` (16, 50%) — `…/Features/SessionSnapshots/Queries/GetSessionTeamDetail/GetSessionTeamDetailQueryHandler.cs`
- `GetLiveSessionOverviewQueryHandler` (10, 44%) — `…/Features/SessionSnapshots/Queries/GetLiveSessionOverview/GetLiveSessionOverviewQueryHandler.cs`
- `GetSessionTeamSnapshotQueryHandler` (6, 73%) — `…/Features/SessionSnapshots/Queries/GetSessionTeamSnapshot/GetSessionTeamSnapshotQueryHandler.cs`
- `MissionMappings` (13, 66%) — `MissionManagement.Application/Features/Missions/MissionContracts.cs` (static mapper; branches are null/optional-field mapping — feed it missions with/without optional pieces)
- `UpdateMissionCommandHandler` (4, 50%) — `MissionManagement.Application/Features/Missions/Commands/UpdateMission/UpdateMissionCommandHandler.cs`

(All session paths are under `src/services/session-management/SessionManagement.Application/`.)

**Note on overlap:** the command handlers delegate the domain guards to `LiveSession`, so the Tier 2 `LiveSession` tests already cover those `throw`s at the aggregate level. The Tier 1 handler tests should focus on the handler's **own** branches — load-or-404, identity/clock usage, port calls (notifier/scoring-audit), and result mapping — and one happy path that proves the orchestration. The query handlers (`Get…`) are mostly mapping branches: drive them with snapshots that exercise present/absent/empty collections.

---

## Verification & definition of done

- `pwsh ./scripts/Invoke-RepositoryValidation.ps1 -Scope BackendUnit -SkipComposeSmoke` → green. (These are all unit tests, so the fast lane is sufficient; run `dotnet test Umbral.sln` if you want the whole suite.)
- Regenerate coverage and confirm **branch coverage climbs**:
  - Overall: **65.4% → ~85%**.
  - `LiveSession` 71% → 95%+; `OverrideValidationOutcomeHandler` & `SubmitTriviaAnswerHandler` 0% → 90%+; the `Get…QueryHandler`s out of the 40–70% band.
- **Done when:** every Tier-1 handler and every Tier-2 aggregate above has direct unit tests for its guard/branch paths, the suite is green, and the branch-coverage number has moved from ~65% into the mid-80s with no class in the list still sitting at 0%.
- **Land it** as a gitflow **feature** branch merged `--no-ff` into `develop` (use the `git-no-ff-merge` skill: `.agents/skills/git-no-ff-merge`), Conventional Commits message (`test(domain): …` / `test(application): …`). Same flow the infra work used (see graph at commit `3905a3d`).

---

## Appendix — paths

**Tier 2 source**
- `src/services/session-management/SessionManagement.Domain/LiveSessions/LiveSession.cs` · `LiveSessionStage.cs` · `ReleasedHint.cs`
- `src/services/scoring-monitoring/ScoringMonitoring.Domain/Scoreboards/Scoreboard.cs` · `Scoreboards/ScoreEntry.cs` · `Penalties/Penalty.cs`
- `src/services/mission-management/MissionManagement.Domain/Missions/Challenge.cs` · `Missions/DomainText.cs` · `Missions/Mission.cs`

**Tier 1 source** — paths inline in the Tier 1 section above.

**Existing tests to mirror / extend (do not duplicate their happy-path coverage)**
- `src/services/session-management/tests/SessionManagement.UnitTests/EvidenceSubmissionQaTests.cs` (only existing session unit test; **no `LiveSession` domain unit test exists yet — create one**)
- `src/services/scoring-monitoring/tests/ScoringMonitoring.UnitTests/ScoringDomainTests.cs`
- `src/services/mission-management/tests/MissionManagement.UnitTests/MissionDomainTests.cs` · `SampleMissions.cs` (builder helpers)
- Happy-path end-to-end flows (for reference only, do not move): `SessionManagement.IntegrationTests/{SessionSnapshotsQaTests,HintReleaseQaTests,EvidenceSubmissionAuditEventTests,SessionStageDeactivationQaTests}.cs`
