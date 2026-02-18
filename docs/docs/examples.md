# Examples

Примеры использования VibeMQ.

## Примеры из репозитория

В репозитории доступны следующие примеры:

| Пример | Описание |
|--------|----------|
| `VibeMQ.Example.Server` | Простой сервер с консольным выводом |
| `VibeMQ.Example.Server.DI` | Сервер с DI-интеграцией |
| `VibeMQ.Example.Client` | Простой клиент с pub/sub |
| `VibeMQ.Example.Client.DI` | Клиент с DI-интеграцией |

## Пример 1: Простой чат

### Сервер

```csharp
using VibeMQ.Server;

var broker = BrokerBuilder.Create()
    .UsePort(8080)
    .Build();

_ = Task.Run(() => broker.RunAsync());

Console.WriteLine("Chat server started on port 8080");
Console.WriteLine("Press any key to exit...");
Console.ReadKey();
```

### Клиент 1 (Отправитель)

```csharp
using VibeMQ.Client;

await using var sender = await VibeMQClient.ConnectAsync("localhost", 8080);

Console.WriteLine("Enter messages (type 'exit' to quit):");

while (true) {
    var message = Console.ReadLine();
    if (message == "exit") break;

    await sender.PublishAsync("chat", new {
        User = "Alice",
        Message = message,
        Timestamp = DateTime.UtcNow
    });
}
```

### Клиент 2 (Получатель)

```csharp
using VibeMQ.Client;

await using var receiver = await VibeMQClient.ConnectAsync("localhost", 8080);

await using var subscription = await receiver.SubscribeAsync<ChatMessage>("chat", msg => {
    Console.WriteLine($"[{msg.Timestamp}] {msg.User}: {msg.Message}");
    return Task.CompletedTask;
});

Console.WriteLine("Waiting for messages... (press any key to exit)");
Console.ReadKey();

public class ChatMessage {
    public string User { get; set; }
    public string Message { get; set; }
    public DateTime Timestamp { get; set; }
}
```

## Пример 2: Worker Queue

### Сервер

```csharp
using VibeMQ.Core.Enums;
using VibeMQ.Server;

var broker = BrokerBuilder.Create()
    .UsePort(8080)
    .ConfigureQueues(opts => {
        opts.DefaultDeliveryMode = DeliveryMode.RoundRobin;
        opts.MaxQueueSize = 10_000;
        opts.EnableDeadLetterQueue = true;
    })
    .Build();

await broker.RunAsync();
```

### Publisher (Task Producer)

```csharp
using VibeMQ.Client;

await using var client = await VibeMQClient.ConnectAsync("localhost", 8080);

// Публикация задач
for (int i = 0; i < 100; i++) {
    await client.PublishAsync("tasks", new TaskData {
        Id = Guid.NewGuid(),
        Type = "ProcessImage",
        Priority = i % 10 == 0 ? "high" : "normal",
        Payload = new {
            ImageUrl = $"https://example.com/image{i}.jpg",
            Operation = "resize"
        }
    });

    Console.WriteLine($"Published task {i}");
}

Console.WriteLine("All tasks published");

public class TaskData {
    public Guid Id { get; set; }
    public string Type { get; set; }
    public string Priority { get; set; }
    public object Payload { get; set; }
}
```

### Worker (Task Consumer)

```csharp
using VibeMQ.Client;

await using var client = await VibeMQClient.ConnectAsync("localhost", 8080);

await using var subscription = await client.SubscribeAsync<TaskData>("tasks", async task => {
    Console.WriteLine($"Processing task {task.Id} (type: {task.Type})");

    // Имитация обработки
    await Task.Delay(100);

    Console.WriteLine($"Task {task.Id} completed");
});

Console.WriteLine("Worker started. Press any key to exit...");
Console.ReadKey();
```

## Пример 3: Pub/Sub уведомления

### Сервер

```csharp
using VibeMQ.Core.Enums;
using VibeMQ.Server;

var broker = BrokerBuilder.Create()
    .UsePort(8080)
    .ConfigureQueues(opts => {
        opts.DefaultDeliveryMode = DeliveryMode.FanOutWithAck;
    })
    .Build();

await broker.RunAsync();
```

### Event Publisher

```csharp
using VibeMQ.Client;

await using var client = await VibeMQClient.ConnectAsync("localhost", 8080);

// Публикация события
await client.PublishAsync("events.user.created", new {
    UserId = "12345",
    Email = "user@example.com",
    CreatedAt = DateTime.UtcNow
});

Console.WriteLine("Event published");
```

### Email Service (Subscriber 1)

```csharp
using VibeMQ.Client;

await using var client = await VibeMQClient.ConnectAsync("localhost", 8080);

await using var subscription = await client.SubscribeAsync<UserEvent>("events.user.created", async evt => {
    Console.WriteLine($"Sending welcome email to {evt.Email}");
    // await emailService.SendAsync(...);
});

Console.WriteLine("Email service started");
await Task.Delay(Timeout.Infinite);

public class UserEvent {
    public string UserId { get; set; }
    public string Email { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### Analytics Service (Subscriber 2)

```csharp
using VibeMQ.Client;

await using var client = await VibeMQClient.ConnectAsync("localhost", 8080);

await using var subscription = await client.SubscribeAsync<UserEvent>("events.user.created", async evt => {
    Console.WriteLine($"Tracking user creation: {evt.UserId}");
    // await analytics.TrackAsync(...);
});

Console.WriteLine("Analytics service started");
await Task.Delay(Timeout.Infinite);
```

## Пример 4: Request/Response

### Сервер

```csharp
using VibeMQ.Server;

var broker = BrokerBuilder.Create()
    .UsePort(8080)
    .Build();

