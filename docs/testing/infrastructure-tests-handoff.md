# Handoff — Infrastructure-layer tests

**Goal:** add tests for the `*.Infrastructure` "seams" (the classes that talk to the outside world: Keycloak, other services over HTTP, SignalR, EF/Postgres, HttpContext). These are the least-covered, most bug-prone parts of the backend.

**Status going in:** the test suite was restructured into per-service `UnitTests` + `IntegrationTests` projects, mission has real-Postgres tests via Testcontainers, and user-management handlers were backfilled. Production line coverage is ~73% overall, but the Infrastructure assemblies drag the bottom:

| Assembly | Line coverage today | Why |
|---|--:|---|
| `ScoringMonitoring.Infrastructure` | ~15% | `Repository<T>`, `ApplyPenaltyScoreboardStore`, factory untested |
| `SessionManagement.Infrastructure` | ~15% | HTTP clients, identity accessors, SignalR notifier, `Repository<T>` untested |
| `UserManagement.Infrastructure` | low | `KeycloakAdminApiClient` has **no** tests |
| `MissionManagement.Infrastructure` | ~95% | already exercised by the Postgres test — use it as the model |

Read the architecture note first: `.claude/projects/.../memory/test-architecture.md` (or the section below).

---

## ⚠️ Important correction: there is no message bus

Earlier notes mentioned "RabbitMQ EventBus." **It does not exist.** `RabbitMQ:Host` appears only in test `ConfigureAppConfiguration` blocks; there is no publisher, consumer, `IBus`, or MassTransit anywhere. Cross-service eventing is done with **SignalR** (`SignalRLiveSessionRealtimeNotifier`, `SignalRScoringMonitoringUpdatesPublisher`). So **do not** plan RabbitMQ/Testcontainers-broker tests — test the SignalR notifiers instead.

---

## Where these tests go

The classification rule (reference boundary) still holds: a test that touches an `Infrastructure` type belongs in the service's **`<Svc>.IntegrationTests`** project, *not* `UnitTests` (which only references Domain + Application). Most Infrastructure seam tests are still "fast" (they mock `HttpMessageHandler` / `IHubContext` / use InMemory EF) — they just live in IntegrationTests because that's the project allowed to reference Infrastructure.

