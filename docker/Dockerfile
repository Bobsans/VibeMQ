# ============================================================
#  VibeMQ Message Broker — Docker Image
#  Uses VibeMQ.Server.Host with configuration from env and optional config file.
#
#  Default build (slim, ~89 MB):
#    docker build -f docker/Dockerfile -t vibemq .
#  Framework-dependent (~112 MB):
#    docker build -f docker/Dockerfile --target runtime -t vibemq .
#  Run:  docker run -p 2925:2925 vibemq
#  Configure: set env vars (e.g. VibeMQ__Port=2925) or mount appsettings.json
# ============================================================

# ============================================================
# Build stages are split per target so default builds don't
# waste time producing unused publish variants.
# ============================================================

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-runtime
WORKDIR /src

# Copy project files first for better layer caching.
# Host lives in docker/; dependencies in src/.
COPY Directory.Build.props ./
COPY src/VibeMQ.Core/VibeMQ.Core.csproj src/VibeMQ.Core/
COPY src/VibeMQ.Server/VibeMQ.Server.csproj src/VibeMQ.Server/
COPY src/VibeMQ.Server.DependencyInjection/VibeMQ.Server.DependencyInjection.csproj src/VibeMQ.Server.DependencyInjection/
COPY src/VibeMQ.Server.Storage.Sqlite/VibeMQ.Server.Storage.Sqlite.csproj src/VibeMQ.Server.Storage.Sqlite/
COPY docker/VibeMQ.Server.Host/VibeMQ.Server.Host.csproj docker/VibeMQ.Server.Host/

RUN dotnet restore docker/VibeMQ.Server.Host/VibeMQ.Server.Host.csproj -p:TargetFramework=net10.0

COPY src/ src/
COPY docker/ docker/

# Publish the Host — framework-dependent (for stage "runtime")
RUN dotnet publish docker/VibeMQ.Server.Host/VibeMQ.Server.Host.csproj \
    -c Release \
    -p:TargetFramework=net10.0 \
    -p:DebugType=none \
    -p:DebugSymbols=false \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-ultra
WORKDIR /src

COPY Directory.Build.props ./
COPY src/VibeMQ.Core/VibeMQ.Core.csproj src/VibeMQ.Core/
COPY src/VibeMQ.Server/VibeMQ.Server.csproj src/VibeMQ.Server/
COPY src/VibeMQ.Server.DependencyInjection/VibeMQ.Server.DependencyInjection.csproj src/VibeMQ.Server.DependencyInjection/
COPY src/VibeMQ.Server.Storage.Sqlite/VibeMQ.Server.Storage.Sqlite.csproj src/VibeMQ.Server.Storage.Sqlite/
COPY docker/VibeMQ.Server.Host/VibeMQ.Server.Host.csproj docker/VibeMQ.Server.Host/

# Restore for linux-musl-x64 (needed for self-contained publish)
RUN dotnet restore docker/VibeMQ.Server.Host/VibeMQ.Server.Host.csproj -p:TargetFramework=net10.0 -r linux-musl-x64

COPY src/ src/
COPY docker/ docker/

# Publish self-contained trimmed single-file (default runtime target)
RUN dotnet publish docker/VibeMQ.Server.Host/VibeMQ.Server.Host.csproj \
    -c Release \
    -p:TargetFramework=net10.0 \
    -r linux-musl-x64 \
    --self-contained true \
    -p:PublishTrimmed=true \
    -p:TrimMode=partial \
    -p:PublishSingleFile=true \
    -p:EnableCompressionInSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:StripSymbols=true \
    -p:DebugType=none \
    -p:DebugSymbols=false \
    -o /app/publish-ultra \
    --no-restore

# --- Runtime: framework-dependent (~112 MB). Build with: docker build --target runtime -t vibemq . ---
FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS runtime
WORKDIR /app
COPY --from=build-runtime /app/publish .
EXPOSE 2925
ENV VIBEMQ_CONFIG_PATH=
ENTRYPOINT ["dotnet", "VibeMQ.Server.Host.dll"]

# --- Runtime ultra: self-contained trimmed single-file + runtime-deps (default target). ---
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine AS runtime-ultra
WORKDIR /app
COPY --from=build-ultra /app/publish-ultra .
RUN chmod +x ./VibeMQ.Server.Host
EXPOSE 2925
ENV VIBEMQ_CONFIG_PATH=
ENTRYPOINT ["./VibeMQ.Server.Host"]
