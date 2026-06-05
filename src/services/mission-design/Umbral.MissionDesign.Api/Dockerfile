FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/mission-design/service/Umbral.MissionDesign.Api/Umbral.MissionDesign.Api.csproj src/mission-design/service/Umbral.MissionDesign.Api/
RUN dotnet restore src/mission-design/service/Umbral.MissionDesign.Api/Umbral.MissionDesign.Api.csproj

COPY src/mission-design/service/Umbral.MissionDesign.Api/ src/mission-design/service/Umbral.MissionDesign.Api/
RUN dotnet publish src/mission-design/service/Umbral.MissionDesign.Api/Umbral.MissionDesign.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Umbral.MissionDesign.Api.dll"]
