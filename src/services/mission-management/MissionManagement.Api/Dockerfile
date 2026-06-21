FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/services/mission-management/MissionManagement.Api/MissionManagement.Api.csproj src/services/mission-management/MissionManagement.Api/
RUN dotnet restore src/services/mission-management/MissionManagement.Api/MissionManagement.Api.csproj

COPY src/services/mission-management/MissionManagement.Api/ src/services/mission-management/MissionManagement.Api/
RUN dotnet publish src/services/mission-management/MissionManagement.Api/MissionManagement.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MissionManagement.Api.dll"]
