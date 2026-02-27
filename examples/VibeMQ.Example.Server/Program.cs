using Microsoft.Extensions.Logging;
using VibeMQ.Enums;
using VibeMQ.Server;
using VibeMQ.Server.WebUI;

// ============================================================
//  VibeMQ Example Server
//  Demonstrates how to configure and run a message broker.
// ============================================================

using var loggerFactory = LoggerFactory.Create(builder => {
    builder
        .SetMinimumLevel(LogLevel.Debug)
        .AddConsole();
});

#pragma warning disable CS0618
var broker = BrokerBuilder.Create()
    .UsePort(2925)
    .UseAuthentication("my-secret-token")
    .UseMaxConnections(500)
    .UseMaxMessageSize(1_048_576) // 1 MB
    .ConfigureQueues(options => {
        options.DefaultDeliveryMode = DeliveryMode.RoundRobin;
        options.MaxQueueSize = 10_000;
        options.EnableAutoCreate = true;
    })
    .ConfigureRateLimiting(options => {
        options.Enabled = true;
        options.MaxConnectionsPerIpPerWindow = 50;
        options.MaxMessagesPerClientPerSecond = 1000;
    })
    .UseLoggerFactory(loggerFactory)
    .Build();
#pragma warning restore CS0618

Console.WriteLine("╔══════════════════════════════════════════╗");
Console.WriteLine("║          VibeMQ Message Broker            ║");
Console.WriteLine("╚══════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine("  Port:            2925");
Console.WriteLine("  Auth:            Enabled (token-based)");
Console.WriteLine("  Max connections: 500");
Console.WriteLine("  Queue auto-create: Yes");
Console.WriteLine("  Web UI:          http://localhost:12925/");
Console.WriteLine();
Console.WriteLine("Press Ctrl+C to shutdown gracefully...");
Console.WriteLine();

using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) => {
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine();
    Console.WriteLine("Shutting down...");
};

try {
    await broker.RunWithWebUIAsync(cts.Token);
} catch (OperationCanceledException) {
    // Expected
}

Console.WriteLine("Server stopped.");
