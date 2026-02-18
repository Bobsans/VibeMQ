# VibeMQ Client

Клиентская библиотека для подключения к брокеру VibeMQ.

## Расположение

```
src/VibeMQ.Client/VibeMQClient.cs
```

## Установка

```bash
dotnet add package VibeMQ.Client
```

## Быстрый старт

### Подключение

```csharp
using VibeMQ.Client;

await using var client = await VibeMQClient.ConnectAsync("localhost", 8080);
```

### Публикация сообщения

```csharp
await client.PublishAsync("notifications", new {
    Title = "Hello",
    Body = "World"
});
```

### Подписка на очередь

```csharp
await using var subscription = await client.SubscribeAsync<Notification>("notifications", async msg => {
    Console.WriteLine($"Received: {msg.Title}");
    await ProcessNotificationAsync(msg);
});
```

## Подключение к брокеру

### Базовое подключение

```csharp
await using var client = await VibeMQClient.ConnectAsync("localhost", 8080);
```

### Подключение с опциями

```csharp
await using var client = await VibeMQClient.ConnectAsync(
    "localhost",
    8080,
    new ClientOptions {
        AuthToken = "my-secret-token",
        ReconnectPolicy = new ReconnectPolicy {
            MaxAttempts = 5,
            UseExponentialBackoff = true,
        },
        KeepAliveInterval = TimeSpan.FromSeconds(30),
    }
);
```

### Подключение с логгером

```csharp
using Microsoft.Extensions.Logging;

var logger = loggerFactory.CreateLogger<VibeMQClient>();

await using var client = await VibeMQClient.ConnectAsync(
    "localhost",
    8080,
    options: null,
    logger: logger
);
```

### Проверка подключения

```csharp
if (client.IsConnected) {
    Console.WriteLine("Connected to broker");
} else {
    Console.WriteLine("Disconnected");
}
```

## Публикация сообщений

### Простая публикация

```csharp
await client.PublishAsync("queue-name", new {
    Field1 = "value1",
    Field2 = 42
});
```

### Публикация с cancellation token

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

await client.PublishAsync(
    "queue-name",
    new { Data = "value" },
    cancellationToken: cts.Token
);
```

### Публикация сложных объектов

```csharp
public class Order {
    public string Id { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<OrderItem> Items { get; set; }
}

await client.PublishAsync("orders", new Order {
    Id = Guid.NewGuid().ToString(),
    Amount = 99.99m,
    CreatedAt = DateTime.UtcNow,
    Items = new List<OrderItem> { ... }
});
```

## Подписка на очереди

### Базовая подписка

```csharp
await using var subscription = await client.SubscribeAsync<MyMessage>("queue-name", async msg => {
    Console.WriteLine($"Received: {msg.Data}");
    await ProcessMessageAsync(msg);
});
```

### Подписка с несколькими очередями

```csharp
await using var sub1 = await client.SubscribeAsync<Order>("orders", HandleOrderAsync);
await using var sub2 = await client.SubscribeAsync<Notification>("notifications", HandleNotificationAsync);
await using var sub3 = await client.SubscribeAsync<Event>("events", HandleEventAsync);
```

### Отписка

```csharp
// Автоматическая при выходе из using
await using var subscription = await client.SubscribeAsync<...>(...);

// Явная отписка
await subscription.DisposeAsync();

// Или через CancellationToken
using var cts = new CancellationTokenSource();
await using var subscription = await client.SubscribeAsync<...>("queue", handler, cts.Token);

cts.Cancel(); // Отписка
```

### Обработка ошибок в подписчике

```csharp
await using var subscription = await client.SubscribeAsync<Order>("orders", async order => {
    try {
        await ProcessOrderAsync(order);
    } catch (Exception ex) {
        logger.LogError(ex, "Failed to process order {OrderId}", order.Id);
        throw; // Проброс исключения для retry
    }
});
```

## Reconnect Policy

Автоматическое переподключение при разрыве соединения.

### Настройка reconnect policy

```csharp
var options = new ClientOptions {
    ReconnectPolicy = new ReconnectPolicy {
        MaxAttempts = 10,                  // Максимум попыток
        InitialDelay = TimeSpan.FromSeconds(1),  // Начальная задержка
        MaxDelay = TimeSpan.FromMinutes(5),      // Максимальная задержка
        UseExponentialBackoff = true,      // Экспоненциальное увеличение
    }
};

await using var client = await VibeMQClient.ConnectAsync("localhost", 8080, options);
```

### Экспоненциальный backoff

При `UseExponentialBackoff = true`:

```
Попытка 1: через 1 сек
Попытка 2: через 2 сек
Попытка 3: через 4 сек
Попытка 4: через 8 сек
...
Попытка N: через min(2^N сек, MaxDelay)
```

### Без reconnect

```csharp
var options = new ClientOptions {
    ReconnectPolicy = null // Отключить авто-реконнект
};
```

## Keep-Alive

Поддержание соединения через периодические PING/PONG.

### Настройка keep-alive

```csharp
var options = new ClientOptions {
    KeepAliveInterval = TimeSpan.FromSeconds(30) // Отправлять PING каждые 30 сек
};

await using var client = await VibeMQClient.ConnectAsync("localhost", 8080, options);
```

### Рекомендации

| Сценарий | Keep-Alive Interval |
|----------|---------------------|
| LAN | 60 сек |
| WAN | 30 сек |
| Нестабильная сеть | 15 сек |

## ClientOptions

Полная конфигурация клиента:

```csharp
public sealed class ClientOptions {
    /// <summary>
    /// Токен аутентификации.
    /// </summary>
    public string? AuthToken { get; set; }

