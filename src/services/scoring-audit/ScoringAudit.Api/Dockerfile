FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/services/scoring-audit/ScoringAudit.Api/ScoringAudit.Api.csproj src/services/scoring-audit/ScoringAudit.Api/
RUN dotnet restore src/services/scoring-audit/ScoringAudit.Api/ScoringAudit.Api.csproj

COPY src/services/scoring-audit/ScoringAudit.Api/ src/services/scoring-audit/ScoringAudit.Api/
RUN dotnet publish src/services/scoring-audit/ScoringAudit.Api/ScoringAudit.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ScoringAudit.Api.dll"]
