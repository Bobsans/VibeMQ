# VibeMQ.Server.Storage.Redis

Redis persistence provider for the [VibeMQ](https://github.com/Bobsans/VibeMQ) message broker. Queues, messages, and dead-letter entries are stored in Redis (LIST + HASH structures) for low-latency persistence.

## Installation

```bash
dotnet add package VibeMQ.Server.Storage.Redis
```

## Quick start

```csharp
using VibeMQ.Server;
using VibeMQ.Server.Storage.Redis;

var broker = BrokerBuilder.Create()
    .UsePort(2925)
    .UseRedisStorage("localhost:6379", options => {
        options.Database = 0;
        options.KeyPrefix = "vibemq";
    })
    .Build();

await broker.RunAsync(cancellationToken);
```

## Dependency Injection

Register the provider **before** `AddVibeMQBroker`:

```csharp
using VibeMQ.Server.DependencyInjection;
using VibeMQ.Server.Storage.Redis;

services.AddVibeMQRedisStorage("localhost:6379", options => {
    options.KeyPrefix = "vibemq";
});
services.AddVibeMQBroker(options => { options.Port = 2925; });
```

With configuration (e.g. `VibeMQ:Storage:Redis` or `ConnectionStrings:Redis`):

```csharp
services.AddVibeMQRedisStorage(configuration.GetSection("VibeMQ:Storage:Redis"));
services.AddVibeMQBroker(options => { options.Port = 2925; });
```

## Options

| Option                | Default           | Description                    |
|-----------------------|-------------------|--------------------------------|
| `ConnectionString`    | `"localhost:6379"` | Redis connection string        |
| `Database`            | `0`               | Redis database number          |
| `KeyPrefix`           | `"vibemq"`        | Prefix for all keys            |
| `ConnectTimeoutMs`    | `5000`            | Connection timeout (ms)       |
| `SyncTimeoutMs`      | `5000`            | Per-operation timeout (ms)     |

## Documentation

Full storage guide: [https://vibemq.readthedocs.io/](https://vibemq.readthedocs.io/) → Persistence & Storage.

## License

MIT. See [repository](https://github.com/Bobsans/VibeMQ) for details.
