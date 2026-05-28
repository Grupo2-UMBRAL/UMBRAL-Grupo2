FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/scoring-audit/service/Umbral.ScoringAudit.Api/Umbral.ScoringAudit.Api.csproj src/scoring-audit/service/Umbral.ScoringAudit.Api/
RUN dotnet restore src/scoring-audit/service/Umbral.ScoringAudit.Api/Umbral.ScoringAudit.Api.csproj

COPY src/scoring-audit/service/Umbral.ScoringAudit.Api/ src/scoring-audit/service/Umbral.ScoringAudit.Api/
RUN dotnet publish src/scoring-audit/service/Umbral.ScoringAudit.Api/Umbral.ScoringAudit.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Umbral.ScoringAudit.Api.dll"]
