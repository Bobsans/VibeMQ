# Broker Server

`BrokerServer` — это основной класс сервера VibeMQ, управляющий жизненным циклом брокера сообщений.

## Расположение

```
src/VibeMQ.Server/BrokerServer.cs
```

## Быстрый старт

### Минимальный запуск

```csharp
using VibeMQ.Server;

var broker = BrokerBuilder.Create()
    .UsePort(8080)
    .Build();

await broker.RunAsync();
```

### Расширенная конфигурация

```csharp
using VibeMQ.Core.Enums;
using VibeMQ.Server;

var broker = BrokerBuilder.Create()
    .UsePort(8080)
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
    .Build();

await broker.RunAsync();
```

## BrokerBuilder

`BrokerBuilder` — fluent API для конфигурации и создания экземпляра `BrokerServer`.

### Методы конфигурации

| Метод | Описание | Значение по умолчанию |
|-------|----------|----------------------|
| `UsePort(int)` | TCP порт для прослушивания | 8080 |
| `UseMaxConnections(int)` | Максимум клиентских подключений | 1000 |
| `UseMaxMessageSize(int)` | Максимальный размер сообщения в байтах | 1 MB |
| `UseAuthentication(string)` | Включить аутентификацию с токеном | Отключено |
| `ConfigureQueues(Action<QueueOptions>)` | Настройка очередей | Auto-create включён |
| `ConfigureRateLimiting(Action<RateLimitOptions>)` | Настройка rate limiting | Отключено |
| `UseLoggerFactory(ILoggerFactory)` | Фабрика логгирования | Console logger |
| `Build()` | Создать экземпляр `BrokerServer` | — |

### Пример использования BrokerBuilder

```csharp
var broker = BrokerBuilder.Create()
    .UsePort(8080)
    .UseMaxConnections(100)
    .ConfigureQueues(opts => {
        opts.DefaultDeliveryMode = DeliveryMode.FanOutWithAck;
        opts.MaxQueueSize = 5000;
        opts.EnableDeadLetterQueue = true;
    })
    .Build();
```

## Жизненный цикл сервера

### Запуск

```csharp
// Блокирующий запуск
await broker.RunAsync(cancellationToken);

// Или в фоне
_ = Task.Run(() => broker.RunAsync(cts.Token));
```

### Остановка

```csharp
using var cts = new CancellationTokenSource();

// Обработка Ctrl+C
Console.CancelKeyPress += (_, e) => {
    e.Cancel = true;
    cts.Cancel();
};

try {
    await broker.RunAsync(cts.Token);
} catch (OperationCanceledException) {
    // Ожидается при shutdown
}
```

### Graceful Shutdown

При получении сигнала остановки:

1. Останавливается приём новых соединений
2. Уведомляются клиенты о shutdown
3. Ожидают обработки in-flight сообщений (30 сек)
4. Закрываются все соединения
5. Очищаются ресурсы

## Метрики

`BrokerServer` предоставляет доступ к метрикам через свойство `Metrics`:

```csharp
var metrics = broker.Metrics;

Console.WriteLine($"Published: {metrics.TotalMessagesPublished}");
Console.WriteLine($"Delivered: {metrics.TotalMessagesDelivered}");
Console.WriteLine($"Acknowledged: {metrics.TotalAcknowledged}");
Console.WriteLine($"Active connections: {metrics.ActiveConnections}");
```

### Доступные метрики

**Счётчики:**
- `TotalMessagesPublished` — всего опубликовано
- `TotalMessagesDelivered` — всего доставлено
- `TotalAcknowledged` — всего подтверждено
- `TotalErrors` — всего ошибок

**Gauge:**
- `ActiveConnections` — активные подключения
- `ActiveQueues` — активные очереди
- `MemoryUsageBytes` — потребление памяти

**Гистограммы:**
- `DeliveryLatency` — латентность доставки
- `ProcessingTime` — время обработки

## Свойства сервера

```csharp
// Количество активных подключений
int activeConnections = broker.ActiveConnections;

// Количество неподтверждённых сообщений
int inFlightMessages = broker.InFlightMessages;

// Доступ к метрикам
IBrokerMetrics metrics = broker.Metrics;
```

## Интеграция с DI

Для использования с Microsoft Dependency Injection:

```bash
dotnet add package VibeMQ.Server.DependencyInjection
```

```csharp
using VibeMQ.Server.DependencyInjection;

services.AddVibeMQBroker(options => {
    options.Port = 8080;
    options.MaxConnections = 500;
    options.EnableAuthentication = true;
    options.AuthToken = "my-secret-token";
});

// Broker запускается как IHostedService
await host.RunAsync();
```

См. [Dependency Injection](dependency-injection.md) для подробностей.

## Конфигурация через appsettings.json

```json
{
  "Broker": {
    "Port": 8080,
    "MaxConnections": 1000,
    "MaxMessageSize": 1048576,
    "EnableAuthentication": true,
    "AuthToken": "secret-token",
    "QueueDefaults": {
      "MaxQueueSize": 10000,
      "DefaultDeliveryMode": "RoundRobin",
      "EnableAutoCreate": true
    }
  }
}
```

## Обработка ошибок

```csharp
try {
    await broker.RunAsync(cancellationToken);
} catch (SocketException ex) {
    // Порт занят или другая ошибка сокета
    Console.WriteLine($"Failed to start: {ex.Message}");
} catch (OperationCanceledException) {
    // Graceful shutdown
    Console.WriteLine("Server stopped");
}
```

## Пример полного сервера

```csharp
using Microsoft.Extensions.Logging;
using VibeMQ.Core.Enums;
using VibeMQ.Server;

using var loggerFactory = LoggerFactory.Create(builder => {
    builder.AddConsole().SetMinimumLevel(LogLevel.Information);
});

var broker = BrokerBuilder.Create()
    .UsePort(8080)
    .UseAuthentication("my-secret-token")
    .UseMaxConnections(500)
    .ConfigureQueues(options => {
        options.DefaultDeliveryMode = DeliveryMode.RoundRobin;
        options.MaxQueueSize = 10_000;
        options.EnableAutoCreate = true;
        options.EnableDeadLetterQueue = true;
    })
    .ConfigureRateLimiting(options => {
        options.Enabled = true;
        options.MaxMessagesPerClientPerSecond = 1000;
    })
    .UseLoggerFactory(loggerFactory)
    .Build();

Console.WriteLine("╔══════════════════════════════════════════╗");
Console.WriteLine("║          VibeMQ Message Broker            ║");
Console.WriteLine("╚══════════════════════════════════════════╝");
Console.WriteLine($"  Port: {8080}");
Console.WriteLine($"  Max connections: {500}");
Console.WriteLine("Press Ctrl+C to stop...");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => {
    e.Cancel = true;
    cts.Cancel();
};

try {
    await broker.RunAsync(cts.Token);
} catch (OperationCanceledException) {
    Console.WriteLine("Server stopped.");
}
```

## См. также

- [Configuration](configuration.md) — подробная настройка
- [Queues](queues.md) — управление очередями
- [Delivery](delivery.md) — гарантии доставки
- [Health Checks](health-checks.md) — мониторинг состояния
