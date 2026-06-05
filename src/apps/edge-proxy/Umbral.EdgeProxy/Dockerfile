FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/edge-proxy/Umbral.EdgeProxy/Umbral.EdgeProxy.csproj src/edge-proxy/Umbral.EdgeProxy/
RUN dotnet restore src/edge-proxy/Umbral.EdgeProxy/Umbral.EdgeProxy.csproj

COPY src/edge-proxy/Umbral.EdgeProxy/ src/edge-proxy/Umbral.EdgeProxy/
RUN dotnet publish src/edge-proxy/Umbral.EdgeProxy/Umbral.EdgeProxy.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Umbral.EdgeProxy.dll"]
