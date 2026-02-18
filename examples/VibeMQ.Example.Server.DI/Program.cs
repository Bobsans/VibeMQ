using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VibeMQ.Core.Enums;
using VibeMQ.Server.DependencyInjection;

// ============================================================
//  VibeMQ Example Server (with DI)
//  Runs the broker using the generic host and Microsoft DI.
//  Same behaviour as Example.Server, configured via AddVibeMQBroker.
// ============================================================

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services => {
        services.AddVibeMQBroker(options => {
            options.Port = 8080;
            options.EnableAuthentication = true;
            options.AuthToken = "my-secret-token";
            options.MaxConnections = 500;
            options.MaxMessageSize = 1_048_576; // 1 MB
            options.QueueDefaults.DefaultDeliveryMode = DeliveryMode.RoundRobin;
            options.QueueDefaults.MaxQueueSize = 10_000;
            options.QueueDefaults.EnableAutoCreate = true;
            options.RateLimit.Enabled = true;
            options.RateLimit.MaxConnectionsPerIpPerWindow = 50;
            options.RateLimit.MaxMessagesPerClientPerSecond = 1000;
        });
    })
    .ConfigureLogging(logging => {
        logging.SetMinimumLevel(LogLevel.Debug);
        logging.AddConsole();
    })
    .Build();

Console.WriteLine("╔══════════════════════════════════════════╗");
Console.WriteLine("║     VibeMQ Message Broker (DI)           ║");
Console.WriteLine("╚══════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine("  Port:            8080");
Console.WriteLine("  Auth:            Enabled (token-based)");
Console.WriteLine("  Max connections: 500");
Console.WriteLine("  Queue auto-create: Yes");
Console.WriteLine();
Console.WriteLine("Press Ctrl+C to shutdown gracefully...");
Console.WriteLine();

await host.RunAsync();
