# VibeMQ.Client.DependencyInjection

[Microsoft.Extensions.DependencyInjection](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection) integration for [VibeMQ](https://github.com/Bobsans/VibeMQ) client. Register the client and use `IVibeMQClientFactory` in ASP.NET Core or Worker Service.

## Installation

```bash
dotnet add package VibeMQ.Client.DependencyInjection
```

Requires **VibeMQ.Client**.

## Quick start

```csharp
using VibeMQ.Client.DependencyInjection;

// In Program.cs or Startup
services.AddVibeMQClient(settings => {
    settings.Host = "localhost";
    settings.Port = 8080;
    settings.ClientOptions.AuthToken = "your-token";
});

// In a service
var factory = serviceProvider.GetRequiredService<IVibeMQClientFactory>();
await using var client = await factory.CreateAsync();
await client.PublishAsync("queue", message);
```

## Documentation

Full documentation: [https://vibemq.readthedocs.io/](https://vibemq.readthedocs.io/) — see **DI Integration**.

## License

MIT. See [repository](https://github.com/Bobsans/VibeMQ) for details.
