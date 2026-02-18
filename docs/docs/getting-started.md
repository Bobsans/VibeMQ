# Getting Started

Это руководство поможет вам быстро начать работу с VibeMQ.

## Предварительные требования

- .NET 8.0 или выше
- Базовое понимание паттерна Pub/Sub

## Установка

### Сервер

```bash
dotnet add package VibeMQ.Server
dotnet add package VibeMQ.Server.DependencyInjection  # опционально, для DI
```

### Клиент

```bash
dotnet add package VibeMQ.Client
dotnet add package VibeMQ.Client.DependencyInjection  # опционально, для DI
```

## Быстрый старт

### 1. Запуск сервера

#### Минимальная конфигурация

```csharp
using VibeMQ.Server;

var broker = BrokerBuilder.Create()
    .UsePort(8080)
    .Build();

await broker.RunAsync();
```

#### Расширенная конфигурация

```csharp
using VibeMQ.Core.Enums;
using VibeMQ.Server;

var broker = BrokerBuilder.Create()
    .UsePort(8080)
    .UseAuthentication("my-secret-token")
    .UseMaxConnections(500)
    .ConfigureQueues(options => {
        options.DefaultDeliveryMode = DeliveryMode.RoundRobin;
        options.MaxQueueSize = 10_000;
        options.EnableAutoCreate = true;
    })
    .ConfigureRateLimiting(options => {
        options.Enabled = true;
        options.MaxMessagesPerClientPerSecond = 1000;
    })
    .Build();

await broker.RunAsync();
```

### 2. Подключение клиента

```csharp
using VibeMQ.Client;

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

### 3. Публикация сообщений

```csharp
await client.PublishAsync("notifications", new {
    Title = "Welcome",
    Body = "Hello from VibeMQ!",
    Priority = "normal"
});
```

### 4. Подписка на очередь

```csharp
await using var subscription = await client.SubscribeAsync<Notification>("notifications", async msg => {
    Console.WriteLine($"Received: {msg.Title} - {msg.Body}");
    await ProcessNotificationAsync(msg);
});
```

## Примеры использования

### Пример 1: Простой чат

```csharp
// Сервер
var broker = BrokerBuilder.Create()
    .UsePort(8080)
    .Build();

_ = Task.Run(() => broker.RunAsync());

// Клиент 1 - отправитель
await using var sender = await VibeMQClient.ConnectAsync("localhost", 8080);
await sender.PublishAsync("chat", new { User = "Alice", Message = "Hello!" });

// Клиент 2 - получатель
await using var receiver = await VibeMQClient.ConnectAsync("localhost", 8080);
await using var sub = await receiver.SubscribeAsync<ChatMessage>("chat", msg => {
    Console.WriteLine($"{msg.User}: {msg.Message}");
    return Task.CompletedTask;
});
```

### Пример 2: Worker Queue

```csharp
// Публикация задач
await client.PublishAsync("tasks", new TaskData {
    Id = Guid.NewGuid(),
    Type = "ProcessImage",
    Payload = new { ImageUrl = "https://..." }
});

// Обработка задач воркерами
await using var subscription = await client.SubscribeAsync<TaskData>("tasks", async task => {
    await ProcessTaskAsync(task);
});
```

## Следующие шаги

- Изучите [архитектуру](architecture.md) VibeMQ
- Настройте [сервер](configuration.md) под ваши нужды
- Узнайте больше о [гарантиях доставки](server/delivery.md)
- Посмотрите [примеры](examples.md) использования