- **mission / scoring / session** — `IntegrationTests` projects already exist; reference `Api` (→ Infrastructure transitively). Put new tests in an `Infrastructure/` subfolder.
- **user-management** — has **no** IntegrationTests project yet (it was deferred). **First task: create it.** Steps:
  ```
  src/services/user-management/tests/UserManagement.IntegrationTests/UserManagement.IntegrationTests.csproj
  ```
  Use the canonical IntegrationTests csproj (copy mission's), referencing `..\..\UserManagement.Api\UserManagement.Api.csproj`. Then:
  ```
  dotnet sln Umbral.sln add src/services/user-management/tests/UserManagement.IntegrationTests/UserManagement.IntegrationTests.csproj
  ```
  and add its path to **both** arrays in `scripts/Invoke-RepositoryValidation.ps1` (`$backendProjects`, `$backendIntegrationTestProjects`).
  - You do **not** need `WebApplicationFactory` (so no `public partial class Program;` shim) to test `KeycloakAdminApiClient` — it's `public`, just `new` it up with a stubbed `HttpClient`.
- **shared** — `KeycloakRoleClaimsTransformation` lives in `Umbral.ServiceDefaults`; test it from the existing `Umbral.ServiceDefaults.UnitTests` (it references ServiceDefaults, no host needed).

### Gotcha: `internal Repository<T>`
`Repository<T>` is `internal` in every service. session & scoring `Infrastructure.csproj` already have `<InternalsVisibleTo Include="*.IntegrationTests" />`, so they're testable. **mission's Infrastructure has no `InternalsVisibleTo`** — if you test mission's `Repository<T>`, add one.

---

## Reusable test helpers (write once, share)

Consider extracting these into a small `Umbral.TestSupport` project (also folds in the duplicated `TestAuthenticationHandler` and the Postgres fixture). If you don't, drop them in each IntegrationTests `Fixtures/` folder.

**1. Stub HTTP handler** — for every HTTP-client seam (note: an equivalent `CapturingHttpMessageHandler` already exists in `session .../ScoringAuditHttpClientTests.cs`; reuse it):
```csharp
internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request);
        return Task.FromResult(responder(request));
    }
}
// var http = new HttpClient(handler) { BaseAddress = new Uri("http://stub") };
```

**2. HttpContext stub** — for identity accessors + the forwarding handler (no Moq needed, use `DefaultHttpContext`):
```csharp
var ctx = new DefaultHttpContext();
ctx.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "op-1")], "Test"));
var accessor = new HttpContextAccessor { HttpContext = ctx };
```

**3. SignalR hub context mock** (Moq — already referenced):
```csharp
var client = new Mock<ISessionClient>();              // confirm the strongly-typed client interface + method names in the hub
var clients = new Mock<IHubClients<ISessionClient>>();
clients.Setup(c => c.All).Returns(client.Object);
var hub = new Mock<IHubContext<SessionManagementHub, ISessionClient>>();
hub.Setup(h => h.Clients).Returns(clients.Object);
// client.Verify(c => c.ReceiveSessionStateChanged(It.IsAny<SessionStateChangedPayload>()), Times.Once);
```

**4. EF for `Repository<T>` / stores** — InMemory for fast, or reuse the Testcontainers Postgres pattern from `MissionPostgresPersistenceTests.cs` for the real thing:
```csharp
var options = new DbContextOptionsBuilder<ScoringMonitoringDbContext>()
    .UseInMemoryDatabase($"infra-{Guid.NewGuid():N}").Options;
await using var db = new ScoringMonitoringDbContext(options);
var store = new ApplyPenaltyScoreboardStore(db);
```

No new NuGet packages are required (Moq, EFCore.InMemory, Testcontainers.PostgreSql, Mvc.Testing are all in CPM). Match the existing style: raw `Assert.*` (FluentAssertions is in CPM but unused — don't introduce it).

---

## Work batches (priority order)

For each class: open the file (paths in the appendix), confirm the exact ctor + method signatures, then write the cases. Method lists below are from a read-through but **verify against the source** before relying on a name.

### Batch 1 — HTTP clients (highest ROI)

**`KeycloakAdminApiClient`** · `user-management/.../Services/Identity/KeycloakAdminApiClient.cs` · `public`, ctor `(HttpClient, IOptions<KeycloakAdminApiOptions>)`.
Gotcha: it caches an admin token (password grant, `SemaphoreSlim`, ~30s expiry buffer). Your stub handler must answer the **token endpoint** (`POST .../protocol/openid-connect/token`) *and* the admin endpoints. Cases:
- token fetch happens once and is reused across calls;
- `CreateUserAsync` parses the `Location` header → id; missing Location → recovers via username lookup;
- `409` → `UmbralDomainException("operator_provider_duplicate")`;
- failed token fetch → `UmbralTechnicalException("user_management_admin_session_failed")`;
- `SetUserEnabledAsync` / `RotateOperatorPasswordAsync` / `AssignOperatorRoleAsync` issue the right verb+path+body;
- `GetUserByIdAsync` 404 → `null`.
- Config validation: each missing required option (`BaseUrl`, `Realm`, `AdminRealm`, `AdminClientId`, `AdminUsername`, `AdminPassword`) throws.

**`ScoringMonitoringHttpClient`** (file `session/.../ScoringAuditHttpClient.cs`) · `public`, ctor `(HttpClient)`.
Partial coverage exists (serialization of `RecordStageCredit`/`LogSessionEvent`). Add: `ApplyPenaltyAsync` response **deserialization**, non-2xx → exception mapping, `HttpRequestException`/timeout path.

**`MissionManagementLiveSessionCatalog`** · `session/.../MissionManagementLiveSessionCatalog.cs` · `public`, ctor `(HttpClient)`.
`GET /api/mission-management/missions/eligible-for-live-session/{id}`. Cases: happy path deserializes `EligibleMissionForLiveSessionSnapshot`; 400→Validation, 401→Unauthorized, 404→NotFound mapping via problem-details parse; malformed JSON.

**`AuthHeaderForwardingHandler`** · `session/.../AuthHeaderForwardingHandler.cs` · `public DelegatingHandler`, ctor `(IHttpContextAccessor)`.
Wrap it over a `StubHttpMessageHandler` via `HttpMessageInvoker`. Cases: forwards `Authorization` from the current HttpContext; does nothing when absent; does not overwrite an already-set header.

### Batch 2 — identity & SignalR

**`HttpContextCurrentOperatorIdentity`** / **`HttpContextCurrentParticipantIdentity`** · `session/.../*.cs` · `public`, ctor `(IHttpContextAccessor)`.
`GetRequiredOperatorUserId()` / `GetRequiredParticipantUserId()`. Cases: claim present (`sub`/`NameIdentifier`) → value; missing → `UmbralDomainException`; participant variant also exercises `ParticipantUserId.Parse` validation.

**`SignalRLiveSessionRealtimeNotifier`** · `session/.../SignalRLiveSessionRealtimeNotifier.cs` · `public`, ctor `(IHubContext<SessionManagementHub, ISessionClient>)`.
One case per `Notify…Async` method: verify the right client method is called once with a payload carrying the expected `RealtimeEventMetadata` (sequence/timestamp/refresh policy). Confirm the hub client interface + method names in the hub file before writing.

**`KeycloakRoleClaimsTransformation`** (shared) · `Umbral.ServiceDefaults/KeycloakRoleClaimsTransformation.cs` · `public IClaimsTransformation`, depends on `AuthConfiguration`.
Cases: roles pulled from `realm_access.roles`, `resource_access.{audience}.roles`, flat `roles`/`role`; audience filtering; malformed/empty JSON handled without throwing. (Put in `Umbral.ServiceDefaults.UnitTests`.)

### Batch 3 — persistence

**`ApplyPenaltyScoreboardStore`** · `scoring/.../Persistence/ApplyPenaltyScoreboardStore.cs` · `public`, ctor `(ScoringMonitoringDbContext)`.
InMemory (or Postgres) DbContext. Cases: `LoadAsync` returns existing scoreboard with `ScoreEntries` included and `RebuildState()` applied; creates a new one when none exists; `PersistPenaltyApplicationAsync` appends the `SessionEventLog` and saves.

**`Repository<T>`** (mission/scoring/session, `internal`) — one suite per service (or a generic helper). Cases: `Add`/`Update`/`Remove` + `SaveChanges` round-trip, `GetByIdAsync`, predicate queries, IQueryable chaining. (Add `InternalsVisibleTo` for mission first.)

**`*DbContextFactory`** (3×, design-time `IDesignTimeDbContextFactory`) — low priority. Cases: builds a context from a config with `ConnectionStrings:Postgres`; throws when missing. Note these are design-time only (used by `dotnet ef`), so coverage here is cosmetic.

### Batch 4 — shared DI

**`Umbral.ServiceDefaults.ServiceCollectionExtensions.AddUmbralPostgresDbContext<T>`** — register against a `ServiceCollection`, assert the `DbContext` resolves scoped with the right schema/migrations-assembly; missing connection string throws. (`ServiceConfigurationTests` / `JwtBearerConfigurationTests` already cover the auth/config side — don't duplicate.)

---

## Verification & definition of done

- Build + run: `pwsh ./scripts/Invoke-RepositoryValidation.ps1 -Scope BackendIntegration -SkipComposeSmoke` (or `dotnet test Umbral.sln`). All green.
- Regenerate the coverage report and confirm the Infrastructure assemblies climb:
  ```
  dotnet test Umbral.sln --collect:"XPlat Code Coverage" --results-directory temp/validation/TestResults
  pwsh ./scripts/Publish-BackendCoverageReports.ps1 -ResultsDirectory temp/validation/TestResults
  # open temp/validation/backend-coverage-report/index.html
  ```
  Target: `ScoringMonitoring.Infrastructure` and `SessionManagement.Infrastructure` from ~15% up toward 75–85%; `UserManagement.Infrastructure` (Keycloak client) into the 80s.
- **Done when:** every Batch-1/2 seam has tests, persistence stores (Batch 3) are covered, the suite is green, and the coverage report shows the Infrastructure assemblies no longer the outliers. Land it as a gitflow `fix/…` branch merged `--no-ff` into `develop` (same as the prior work).

---

## Appendix — file paths

**user-management**
- `src/services/user-management/UserManagement.Infrastructure/Services/Identity/KeycloakAdminApiClient.cs`
- `src/services/user-management/UserManagement.Infrastructure/Services/Identity/KeycloakAdminApiOptions.cs`
- `src/services/user-management/UserManagement.Infrastructure/UserManagementInfrastructureServiceCollectionExtensions.cs`

**session-management** (`src/services/session-management/SessionManagement.Infrastructure/`)
- `ScoringAuditHttpClient.cs` (class `ScoringMonitoringHttpClient`), `MissionManagementLiveSessionCatalog.cs`, `AuthHeaderForwardingHandler.cs`
- `HttpContextCurrentOperatorIdentity.cs`, `HttpContextCurrentParticipantIdentity.cs`
- `SignalRLiveSessionRealtimeNotifier.cs`, `CryptographicJoinCodeGenerator.cs`
- `Persistence/Repository.cs`, `Persistence/SessionManagementDbContext.cs`, `Persistence/SessionManagementDbContextFactory.cs`

**scoring-monitoring** (`src/services/scoring-monitoring/ScoringMonitoring.Infrastructure/`)
- `Persistence/ApplyPenaltyScoreboardStore.cs`, `Persistence/Repository.cs`, `Persistence/ScoringMonitoringDbContext.cs`, `Persistence/ScoringMonitoringDbContextFactory.cs`

**mission-management** (`src/services/mission-management/MissionManagement.Infrastructure/`)
- `Persistence/Repository.cs` (needs `InternalsVisibleTo`), `Persistence/MissionManagementDbContext.cs`

**shared** (`src/shared/Umbral.ServiceDefaults/`)
- `KeycloakRoleClaimsTransformation.cs`, `ServiceCollectionExtensions.cs`, `AuthConfiguration.cs`

**existing tests to mirror / not duplicate**
- `src/services/session-management/tests/SessionManagement.IntegrationTests/ScoringAuditHttpClientTests.cs` (HTTP stub pattern)
- `src/services/mission-management/tests/MissionManagement.IntegrationTests/MissionPostgresPersistenceTests.cs` (Testcontainers pattern)
- `src/shared/Umbral.ServiceDefaults.UnitTests/JwtBearerConfigurationTests.cs`
