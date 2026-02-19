# VibeMQ.Client

Client library for connecting to [VibeMQ](https://github.com/Bobsans/VibeMQ) message broker. Publish and subscribe to queues with automatic reconnection and keep-alive.

## Installation

```bash
dotnet add package VibeMQ.Client
```

## Quick start

```csharp
using VibeMQ.Client;

await using var client = await VibeMQClient.ConnectAsync(
    "localhost",
    8080,
    new ClientOptions { AuthToken = "your-token" }
);

// Publish
await client.PublishAsync("notifications", new { Title = "Hello", Body = "World" });

// Subscribe
await using var sub = await client.SubscribeAsync<dynamic>("notifications", msg => {
    Console.WriteLine(msg.Title);
    return Task.CompletedTask;
});
```

## Documentation

Full documentation: [https://vibemq.readthedocs.io/](https://vibemq.readthedocs.io/)

## Dependency Injection

For ASP.NET Core / Worker Service, use **VibeMQ.Client.DependencyInjection** for `IVibeMQClientFactory` and registration helpers.

## License

MIT. See [repository](https://github.com/Bobsans/VibeMQ) for details.
