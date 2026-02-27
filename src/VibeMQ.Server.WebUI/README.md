# VibeMQ.Server.WebUI

Optional Web dashboard for the VibeMQ broker. Serves a Vue 3 SPA and REST API for health, metrics, and queues on a separate HTTP port (default **12925**).

## Quick start

```csharp
var broker = BrokerBuilder.Create()
    .UsePort(2925)
    .UseLoggerFactory(loggerFactory)
    .Build();

await broker.RunWithWebUIAsync(); // Web UI on http://localhost:12925/
```

With options:

```csharp
await broker.RunWithWebUIAsync(new WebUIOptions {
    Port = 12925,
    Enabled = true,
    PathPrefix = "/",
}, cancellationToken);
```

Or run the Web UI server manually:

```csharp
var webUi = new WebUIServer(broker, new WebUIOptions { Port = 12925 }, logger);
webUi.Start();
await broker.RunAsync(cancellationToken);
await webUi.DisposeAsync();
```

## API endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/health` | Broker health (connections, queues, in-flight, memory) |
| GET | `/api/metrics` | Full metrics snapshot (counters, gauges, latency, uptime) |
| GET | `/api/queues` | List of queue names |
| GET | `/api/queues/{name}` | Single queue metadata (message count, subscribers, etc.) |

All JSON responses use `snake_case` property names.

## Building the frontend

The dashboard UI is a Vue 3 + Vite SPA. **The frontend is built automatically** when you build the .NET project: an MSBuild target runs `npm install` and `npm run build` in `App/` before the main build, and the contents of `App/dist/` are embedded into the assembly.

The build is incremental: changing any file under `App/src/` (or `App/index.html`, Vite config, or `package*.json`) will trigger a rebuild of the Web UI project and rerun the frontend build as needed.

To skip the frontend build (e.g. in CI where Node.js is not installed, and you rely on pre-built assets):

```bash
dotnet build -p:SkipFrontendBuild=true
```

If `dist/` is missing and the frontend build was skipped, the project still builds but the UI returns a 503 with instructions.

## Requirements

- .NET 8.0 or later
- VibeMQ.Server (and transitively VibeMQ.Core)

No ASP.NET Core or Node.js at runtime; Node/npm only for building the frontend.
