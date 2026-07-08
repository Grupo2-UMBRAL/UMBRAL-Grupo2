# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 1) Copy build config + project manifests only, then restore.
#    This layer is cached and only rebuilds when a .csproj / props file changes,
#    not on every source edit.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/shared/Umbral.ServiceDefaults/Umbral.ServiceDefaults.csproj src/shared/Umbral.ServiceDefaults/
COPY src/services/scoring-monitoring/ScoringMonitoring.Api/ScoringMonitoring.Api.csproj src/services/scoring-monitoring/ScoringMonitoring.Api/
COPY src/services/scoring-monitoring/ScoringMonitoring.Application/ScoringMonitoring.Application.csproj src/services/scoring-monitoring/ScoringMonitoring.Application/
COPY src/services/scoring-monitoring/ScoringMonitoring.Domain/ScoringMonitoring.Domain.csproj src/services/scoring-monitoring/ScoringMonitoring.Domain/
COPY src/services/scoring-monitoring/ScoringMonitoring.Infrastructure/ScoringMonitoring.Infrastructure.csproj src/services/scoring-monitoring/ScoringMonitoring.Infrastructure/
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore src/services/scoring-monitoring/ScoringMonitoring.Api/ScoringMonitoring.Api.csproj

# 2) Copy the rest of the source and publish without re-restoring.
COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/services/scoring-monitoring/ScoringMonitoring.Api/ScoringMonitoring.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ScoringMonitoring.Api.dll"]
