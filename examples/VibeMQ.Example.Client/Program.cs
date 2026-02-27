using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using VibeMQ.Client;
using VibeMQ.Configuration;
using VibeMQ.Enums;
using VibeMQ.Interfaces;

// ============================================================
//  VibeMQ Example Client
//  Demonstrates pub/sub with different queue delivery modes
//  and message sizes. Run the Example.Server first.
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
Console.WriteLine("Connecting to broker at localhost:2925...");

#pragma warning disable CS0618
await using var client = await VibeMQClient.ConnectAsync("localhost", 2925, new ClientOptions {
    AuthToken = "my-secret-token",
    ReconnectPolicy = new ReconnectPolicy {
        MaxAttempts = 5,
        UseExponentialBackoff = true,
    },
    KeepAliveInterval = TimeSpan.FromSeconds(30),
}, logger);
#pragma warning restore CS0618

Console.WriteLine("Connected!");
Console.WriteLine();

// --- Create queues with different delivery modes ---
Console.WriteLine("Creating queues with different delivery modes...");

await client.CreateQueueAsync("notifications", new QueueOptions {
    Mode = DeliveryMode.RoundRobin,
    MaxQueueSize = 1000,
});
Console.WriteLine("  notifications (RoundRobin)");

await client.CreateQueueAsync("alerts", new QueueOptions {
    Mode = DeliveryMode.FanOutWithAck,
    MaxQueueSize = 5000,
});
Console.WriteLine("  alerts (FanOutWithAck)");

await client.CreateQueueAsync("broadcast", new QueueOptions {
    Mode = DeliveryMode.FanOutWithoutAck,
    MaxQueueSize = 1000,
});
Console.WriteLine("  broadcast (FanOutWithoutAck)");

await client.CreateQueueAsync("jobs", new QueueOptions {
    Mode = DeliveryMode.PriorityBased,
    MaxQueueSize = 2000,
});
Console.WriteLine("  jobs (PriorityBased)");

Console.WriteLine();

// --- Subscriptions ---
var notificationCount = 0;
// await using var subNotifications = await client.SubscribeAsync<Notification>("notifications", notification => {
//     var n = Interlocked.Increment(ref notificationCount);
//     Console.WriteLine($"  [notifications:{n}] {notification.Title} — {notification.Body} (priority: {notification.Priority})");
//     return Task.CompletedTask;
// });
// Console.WriteLine("Subscribed: notifications (RoundRobin, small messages).");
//
var alertCount = 0;
// await using var subAlerts = await client.SubscribeAsync<AlertEvent>("alerts", alert => {
//     var n = Interlocked.Increment(ref alertCount);
//     Console.WriteLine($"  [alerts:{n}] {alert.Source} | {alert.Level}: {alert.Message} (ts: {alert.Timestamp})");
//     return Task.CompletedTask;
// });
// Console.WriteLine("Subscribed: alerts (FanOutWithAck, medium messages).");
//
var broadcastCount = 0;
// await using var subBroadcast = await client.SubscribeAsync<BroadcastMessage>("broadcast", msg => {
//     var n = Interlocked.Increment(ref broadcastCount);
//     Console.WriteLine($"  [broadcast:{n}] {msg.Channel}: {msg.Text?.Length ?? 0} chars");
//     return Task.CompletedTask;
// });
// Console.WriteLine("Subscribed: broadcast (FanOutWithoutAck, no ack required).");
//
var jobCount = 0;
// await using var subJobs = await client.SubscribeAsync<JobPayload>("jobs", job => {
//     var n = Interlocked.Increment(ref jobCount);
//     var size = job.Payload is null ? 0 : job.Payload.Length;
//     Console.WriteLine($"  [jobs:{n}] id={job.JobId} type={job.JobType} payloadSize={size}");
//     return Task.CompletedTask;
// });
// Console.WriteLine("Subscribed: jobs (PriorityBased, variable size).");
//
// Console.WriteLine();

// --- Publish: small messages (notifications) ---
Console.WriteLine("Publishing small messages to 'notifications' (RoundRobin)...");
var smallNotifications = new[] {
    new Notification { Title = "Welcome", Body = "Connected to VibeMQ.", Priority = "low" },
    new Notification { Title = "Order #1234", Body = "Order shipped.", Priority = "normal" },
    new Notification { Title = "System alert", Body = "CPU > 90%.", Priority = "high" },
};
foreach (var n in smallNotifications) {
    await client.PublishAsync("notifications", n);
    Console.WriteLine($"  Published: {n.Title}");
    await Task.Delay(200);
}
Console.WriteLine();

