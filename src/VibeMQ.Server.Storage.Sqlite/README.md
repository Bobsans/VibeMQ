# VibeMQ.Server.Storage.Sqlite

SQLite persistence provider for the [VibeMQ](https://github.com/Bobsans/VibeMQ) message broker. Queues, messages, and dead-letter entries are stored in a single SQLite database file.

## Installation

```bash
dotnet add package VibeMQ.Server.Storage.Sqlite
```

## Quick start

```csharp
using VibeMQ.Server;
using VibeMQ.Server.Storage.Sqlite;

var broker = BrokerBuilder.Create()
    .UsePort(2925)
    .UseSqliteStorage(options => {
        options.DatabasePath = "vibemq.db";
        options.EnableWal = true;
    })
    .Build();

await broker.RunAsync(cancellationToken);
```

## Dependency Injection

Register the provider **before** `AddVibeMQBroker`:

```csharp
using VibeMQ.Server.DependencyInjection;
using VibeMQ.Server.Storage.Sqlite;

services.AddVibeMQSqliteStorage(options => {
    options.DatabasePath = "/data/vibemq.db";
});
services.AddVibeMQBroker(options => { options.Port = 2925; });
```

## Options

| Option           | Default      | Description                    |
|------------------|-------------|--------------------------------|
| `DatabasePath`   | `"vibemq.db"` | Path to the SQLite file       |
| `EnableWal`      | `true`      | WAL mode for better concurrency |
| `BusyTimeoutMs`  | `5000`      | Lock wait timeout (ms)        |

## Documentation

Full storage guide: [https://vibemq.readthedocs.io/](https://vibemq.readthedocs.io/) → Persistence & Storage.

## License

MIT. See [repository](https://github.com/Bobsans/VibeMQ) for details.
