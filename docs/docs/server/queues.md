# Queues

Очереди — это основной механизм доставки сообщений в VibeMQ.

## Расположение

```
src/VibeMQ.Server/Queues/QueueManager.cs
src/VibeMQ.Server/Queues/MessageQueue.cs
```

## Обзор

`QueueManager` управляет всеми очередями:
- Создание и удаление
- Публикация сообщений
- Доставка подписчикам
- Интеграция с AckTracker и DeadLetterQueue

## Режимы доставки

VibeMQ поддерживает несколько режимов доставки сообщений:

### RoundRobin

Сообщение доставляется **одному** подписчику из очереди (load balancing).

```csharp
options.DefaultDeliveryMode = DeliveryMode.RoundRobin;
```

**Использование:**
- Обработка задач воркерами
- Балансировка нагрузки между потребителями

```
Publisher → Queue → Subscriber 1
                  → Subscriber 2 (следующее)
                  → Subscriber 3 (следующее)
```

### FanOutWithAck

Сообщение доставляется **всем** подписчикам с подтверждением (ACK).

```csharp
options.DefaultDeliveryMode = DeliveryMode.FanOutWithAck;
```

**Использование:**
- Уведомления нескольким системам
- Репликация данных
- Аудит и логирование

```
Publisher → Queue → Subscriber 1 ──ACK─┐
                  → Subscriber 2 ──ACK─┤
                  → Subscriber 3 ──ACK─┘
```

### FanOutWithoutAck

Сообщение доставляется **всем** подписчикам без подтверждения.

```csharp
options.DefaultDeliveryMode = DeliveryMode.FanOutWithoutAck;
```

**Использование:**
- Broadcas-уведомления
- События, где потеря допустима

### PriorityBased

Доставка по приоритету сообщений.

```csharp
options.DefaultDeliveryMode = DeliveryMode.PriorityBased;
```

**Использование:**
- Критичные сообщения обрабатываются первыми
- SLA-требования

## Создание очереди

### Автоматическое создание

По умолчанию очереди создаются автоматически при первой публикации:

```csharp
var broker = BrokerBuilder.Create()
    .ConfigureQueues(opts => {
        opts.EnableAutoCreate = true; // по умолчанию
    })
    .Build();

// Очередь "notifications" будет создана автоматически
await client.PublishAsync("notifications", data);
```

### Явное создание

```csharp
var queueManager = sp.GetRequiredService<IQueueManager>();

await queueManager.CreateQueueAsync("orders", new QueueOptions {
    Mode = DeliveryMode.RoundRobin,
    MaxQueueSize = 5000,
    MessageTtl = TimeSpan.FromHours(1),
    EnableDeadLetterQueue = true,
    DeadLetterQueueName = "orders-dlq",
    OverflowStrategy = OverflowStrategy.DropOldest,
    MaxRetryAttempts = 3
});
```

## Конфигурация очереди

### QueueOptions

| Свойство | Тип | По умолчанию | Описание |
|----------|-----|--------------|----------|
| `Mode` | `DeliveryMode` | `RoundRobin` | Режим доставки |
| `MaxQueueSize` | `int` | 10000 | Максимум сообщений в очереди |
| `MessageTtl` | `TimeSpan?` | null | Время жизни сообщения |
| `EnableDeadLetterQueue` | `bool` | false | Включить DLQ |
| `DeadLetterQueueName` | `string` | `{queue}-dlq` | Имя DLQ |
| `OverflowStrategy` | `OverflowStrategy` | `DropOldest` | Стратегия переполнения |
| `MaxRetryAttempts` | `int` | 3 | Попытки доставки |

### OverflowStrategy

Стратегия поведения при переполнении очереди:

```csharp
public enum OverflowStrategy {
    DropOldest,     // Удалить самое старое сообщение
    DropNewest,     // Отклонить новое сообщение
    BlockPublisher, // Блокировать отправителя (backpressure)
    RedirectToDlq   // Перенаправить в Dead Letter Queue
}
```

## Публикация сообщений

### Базовая публикация

```csharp
await client.PublishAsync("notifications", new {
    Title = "Hello",
    Body = "World"
});
```

### С приоритетом

```csharp
await client.PublishAsync("alerts", new {
    Title = "Critical Error",
    Body = "System overload detected"
}, headers: new Dictionary<string, string> {
    ["priority"] = "high"
});
```

