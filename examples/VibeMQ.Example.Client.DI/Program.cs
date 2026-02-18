using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VibeMQ.Client;
using VibeMQ.Client.DependencyInjection;

// ============================================================
//  VibeMQ Example Client (with DI)
//  Uses the generic host and IVibeMQClientFactory for pub/sub.
//  Run Example.Server or Example.Server.DI first, then run this.
// ============================================================

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services => {
        services.AddVibeMQClient(settings => {
            settings.Host = "localhost";
            settings.Port = 8080;
            settings.ClientOptions.AuthToken = "my-secret-token";
            settings.ClientOptions.ReconnectPolicy = new ReconnectPolicy {
                MaxAttempts = 5,
                UseExponentialBackoff = true,
            };
            settings.ClientOptions.KeepAliveInterval = TimeSpan.FromSeconds(30);
        });
    })
    .ConfigureLogging(logging => {
        logging.SetMinimumLevel(LogLevel.Information);
        logging.AddConsole();
    })
    .Build();

Console.WriteLine("╔══════════════════════════════════════════╗");
Console.WriteLine("║     VibeMQ Example Client (DI)            ║");
Console.WriteLine("╚══════════════════════════════════════════╝");
Console.WriteLine();

var factory = host.Services.GetRequiredService<IVibeMQClientFactory>();

Console.WriteLine("Connecting to broker at localhost:8080...");
await using var client = await factory.CreateAsync();
Console.WriteLine("Connected!");
Console.WriteLine();

// Subscribe to notifications
var messageCount = 0;
await using var subscription = await client.SubscribeAsync<Notification>("notifications", notification => {
    var count = Interlocked.Increment(ref messageCount);
    Console.WriteLine($"  [{count}] Received: {notification.Title} — {notification.Body} (priority: {notification.Priority})");
    return Task.CompletedTask;
});

Console.WriteLine("Subscribed to 'notifications' queue.");
Console.WriteLine();

// Publish some messages
Console.WriteLine("Publishing messages...");
Console.WriteLine();

var notifications = new[] {
    new Notification { Title = "Welcome", Body = "You are now connected to VibeMQ!", Priority = "low" },
    new Notification { Title = "Order #1234", Body = "Your order has been shipped.", Priority = "normal" },
    new Notification { Title = "Payment received", Body = "We received your payment of $99.99.", Priority = "normal" },
    new Notification { Title = "System alert", Body = "CPU usage exceeded 90%.", Priority = "high" },
    new Notification { Title = "Deployment", Body = "New version v2.1.0 deployed.", Priority = "normal" },
};

foreach (var notification in notifications) {
    await client.PublishAsync("notifications", notification);
    Console.WriteLine($"  Published: {notification.Title}");
    await Task.Delay(500);
}

Console.WriteLine();
Console.WriteLine("Waiting for messages to be delivered...");
await Task.Delay(2000);

Console.WriteLine();
Console.WriteLine($"Total messages received: {messageCount}");
Console.WriteLine();
Console.WriteLine("Disconnecting...");
await client.DisconnectAsync();
Console.WriteLine("Done.");

// --- Message model ---

public class Notification {
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("body")]
    public string Body { get; set; } = "";

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = "normal";
}
