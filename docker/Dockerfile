# ============================================================
#  VibeMQ Message Broker — Docker Image
#  Uses VibeMQ.Server.Host with configuration from env and optional config file.
#
#  Build (from repo root):  docker build -f docker/Dockerfile -t vibemq .
#  Run:                    docker run -p 2925:2925 vibemq
#  Configure: set env vars (e.g. VibeMQ__Port=2925) or mount appsettings.json
# ============================================================

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first for better layer caching.
# Host lives in docker/; dependencies in src/.
COPY Directory.Build.props ./
COPY src/VibeMQ.Core/VibeMQ.Core.csproj src/VibeMQ.Core/
COPY src/VibeMQ.Server/VibeMQ.Server.csproj src/VibeMQ.Server/
COPY src/VibeMQ.Server.DependencyInjection/VibeMQ.Server.DependencyInjection.csproj src/VibeMQ.Server.DependencyInjection/
COPY src/VibeMQ.Server.Storage.Sqlite/VibeMQ.Server.Storage.Sqlite.csproj src/VibeMQ.Server.Storage.Sqlite/
COPY docker/VibeMQ.Server.Host/VibeMQ.Server.Host.csproj docker/VibeMQ.Server.Host/

# Restore and publish for net8.0 only (SDK 8.0 image does not support net10.0)
RUN dotnet restore docker/VibeMQ.Server.Host/VibeMQ.Server.Host.csproj -f net8.0

COPY src/ src/
COPY docker/ docker/

# Publish the Host (production-ready entry point with config support)
RUN dotnet publish docker/VibeMQ.Server.Host/VibeMQ.Server.Host.csproj \
    -c Release \
    -f net8.0 \
    -o /app/publish \
    --no-restore

# --- Runtime image ---
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Default broker port (override with VibeMQ__Port)
EXPOSE 2925

# Optional: mount config to /app/config/appsettings.json and set VIBEMQ_CONFIG_PATH=/app/config/appsettings.json
ENV VIBEMQ_CONFIG_PATH=

ENTRYPOINT ["dotnet", "VibeMQ.Server.Host.dll"]
