# Backend Foundation

Este directorio contiene el baseline backend ejecutable para los bounded contexts que hoy se implementan con servicios `.NET`.

## Servicios incluidos

- `user-management/UserManagement.Api`
- `mission-management/MissionManagement.Api`
- `session-management/Umbral.SessionManagement.Api`
- `scoring-monitoring/ScoringMonitoring.Api`

## Capas esperadas por servicio

- entrada HTTP en `Program.cs` y endpoints
- aplicacion con comandos y queries `MediatR` bajo `Application/`
- persistencia e integraciones tecnicas bajo `Infrastructure/`
- contexto de dominio gobernado por `CONTEXT.md` del bounded context

## Convencion de errores HTTP

Los servicios usan `UmbralExceptionHandler` para mapear fallos de forma consistente:

- `Validation` -> `400`
- `Unauthorized` -> `401`
- `Forbidden` -> `403`
- `NotFound` -> `404`
- `Conflict` -> `409`
- `Domain` -> `422`
- `Technical` -> `500`

## Build y tests

### User Management

```powershell
dotnet build src/services/user-management/UserManagement.Api/UserManagement.Api.csproj
dotnet test src/services/user-management/UserManagement.Api.Tests/UserManagement.Api.Tests.csproj
```

### Mission Management

```powershell
dotnet build src/services/mission-management/MissionManagement.Api/MissionManagement.Api.csproj
dotnet test src/services/mission-management/MissionManagement.Api.Tests/MissionManagement.Api.Tests.csproj
```

### Session Operations

```powershell
dotnet build src/services/session-management/Umbral.SessionManagement.Api/Umbral.SessionManagement.Api.csproj
dotnet test src/services/session-management/Umbral.SessionManagement.Api.Tests/Umbral.SessionManagement.Api.Tests.csproj
```

### Scoring and Monitoring

```powershell
dotnet build src/services/scoring-monitoring/ScoringMonitoring.Api/ScoringMonitoring.Api.csproj
dotnet test src/services/scoring-monitoring/ScoringMonitoring.Api.Tests/ScoringMonitoring.Api.Tests.csproj
```

### Shared technical defaults

```powershell
dotnet test src/shared/Umbral.ServiceDefaults.Tests/Umbral.ServiceDefaults.Tests.csproj
```

## Migraciones EF Core

Cada servicio deja preparado:

- `DbContext` con schema propio
- factory de diseÃ±o `IDesignTimeDbContextFactory`
- historial de migraciones en el schema del servicio
- flag `Persistence:ApplyMigrationsOnStartup`

Ejemplos:

```powershell
dotnet ef migrations add InitialSchemaBaseline --project src/services/mission-management/MissionManagement.Infrastructure/MissionManagement.Infrastructure.csproj --startup-project src/services/mission-management/MissionManagement.Api/MissionManagement.Api.csproj
dotnet ef migrations add InitialSchemaBaseline --project src/services/session-management/Umbral.SessionManagement.Api/Umbral.SessionManagement.Api.csproj --startup-project src/services/session-management/Umbral.SessionManagement.Api/Umbral.SessionManagement.Api.csproj
dotnet ef migrations add InitialSchemaBaseline --project src/services/scoring-monitoring/ScoringMonitoring.Infrastructure/ScoringMonitoring.Infrastructure.csproj --startup-project src/services/scoring-monitoring/ScoringMonitoring.Api/ScoringMonitoring.Api.csproj
```

## Validacion integrada

La validacion reproducible del repo vive en `scripts/Invoke-RepositoryValidation.ps1`.

- backend: `dotnet build`, `dotnet test` y cobertura `XPlat Code Coverage`
- smoke distribuido: `scripts/Invoke-ComposeSmokeValidation.ps1`
- politica y artefactos: `docs/architecture/validation-pipeline.md`