// --- Publish: medium messages (alerts) ---
Console.WriteLine("Publishing medium messages to 'alerts' (FanOutWithAck)...");
var alerts = new[] {
    new AlertEvent {
        Source = "api-gateway",
        Level = "Warning",
        Message = "Rate limit approaching for client 192.168.1.1",
        Timestamp = DateTime.UtcNow.ToString("O"),
        Tags = new Dictionary<string, string> { ["env"] = "prod", ["region"] = "eu-west" },
    },
    new AlertEvent {
        Source = "database",
        Level = "Error",
        Message = "Connection pool exhausted; waiting for available connection.",
        Timestamp = DateTime.UtcNow.ToString("O"),
        Tags = new Dictionary<string, string> { ["db"] = "primary" },
    },
};
foreach (var a in alerts) {
    await client.PublishAsync("alerts", a);
    Console.WriteLine($"  Published: {a.Source} {a.Level}");
    await Task.Delay(200);
}
Console.WriteLine();

// --- Publish: broadcast (no ack) ---
Console.WriteLine("Publishing to 'broadcast' (FanOutWithoutAck)...");
await client.PublishAsync("broadcast", new BroadcastMessage {
    Channel = "system",
    Text = "Server maintenance window: 02:00–04:00 UTC. Brief disconnects possible.",
});
Console.WriteLine("  Published: system broadcast");
await Task.Delay(200);
Console.WriteLine();

// --- Publish: jobs with priority and variable size ---
Console.WriteLine("Publishing jobs with priority and variable size to 'jobs' (PriorityBased)...");

// Small job
await client.PublishAsync("jobs", new JobPayload { JobId = "job-1", JobType = "ping", Payload = "pong" },
    new Dictionary<string, string> { ["priority"] = "Low" });
Console.WriteLine("  Published: job-1 (Low, small)");

// Medium job (JSON body ~few hundred bytes)
var mediumPayload = new { data = new int[50], name = "batch-process", createdAt = DateTime.UtcNow };
await client.PublishAsync("jobs", new JobPayload {
    JobId = "job-2",
    JobType = "batch",
    Payload = System.Text.Json.JsonSerializer.Serialize(mediumPayload),
}, new Dictionary<string, string> { ["priority"] = "Normal" });
Console.WriteLine("  Published: job-2 (Normal, medium)");

// Large job (~10 KB)
var largeData = new string('x', 10_000);
await client.PublishAsync("jobs", new JobPayload {
    JobId = "job-3",
    JobType = "bulk",
    Payload = largeData,
}, new Dictionary<string, string> { ["priority"] = "High" });
Console.WriteLine("  Published: job-3 (High, large ~10KB)");

// Critical small job (should be delivered first with PriorityBased)
await client.PublishAsync("jobs", new JobPayload { JobId = "job-4", JobType = "urgent", Payload = "immediate" },
    new Dictionary<string, string> { ["priority"] = "Critical" });
Console.WriteLine("  Published: job-4 (Critical, small)");

Console.WriteLine();
Console.WriteLine("Waiting for messages to be delivered...");
await Task.Delay(3000);

Console.WriteLine();
Console.WriteLine("--- Summary ---");
Console.WriteLine($"  notifications received: {notificationCount}");
Console.WriteLine($"  alerts received:       {alertCount}");
Console.WriteLine($"  broadcast received:    {broadcastCount}");
Console.WriteLine($"  jobs received:         {jobCount}");
Console.WriteLine();
Console.WriteLine("Disconnecting...");
await client.DisconnectAsync();
Console.WriteLine("Done.");

// --- Message models ---

public class Notification {
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("body")]
    public string Body { get; set; } = "";

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = "normal";
}

public class AlertEvent {
    [JsonPropertyName("source")]
    public string Source { get; set; } = "";

    [JsonPropertyName("level")]
    public string Level { get; set; } = "";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";

    [JsonPropertyName("tags")]
    public Dictionary<string, string>? Tags { get; set; }
}

public class BroadcastMessage {
    [JsonPropertyName("channel")]
    public string Channel { get; set; } = "";

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

public class JobPayload {
    [JsonPropertyName("jobId")]
    public string JobId { get; set; } = "";

    [JsonPropertyName("jobType")]
    public string JobType { get; set; } = "";

    [JsonPropertyName("payload")]
    public string? Payload { get; set; }
}