await broker.RunAsync();
```

### RPC Server (Responder)

```csharp
using VibeMQ.Client;

await using var client = await VibeMQClient.ConnectAsync("localhost", 8080);

// Подписка на запросы
await using var subscription = await client.SubscribeAsync<CalculateRequest>("requests", async req => {
    Console.WriteLine($"Received request: {req.Operation}({req.A}, {req.B})");

    var result = req.Operation switch {
        "add" => req.A + req.B,
        "subtract" => req.A - req.B,
        "multiply" => req.A * req.B,
        "divide" => req.A / req.B,
        _ => 0
    };

    // Публикация ответа
    await client.PublishAsync("responses", new CalculateResponse {
        CorrelationId = req.CorrelationId,
        Result = result
    });
});

Console.WriteLine("RPC server started");
await Task.Delay(Timeout.Infinite);

public class CalculateRequest {
    public string CorrelationId { get; set; }
    public string Operation { get; set; }
    public double A { get; set; }
    public double B { get; set; }
}

public class CalculateResponse {
    public string CorrelationId { get; set; }
    public double Result { get; set; }
}
```

### RPC Client (Caller)

```csharp
using VibeMQ.Client;

await using var client = await VibeMQClient.ConnectAsync("localhost", 8080);

var correlationId = Guid.NewGuid().ToString();
var tcs = new TaskCompletionSource<CalculateResponse>();

// Подписка на ответы
await using var subscription = await client.SubscribeAsync<CalculateResponse>("responses", async resp => {
    if (resp.CorrelationId == correlationId) {
        tcs.SetResult(resp);
    }
    await Task.CompletedTask;
});

// Публикация запроса
await client.PublishAsync("requests", new CalculateRequest {
    CorrelationId = correlationId,
    Operation = "add",
    A = 10,
    B = 20
});

// Ожидание ответа
var response = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
Console.WriteLine($"Result: {response.Result}");
```

## Пример 5: DI Integration

### Server с DI

```csharp
using VibeMQ.Server.DependencyInjection;
using VibeMQ.Core.Enums;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddVibeMQBroker(options => {
    options.Port = 8080;
    options.QueueDefaults = new QueueDefaults {
        DefaultDeliveryMode = DeliveryMode.RoundRobin,
        EnableAutoCreate = true,
    };
});

builder.Services.AddHostedService<QueueInitializerService>();

await builder.Build().RunAsync();

public class QueueInitializerService : IHostedService {
    private readonly IQueueManager _queueManager;

    public QueueInitializerService(IQueueManager queueManager) {
        _queueManager = queueManager;
    }

    public async Task StartAsync(CancellationToken cancellationToken) {
        await _queueManager.CreateQueueAsync("orders", new QueueOptions {
            Mode = DeliveryMode.RoundRobin,
            MaxQueueSize = 5000,
            EnableDeadLetterQueue = true,
        }, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

### Client с DI

```csharp
using VibeMQ.Client.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddVibeMQClient(options => {
    options.Host = "localhost";
    options.Port = 8080;
});

builder.Services.AddHostedService<OrderWorker>();

await builder.Build().RunAsync();

public class OrderWorker : BackgroundService {
    private readonly IVibeMQClientFactory _clientFactory;
    private readonly ILogger<OrderWorker> _logger;

    public OrderWorker(
        IVibeMQClientFactory clientFactory,
        ILogger<OrderWorker> logger)
    {
        _clientFactory = clientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        await using var client = await _clientFactory.CreateAsync(stoppingToken);

        await using var subscription = await client.SubscribeAsync<Order>("orders", async order => {
            _logger.LogInformation("Processing order {OrderId}", order.Id);
            await ProcessOrderAsync(order);
        }, stoppingToken);

        _logger.LogInformation("Order worker started");
    }

    private Task ProcessOrderAsync(Order order) {
        // Обработка заказа
        return Task.CompletedTask;
    }
}

public class Order {
    public string Id { get; set; }
    public decimal Amount { get; set; }
}
```

## Пример 6: Monitoring

### Metrics Reporter

```csharp
using VibeMQ.Server;
using VibeMQ.Core.Metrics;

var broker = BrokerBuilder.Create()
    .UsePort(8080)
    .ConfigureHealthChecks(opts => {
        opts.Enabled = true;
        opts.Port = 8081;
    })
    .Build();

// Запуск метрик репортёра
_ = Task.Run(async () => {
    while (true) {
        await Task.Delay(TimeSpan.FromSeconds(10));

        var metrics = broker.Metrics;
        Console.WriteLine($@"
=== VibeMQ Metrics ===
Published:  {metrics.TotalMessagesPublished}
Delivered:  {metrics.TotalMessagesDelivered}
Acknowledged: {metrics.TotalAcknowledged}
Errors:     {metrics.TotalErrors}
Connections: {metrics.ActiveConnections}
Queues:     {metrics.ActiveQueues}
Memory:     {metrics.MemoryUsageBytes / 1024 / 1024} MB
");
    }
});

await broker.RunAsync();
```

## Запуск примеров

### Из репозитория

```bash
# Сервер
cd examples/VibeMQ.Example.Server
dotnet run

# Клиент (в другом терминале)
cd examples/VibeMQ.Example.Client
dotnet run
```

### DI примеры

```bash
# Сервер с DI
cd examples/VibeMQ.Example.Server.DI
dotnet run

# Клиент с DI (в другом терминале)
cd examples/VibeMQ.Example.Client.DI
dotnet run
```

## См. также

- [Getting Started](getting-started.md) — начало работы
- [Dependency Injection](dependency-injection.md) — DI-интеграция
- [Client](client/client.md) — клиентская библиотека
