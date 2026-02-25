<div align="center">

# VibeMQ

**A simple yet reliable message broker for .NET**

[![NuGet](https://img.shields.io/nuget/v/VibeMQ.Client?label=NuGet&color=blue)](https://www.nuget.org/packages/VibeMQ.Client)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8%20%7C%2010-purple)](https://dotnet.microsoft.com)
[![Docs](https://img.shields.io/badge/docs-readthedocs-blue)](https://vibemq.readthedocs.io)

VibeMQ is a lightweight, embeddable TCP message broker for .NET. Drop it into your process or run it standalone — no external dependencies required.

</div>

---

## Features

- **📨 Publish / Subscribe** — JSON payloads over a custom binary framing protocol
- **🔀 Delivery modes** — Round-robin, fan-out (with / without ACK), priority-based
- **✅ Delivery guarantees** — Per-message ACK with configurable retry and dead-letter queue
- **🔒 Authorization** — Username + password auth (BCrypt) with per-queue ACL and glob patterns
- **🗜️ Compression** — GZip / Brotli negotiated at handshake; per-message threshold
- **💾 Persistence** — Pluggable storage; SQLite provider included
- **🔄 Auto-reconnect** — Exponential backoff with configurable policy
- **📋 Queue declarations** — Client-side provisioning with conflict resolution
- **📊 Health checks** — HTTP endpoints for Kubernetes / orchestrators
- **🚦 Rate limiting** — Per-IP and per-connection throttling
- **🔐 TLS** — Optional SSL/TLS for encrypted connections
- **💉 DI-first** — `Microsoft.Extensions.*` integration for both server and client

---

## Quick Start

### 1. Install packages

```
dotnet add package VibeMQ.Server.DependencyInjection
dotnet add package VibeMQ.Client.DependencyInjection
```

### 2. Start the broker

```csharp
using VibeMQ.Server;

var broker = BrokerBuilder.Create()
    .UsePort(2925)
    .UseAuthorization(o => {
        o.SuperuserUsername = "admin";
        o.SuperuserPassword = "changeme";
    })
    .Build();

await broker.RunAsync();
```

<details>
<summary>With <code>Microsoft.Extensions.Hosting</code></summary>

```csharp
builder.Services.AddVibeMQBroker(o => {
    o.Port = 2925;
    o.Authorization = new AuthorizationOptions {
        SuperuserUsername = "admin",
        SuperuserPassword = "changeme",
    };
});
```

</details>

### 3. Connect and publish

```csharp
using VibeMQ.Client;

var client = await VibeMQClient.ConnectAsync("localhost", 2925, new ClientOptions {
    Username = "admin",
    Password = "changeme",
});

// Subscribe
await client.SubscribeAsync<MyMessage>("notifications", async msg => {
    Console.WriteLine($"Received: {msg.Body}");
});

// Publish
await client.PublishAsync("notifications", new MyMessage { Body = "Hello, VibeMQ!" });
```

<details>
<summary>With <code>Microsoft.Extensions.Hosting</code></summary>

```csharp
builder.Services.AddVibeMQClient(o => {
    o.Host = "localhost";
    o.Port = 2925;
    o.Username = "admin";
    o.Password = "changeme";
});

// In your service
public class MyWorker(IVibeMQClient client) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await client.SubscribeAsync<MyMessage>("notifications", Handle, ct);
        await client.PublishAsync("notifications", new MyMessage { Body = "Hello!" }, ct);
    }
}
```

</details>

---

## NuGet Packages

| Package | Description |
|---|---|
| `VibeMQ.Client` | Client library |
| `VibeMQ.Client.DependencyInjection` | `IServiceCollection` extensions for the client |
| `VibeMQ.Server` | Broker server |
| `VibeMQ.Server.DependencyInjection` | `IServiceCollection` extensions for the broker |
| `VibeMQ.Server.Storage.Sqlite` | SQLite persistence provider |
| `VibeMQ.Core` | Shared protocol, models, and interfaces |

---

## Delivery Modes

| Mode | Behavior |
|---|---|
| `RoundRobin` | Each message goes to exactly one subscriber, cycling through the list |
| `FanOutWithAck` | All subscribers receive a copy; each must ACK before the next delivery |
| `FanOutWithoutAck` | All subscribers receive a copy; no ACK required |
| `PriorityBased` | Messages are dequeued in `Critical → High → Normal → Low` order |

```csharp
await client.CreateQueueAsync("tasks", new QueueOptions {
    DeliveryMode = DeliveryMode.FanOutWithAck,
    MaxSize = 10_000,
    OverflowStrategy = OverflowStrategy.RedirectToDlq,
});
```

---

## Authorization

VibeMQ 1.6+ supports per-queue access control with glob patterns.

```csharp
// Server — enable authorization
BrokerBuilder.Create()
    .UseAuthorization(o => {
        o.SuperuserUsername = "admin";
        o.SuperuserPassword = "s3cr3t";
    })
    .Build();

// Superuser — grant permissions to a regular user
await client.AdminGrantPermissionAsync("alice", "orders.*",
    [QueueOperation.Publish, QueueOperation.Subscribe]);

// alice — can publish/subscribe to orders.*, but nothing else
var aliceClient = await VibeMQClient.ConnectAsync("localhost", 2925, new ClientOptions {
    Username = "alice",
    Password = "alicepass",
});
```

**Pattern syntax** — `*` matches any sequence of characters, including dots:

| Pattern | Matches | Doesn't match |
|---|---|---|
| `*` | everything | — |
| `orders.*` | `orders.events`, `orders.dlq` | `orders` |
| `tenant.*.logs` | `tenant.alice.logs` | `tenant.logs` |
| `notifications` | `notifications` | `notifications.v2` |

---

## Compression

Compression is negotiated per-connection and applied automatically above a configurable threshold.

```csharp
// Client — declare preferred algorithms
var client = await VibeMQClient.ConnectAsync("localhost", 2925, new ClientOptions {
    PreferredCompressions = [CompressionAlgorithm.Brotli, CompressionAlgorithm.GZip],
    CompressionThreshold = 1024, // bytes — skip compression for small messages
});

// Server — advertise supported algorithms
BrokerBuilder.Create()
    .UsePort(2925)
    .ConfigureOptions(o => {
        o.SupportedCompressions = [CompressionAlgorithm.Brotli, CompressionAlgorithm.GZip];
    })
    .Build();
```

---

## Queue Declarations

Declare queues on the client and have them auto-provisioned at connect time.

```csharp
var client = await VibeMQClient.ConnectAsync("localhost", 2925, new ClientOptions {
    QueueDeclarations = [
        new QueueDeclaration("tasks") {
            DeliveryMode   = DeliveryMode.RoundRobin,
            MaxSize        = 5_000,
            ConflictPolicy = QueueConflictPolicy.Ignore,
        },
    ],
});
```

---

## Persistence

SQLite persistence is enabled by adding the storage package and calling one method.

```csharp
dotnet add package VibeMQ.Server.Storage.Sqlite
```

```csharp
using VibeMQ.Server.Storage.Sqlite;

BrokerBuilder.Create()
    .UseStorageProvider(SqliteStorageProviderFactory.Create("broker.db"))
    .Build();
```

Messages, DLQ entries, and auth data all persist across restarts.

---

## Auto-Reconnect

The client transparently reconnects on connection loss.

```csharp
var client = await VibeMQClient.ConnectAsync("localhost", 2925, new ClientOptions {
    Reconnect = new ReconnectPolicy {
        Enabled     = true,
        MaxAttempts = 10,
        InitialDelay = TimeSpan.FromSeconds(1),
        MaxDelay     = TimeSpan.FromSeconds(30),
        BackoffFactor = 2.0,
    },
});
```

---

## Health Checks

```csharp
// Server
BrokerBuilder.Create()
    .ConfigureHealthChecks(h => {
        h.Enabled = true;
        h.Port    = 8081;
        h.Path    = "/health";
    })
    .Build();
```

`GET http://localhost:8081/health` returns `200 OK` with broker stats in JSON.

---

## Admin API

Superusers have access to user and permission management commands directly from the client:

```csharp
// User management
await client.AdminCreateUserAsync("alice", "password");
await client.AdminChangePasswordAsync("alice", "newpassword");
await client.AdminDeleteUserAsync("alice");

// Permission management
await client.AdminGrantPermissionAsync("alice", "orders.*",
    [QueueOperation.Publish, QueueOperation.Subscribe]);
await client.AdminRevokePermissionAsync("alice", "orders.*");

// Listing
var users = await client.AdminListUsersAsync();
var perms = await client.AdminGetUserPermissionsAsync("alice");
```

---

## Class-Based Subscriptions

```csharp
// Decorate with [Queue] for automatic subscription on connect
[Queue("notifications")]
public class NotificationHandler : IMessageHandler<Notification>
{
    public Task HandleAsync(Notification message, CancellationToken ct)
    {
        Console.WriteLine($"[{message.Priority}] {message.Title}");
        return Task.CompletedTask;
    }
}

// Register with the client
await client.SubscribeAsync<Notification, NotificationHandler>("notifications");
```

---

## Configuration Reference

<details>
<summary>Full server options</summary>

```csharp
BrokerBuilder.Create()
    .UsePort(2925)
    .UseMaxConnections(1000)
    .UseMaxMessageSize(1 * 1024 * 1024)   // 1 MB
    .UseAuthorization(o => {
        o.SuperuserUsername = "admin";
        o.SuperuserPassword = "changeme";
        o.DatabasePath      = "auth.db";
    })
    .ConfigureQueues(q => {
        q.DefaultDeliveryMode     = DeliveryMode.RoundRobin;
        q.DefaultMaxSize          = 10_000;
        q.DefaultOverflowStrategy = OverflowStrategy.DropOldest;
        q.AutoCreate              = true;
    })
    .ConfigureRateLimiting(r => {
        r.Enabled                     = true;
        r.MaxMessagesPerSecondPerClient = 500;
    })
    .ConfigureHealthChecks(h => {
        h.Enabled = true;
        h.Port    = 8081;
    })
    .UseTls(t => {
        t.Enabled             = true;
        t.CertificatePath     = "cert.pfx";
        t.CertificatePassword = "certpass";
    })
    .UseStorageProvider(SqliteStorageProviderFactory.Create("broker.db"))
    .Build();
```

</details>

<details>
<summary>Full client options</summary>

```csharp
new ClientOptions {
    // Auth
    Username = "alice",
    Password = "secret",

    // Reconnect
    Reconnect = new ReconnectPolicy {
        Enabled      = true,
        MaxAttempts  = 10,
        InitialDelay = TimeSpan.FromSeconds(1),
        MaxDelay     = TimeSpan.FromSeconds(30),
        BackoffFactor = 2.0,
    },

    // Keep-alive
    PingInterval = TimeSpan.FromSeconds(30),

    // Compression
    PreferredCompressions = [CompressionAlgorithm.Brotli],
    CompressionThreshold  = 1024,

    // Queue declarations
    QueueDeclarations = [
        new QueueDeclaration("my-queue") {
            DeliveryMode   = DeliveryMode.FanOutWithAck,
            ConflictPolicy = QueueConflictPolicy.Ignore,
        },
    ],

    // Timeouts
    CommandTimeout = TimeSpan.FromSeconds(10),

    // TLS
    UseTls              = true,
    SkipCertValidation  = false,
}
```

</details>

---

## Documentation

Full documentation is available at **[vibemq.readthedocs.io](https://vibemq.readthedocs.io)**, including:

- [Getting Started](https://vibemq.readthedocs.io/en/latest/docs/getting-started.html)
- [Server Setup](https://vibemq.readthedocs.io/en/latest/docs/server-setup.html)
- [Client Usage](https://vibemq.readthedocs.io/en/latest/docs/client-usage.html)
- [Authorization](https://vibemq.readthedocs.io/en/latest/docs/authorization.html)
- [Configuration Reference](https://vibemq.readthedocs.io/en/latest/docs/configuration.html)
- [Storage Providers](https://vibemq.readthedocs.io/en/latest/docs/storage.html)
- [Protocol Specification](https://vibemq.readthedocs.io/en/latest/docs/protocol.html)
- [Changelog](https://vibemq.readthedocs.io/en/latest/docs/changelog.html)

---

## License

VibeMQ is licensed under the [MIT License](LICENSE).
