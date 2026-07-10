# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 1) Copy build config + project manifests only, then restore.
#    This layer is cached and only rebuilds when a .csproj / props file changes,
#    not on every source edit.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/shared/Umbral.Kernel/Umbral.Kernel.csproj src/shared/Umbral.Kernel/
COPY src/shared/Umbral.ServiceDefaults/Umbral.ServiceDefaults.csproj src/shared/Umbral.ServiceDefaults/
COPY src/services/user-management/UserManagement.Api/UserManagement.Api.csproj src/services/user-management/UserManagement.Api/
COPY src/services/user-management/UserManagement.Application/UserManagement.Application.csproj src/services/user-management/UserManagement.Application/
COPY src/services/user-management/UserManagement.Domain/UserManagement.Domain.csproj src/services/user-management/UserManagement.Domain/
COPY src/services/user-management/UserManagement.Infrastructure/UserManagement.Infrastructure.csproj src/services/user-management/UserManagement.Infrastructure/
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/services/user-management/UserManagement.Api/UserManagement.Api.csproj

# 2) Copy the rest of the source and publish without re-restoring.
COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/services/user-management/UserManagement.Api/UserManagement.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "UserManagement.Api.dll"]