### С correlation ID

```csharp
var correlationId = Guid.NewGuid().ToString();

await client.PublishAsync("requests", new {
    Data = "..."
}, headers: new Dictionary<string, string> {
    ["correlationId"] = correlationId
});
```

## Подписка на очередь

```csharp
await using var subscription = await client.SubscribeAsync<Notification>("notifications", async msg => {
    Console.WriteLine($"Received: {msg.Title}");
    await ProcessNotificationAsync(msg);
});
```

### Отписка

```csharp
// Автоматическая при disposal (using)
await subscription.DisposeAsync();

// Или явная
await subscription.DisposeAsync();
```

### Множественные подписчики

```csharp
// Подписчик 1
await using var sub1 = await client1.SubscribeAsync<Order>("orders", HandleOrderAsync);

// Подписчик 2 (RoundRobin - будут получать сообщения по очереди)
await using var sub2 = await client2.SubscribeAsync<Order>("orders", HandleOrderAsync);

// Подписчик 3 (FanOut - все получат все сообщения)
var broker = BrokerBuilder.Create()
    .ConfigureQueues(opts => {
        opts.DefaultDeliveryMode = DeliveryMode.FanOutWithAck;
    })
    .Build();
```

## Управление очередями

### Список очередей

```csharp
var queues = await queueManager.ListQueuesAsync();
foreach (var queue in queues) {
    Console.WriteLine(queue);
}
```

### Информация об очереди

```csharp
var info = await queueManager.GetQueueInfoAsync("notifications");
if (info != null) {
    Console.WriteLine($"Name: {info.Name}");
    Console.WriteLine($"Messages: {info.MessageCount}");
    Console.WriteLine($"Subscribers: {info.SubscriberCount}");
    Console.WriteLine($"Mode: {info.Mode}");
}
```

### Удаление очереди

```csharp
await queueManager.DeleteQueueAsync("old-queue");
```

## Dead Letter Queue

DLQ хранит сообщения, которые не удалось доставить:

```csharp
var options = new QueueOptions {
    EnableDeadLetterQueue = true,
    DeadLetterQueueName = "notifications-dlq",
    MaxRetryAttempts = 3
};

await queueManager.CreateQueueAsync("notifications", options);
```

### Причины попадания в DLQ

```csharp
public enum FailureReason {
    MaxRetriesExceeded,      // Исчерпаны попытки доставки
    MessageExpired,          // Истёк TTL сообщения
    DeserializationError,    // Ошибка десериализации
    HandlerException         // Исключение в обработчике
}
```

### Обработка DLQ

```csharp
var dlq = sp.GetRequiredService<DeadLetterQueue>();

// Получить сообщения из DLQ
var failedMessages = await dlq.GetMessagesAsync("notifications-dlq", 100);

foreach (var msg in failedMessages) {
    Console.WriteLine($"Message {msg.Id}: {msg.FailureReason}");
    
    // Повторная попытка
    await dlq.RetryMessageAsync(msg.Id);
}
```

## Best Practices

### 1. Выбор режима доставки

- **RoundRobin** для обработки задач воркерами
- **FanOutWithAck** для критичных уведомлений
- **FanOutWithoutAck** для событий, где потеря допустима

### 2. Размер очереди

```csharp
// Установите разумный лимит
options.MaxQueueSize = 10_000; // 10K сообщений

// Выберите стратегию переполнения
options.OverflowStrategy = OverflowStrategy.DropOldest;
```

### 3. TTL сообщений

```csharp
// Сообщения актуальны 1 час
options.MessageTtl = TimeSpan.FromHours(1);
```

### 4. Dead Letter Queue

```csharp
// Всегда включайте DLQ для важных очередей
options.EnableDeadLetterQueue = true;
options.MaxRetryAttempts = 3;
```

### 5. Мониторинг

```csharp
// Регулярно проверяйте размер очередей
var info = await queueManager.GetQueueInfoAsync("orders");
if (info.MessageCount > 1000) {
    logger.LogWarning("Queue {Queue} is growing: {Count}", info.Name, info.MessageCount);
}
```

## См. также

- [Broker Server](broker-server.md) — основной сервер
- [Delivery](delivery.md) — гарантии доставки
- [Configuration](configuration.md) — настройка
