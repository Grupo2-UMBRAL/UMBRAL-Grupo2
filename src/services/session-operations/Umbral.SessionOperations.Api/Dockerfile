FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/session-operations/service/Umbral.SessionOperations.Api/Umbral.SessionOperations.Api.csproj src/session-operations/service/Umbral.SessionOperations.Api/
RUN dotnet restore src/session-operations/service/Umbral.SessionOperations.Api/Umbral.SessionOperations.Api.csproj

COPY src/session-operations/service/Umbral.SessionOperations.Api/ src/session-operations/service/Umbral.SessionOperations.Api/
RUN dotnet publish src/session-operations/service/Umbral.SessionOperations.Api/Umbral.SessionOperations.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Umbral.SessionOperations.Api.dll"]
