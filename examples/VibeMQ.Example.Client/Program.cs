using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using VibeMQ.Client;
using VibeMQ.Interfaces;

// ============================================================
//  VibeMQ Example Client
//  Demonstrates pub/sub messaging with the VibeMQ broker.
//  Run the Example.Server first, then run this client.
// ============================================================

using var loggerFactory = LoggerFactory.Create(builder => {
    builder
        .SetMinimumLevel(LogLevel.Information)
        .AddConsole();
});

var logger = loggerFactory.CreateLogger<VibeMQClient>();

Console.WriteLine("╔══════════════════════════════════════════╗");
Console.WriteLine("║          VibeMQ Example Client            ║");
Console.WriteLine("╚══════════════════════════════════════════╝");
Console.WriteLine();

// Connect to broker
Console.WriteLine("Connecting to broker at localhost:8080...");

await using var client = await VibeMQClient.ConnectAsync("localhost", 8080, new ClientOptions {
    AuthToken = "my-secret-token",
    ReconnectPolicy = new ReconnectPolicy {
        MaxAttempts = 5,
        UseExponentialBackoff = true,
    },
    KeepAliveInterval = TimeSpan.FromSeconds(30),
}, logger);

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

// Subscribe using class-based handler
var classBasedMessageCount = 0;
NotificationClassBasedHandler.OnHandled = notification => {
    var count = Interlocked.Increment(ref classBasedMessageCount);
    Console.WriteLine($"  [class:{count}] Received: {notification.Title} — {notification.Body} (priority: {notification.Priority})");
};

await using var classBasedSubscription = await client.SubscribeAsync<Notification, NotificationClassBasedHandler>("notifications.class-based");

Console.WriteLine("Subscribed to 'notifications.class-based' queue (class-based handler).");
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
    await Task.Delay(500); // Small delay for readability
}

await client.PublishAsync("notifications.class-based", new Notification {
    Title = "Class-based subscription",
    Body = "This message was processed by NotificationClassBasedHandler.",
    Priority = "normal",
});
Console.WriteLine("  Published: Class-based subscription");

Console.WriteLine();
Console.WriteLine("Waiting for messages to be delivered...");
await Task.Delay(2000);

Console.WriteLine();
Console.WriteLine($"Total messages received: {messageCount}");
Console.WriteLine($"Total class-based messages received: {classBasedMessageCount}");
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

public sealed class NotificationClassBasedHandler : IMessageHandler<Notification> {
    public static Action<Notification>? OnHandled { get; set; }

    public Task HandleAsync(Notification message, CancellationToken cancellationToken) {
        OnHandled?.Invoke(message);
        return Task.CompletedTask;
    }
}
