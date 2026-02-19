# VibeMQ.Server.DependencyInjection

[Microsoft.Extensions.DependencyInjection](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection) and hosting integration for [VibeMQ](https://github.com/Bobsans/VibeMQ) broker. Register and run the broker with `AddVibeMQBroker` in ASP.NET Core or Worker Service.

## Installation

```bash
dotnet add package VibeMQ.Server.DependencyInjection
```

Requires **VibeMQ.Server**.

## Quick start

```csharp
using VibeMQ.Server.DependencyInjection;
using VibeMQ.Core.Enums;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services => {
        services.AddVibeMQBroker(options => {
            options.Port = 8080;
            options.EnableAuthentication = true;
            options.AuthToken = "my-secret-token";
            options.QueueDefaults.DefaultDeliveryMode = DeliveryMode.RoundRobin;
        });
    })
    .Build();

await host.RunAsync();
```

## Documentation

Full documentation: [https://vibemq.readthedocs.io/](https://vibemq.readthedocs.io/) — see **DI Integration**.

## License

MIT. See [repository](https://github.com/Bobsans/VibeMQ) for details.
