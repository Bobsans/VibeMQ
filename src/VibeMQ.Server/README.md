# VibeMQ.Server

Server-side library for running the [VibeMQ](https://github.com/Bobsans/VibeMQ) message broker. TCP-based broker with pub/sub, queues, delivery guarantees, and optional TLS/auth.

## Installation

```bash
dotnet add package VibeMQ.Server
```

## Quick start

```csharp
using VibeMQ.Server;
using VibeMQ.Core.Enums;

var broker = BrokerBuilder.Create()
    .UsePort(8080)
    .UseAuthentication("my-secret-token")
    .ConfigureQueues(options => {
        options.DefaultDeliveryMode = DeliveryMode.RoundRobin;
        options.MaxQueueSize = 10_000;
    })
    .Build();

await broker.RunAsync(cancellationToken);
```

## Documentation

Full documentation: [https://vibemq.readthedocs.io/](https://vibemq.readthedocs.io/)

## Dependency Injection

For ASP.NET Core / Worker Service, use **VibeMQ.Server.DependencyInjection** for `AddVibeMQBroker` and hosted broker setup.

## License

MIT. See [repository](https://github.com/Bobsans/VibeMQ) for details.
