# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 1) Copy build config + project manifests only, then restore.
#    This layer is cached and only rebuilds when a .csproj / props file changes,
#    not on every source edit.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/shared/Umbral.ServiceDefaults/Umbral.ServiceDefaults.csproj src/shared/Umbral.ServiceDefaults/
COPY src/services/mission-management/MissionManagement.Api/MissionManagement.Api.csproj src/services/mission-management/MissionManagement.Api/
COPY src/services/mission-management/MissionManagement.Application/MissionManagement.Application.csproj src/services/mission-management/MissionManagement.Application/
COPY src/services/mission-management/MissionManagement.Domain/MissionManagement.Domain.csproj src/services/mission-management/MissionManagement.Domain/
COPY src/services/mission-management/MissionManagement.Infrastructure/MissionManagement.Infrastructure.csproj src/services/mission-management/MissionManagement.Infrastructure/
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/services/mission-management/MissionManagement.Api/MissionManagement.Api.csproj

# 2) Copy the rest of the source and publish without re-restoring.
COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/services/mission-management/MissionManagement.Api/MissionManagement.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MissionManagement.Api.dll"]
