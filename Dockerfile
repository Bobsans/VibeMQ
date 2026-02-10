# ============================================================
#  VibeMQ Message Broker — Docker Image
#  Build:  docker build -t vibemq .
#  Run:    docker run -p 8080:8080 vibemq
# ============================================================

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first for better layer caching
COPY VibeMQ.slnx ./
COPY Directory.Build.props ./
COPY src/VibeMQ.Core/VibeMQ.Core.csproj src/VibeMQ.Core/
COPY src/VibeMQ.Protocol/VibeMQ.Protocol.csproj src/VibeMQ.Protocol/
COPY src/VibeMQ.Server/VibeMQ.Server.csproj src/VibeMQ.Server/
COPY src/VibeMQ.Client/VibeMQ.Client.csproj src/VibeMQ.Client/
COPY src/VibeMQ.Health/VibeMQ.Health.csproj src/VibeMQ.Health/
COPY examples/VibeMQ.Example.Server/VibeMQ.Example.Server.csproj examples/VibeMQ.Example.Server/

# Restore only the server example (pulls transitive deps)
RUN dotnet restore examples/VibeMQ.Example.Server/VibeMQ.Example.Server.csproj

# Copy all source files
COPY src/ src/
COPY examples/VibeMQ.Example.Server/ examples/VibeMQ.Example.Server/

# Publish
RUN dotnet publish examples/VibeMQ.Example.Server/VibeMQ.Example.Server.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# --- Runtime image ---
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Broker TCP port
EXPOSE 8080

ENTRYPOINT ["dotnet", "VibeMQ.Example.Server.dll"]