    /// <summary>
    /// Политика переподключения.
    /// </summary>
    public ReconnectPolicy? ReconnectPolicy { get; set; }

    /// <summary>
    /// Интервал отправки PING.
    /// </summary>
    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Таймаут подключения.
    /// </summary>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Таймаут операции.
    /// </summary>
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
```

### Пример полной конфигурации

```csharp
var options = new ClientOptions {
    AuthToken = "my-secret-token",
    ReconnectPolicy = new ReconnectPolicy {
        MaxAttempts = int.MaxValue,
        InitialDelay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromMinutes(5),
        UseExponentialBackoff = true,
    },
    KeepAliveInterval = TimeSpan.FromSeconds(30),
    ConnectionTimeout = TimeSpan.FromSeconds(5),
    OperationTimeout = TimeSpan.FromSeconds(30),
};

await using var client = await VibeMQClient.ConnectAsync("broker.example.com", 8080, options);
```

## Обработка ошибок

### Исключения клиента

```csharp
try {
    await client.PublishAsync("queue", data);
} catch (InvalidOperationException ex) {
    // Ошибка публикации (например, очередь не существует)
    logger.LogError(ex, "Publish failed");
} catch (TimeoutException ex) {
    // Таймаут операции
    logger.LogError(ex, "Operation timed out");
} catch (SocketException ex) {
    // Ошибка соединения
    logger.LogError(ex, "Connection error");
}
```

### Retry logic

```csharp
public async Task PublishWithRetryAsync<T>(
    VibeMQClient client,
    string queue,
    T payload,
    int maxRetries = 3)
{
    for (int i = 0; i < maxRetries; i++) {
        try {
            await client.PublishAsync(queue, payload);
            return;
        } catch (SocketException) when (i < maxRetries - 1) {
            await Task.Delay(TimeSpan.FromSeconds(i));
        }
    }
}
```

## Паттерны использования

### Request/Response

```csharp
// Публикация запроса
var correlationId = Guid.NewGuid().ToString();

await client.PublishAsync("requests", new {
    Id = correlationId,
    Data = "Process this"
}, headers: new Dictionary<string, string> {
    ["correlationId"] = correlationId,
    ["replyTo"] = "responses"
});

// Подписка на ответы
await using var subscription = await client.SubscribeAsync<Response>("responses", msg => {
    if (msg.CorrelationId == correlationId) {
        Console.WriteLine($"Response received: {msg.Data}");
    }
    return Task.CompletedTask;
});
```

### Worker Queue

```csharp
// Publisher
await client.PublishAsync("tasks", new TaskData {
    Id = Guid.NewGuid(),
    Type = "ProcessImage",
    Url = "https://..."
});

// Worker
await using var subscription = await client.SubscribeAsync<TaskData>("tasks", async task => {
    await ProcessTaskAsync(task);
    // ACK отправляется автоматически после обработки
});
```

### Pub/Sub уведомления

```csharp
// Publisher
await client.PublishAsync("events.user.created", new {
    UserId = "123",
    Email = "user@example.com"
});

// Subscriber 1 (Email service)
await using var sub1 = await client1.SubscribeAsync<UserEvent>("events.user.created", SendEmailAsync);

// Subscriber 2 (Analytics service)
await using var sub2 = await client2.SubscribeAsync<UserEvent>("events.user.created", TrackEventAsync);

// Subscriber 3 (Notification service)
await using var sub3 = await client3.SubscribeAsync<UserEvent>("events.user.created", SendPushAsync);
```

## DI-интеграция

Для использования с Microsoft Dependency Injection:

```bash
dotnet add package VibeMQ.Client.DependencyInjection
```

```csharp
using VibeMQ.Client.DependencyInjection;

services.AddVibeMQClient(options => {
    options.Host = "localhost";
    options.Port = 8080;
    options.AuthToken = "my-secret-token";
    options.KeepAliveInterval = TimeSpan.FromSeconds(30);
});

// Inject IVibeMQClientFactory
public class MyService {
    private readonly IVibeMQClientFactory _clientFactory;

