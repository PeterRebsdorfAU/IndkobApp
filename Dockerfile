# Multi-stage build af backend-API'et (ASP.NET Core 10) til Render.
# Bygges fra repo-roden (dockerContext: .).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Restore først (udnytter Docker-lag-cache)
COPY backend/IndkobsApp.Api/IndkobsApp.Api.csproj backend/IndkobsApp.Api/
RUN dotnet restore backend/IndkobsApp.Api/IndkobsApp.Api.csproj
# Resten af kildekoden + publish
COPY backend/IndkobsApp.Api/ backend/IndkobsApp.Api/
RUN dotnet publish backend/IndkobsApp.Api/IndkobsApp.Api.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production
# Render angiver porten via env-var PORT. Lyt på den (fallback 8080 ved lokal kørsel).
ENTRYPOINT ["/bin/sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} dotnet IndkobsApp.Api.dll"]