    public MyService(IVibeMQClientFactory clientFactory) {
        _clientFactory = clientFactory;
    }

    public async Task DoWorkAsync() {
        await using var client = await _clientFactory.CreateAsync();
        await client.PublishAsync("queue", data);
    }
}
```

См. [Dependency Injection](dependency-injection.md) для подробностей.

## Best Practices

### 1. Управление временем жизни

Используйте `await using` для автоматического закрытия:

```csharp
await using var client = await VibeMQClient.ConnectAsync(...);
// Клиент закроется автоматически
```

### 2. Подписки

Храните подписки долгоживущими:

```csharp
// ✅ Правильно
await using var subscription = await client.SubscribeAsync(...);
await Task.Delay(TimeSpan.FromHours(1));

// ❌ Неправильно
while (true) {
    await using var sub = await client.SubscribeAsync(...);
    await Task.Delay(1000);
    // Подписка создаётся/уничтожается постоянно
}
```

### 3. Обработка ошибок

Всегда обрабатывайте исключения в подписчиках:

```csharp
await using var subscription = await client.SubscribeAsync<Order>("orders", async order => {
    try {
        await ProcessOrderAsync(order);
    } catch (Exception ex) {
        logger.LogError(ex, "Failed to process order");
        throw; // Для retry
    }
});
```

### 4. Reconnect

Включайте авто-реконнект для production:

```csharp
var options = new ClientOptions {
    ReconnectPolicy = new ReconnectPolicy {
        MaxAttempts = int.MaxValue,
        UseExponentialBackoff = true,
    }
};
```

### 5. Keep-Alive

Настраивайте интервал под вашу сеть:

```csharp
// Для стабильной LAN
KeepAliveInterval = TimeSpan.FromMinutes(1);

// Для нестабильной сети
KeepAliveInterval = TimeSpan.FromSeconds(15);
```

## См. также

- [Reconnect](reconnect.md) — детальная настройка реконнектов
- [Protocol](protocol.md) — описание протокола
- [Dependency Injection](dependency-injection.md) — DI-интеграция
- [Examples](examples.md) — примеры использования
