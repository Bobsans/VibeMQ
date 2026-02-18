# Delivery Guarantees

VibeMQ обеспечивает гарантии доставки сообщений через систему подтверждений (ACK) и повторных попыток.

## Расположение

```
src/VibeMQ.Server/Delivery/AckTracker.cs
src/VibeMQ.Server/Delivery/DeadLetterQueue.cs
src/VibeMQ.Server/Delivery/PendingDelivery.cs
```

## Режимы доставки

### Без подтверждения (At-Most-Once)

Сообщение доставляется без ожидания ACK. Возможна потеря при сбое.

```csharp
options.DefaultDeliveryMode = DeliveryMode.FanOutWithoutAck;
```

**Характеристики:**
- ✅ Максимальная производительность
- ✅ Минимальная латентность
- ❌ Возможна потеря сообщений

**Использование:**
- Телеметрия и метрики
- Логирование
- События, где потеря допустима

### С подтверждением (At-Least-Once)

Сообщение доставляется с ожиданием ACK. Гарантирована минимум одна доставка, возможны дубликаты.

```csharp
options.DefaultDeliveryMode = DeliveryMode.FanOutWithAck;
```

**Характеристики:**
- ✅ Гарантия доставки
- ⚠️ Возможны дубликаты
- ⚠️ Ниже производительность

**Использование:**
- Финансовые транзакции
- Уведомления
- Критичные данные

## AckTracker

`AckTracker` отслеживает неподтверждённые сообщения и управляет повторными попытками.

### Жизненный цикл сообщения с ACK

```
1. Публикация → Queue
2. Доставка → Клиент
3. Ожидание ACK (таймаут: 30 сек)
4. Получение ACK → Удаление из трекинга
   │
   └─> Таймаут → Повторная отправка (max 3)
                 │
                 └─> Исчерпание попыток → Dead Letter Queue
```

### Конфигурация AckTracker

```csharp
var options = new QueueOptions {
    MaxRetryAttempts = 3,           // Попытки доставки
    MessageTtl = TimeSpan.FromHours(1) // Время жизни сообщения
};
```

### Обработка ACK на клиенте

```csharp
await using var subscription = await client.SubscribeAsync<Order>("orders", async order => {
    try {
        await ProcessOrderAsync(order);
        // ACK отправляется автоматически после успешной обработки
    } catch (Exception ex) {
        // При исключении ACK не отправляется
        // Сообщение будет доставлено повторно
        logger.LogError(ex, "Failed to process order {OrderId}", order.Id);
        throw; // Пробрасываем исключение
    }
});
```

## Повторные попытки (Retry)

### Экспоненциальный backoff

При неудачной доставке VibeMQ использует экспоненциальную задержку:

```
Попытка 1: немедленно
Попытка 2: через 1 сек
Попытка 3: через 2 сек
Попытка 4: через 4 сек
...
```

### Конфигурация retry

```csharp
var options = new QueueOptions {
    MaxRetryAttempts = 5,           // Максимум попыток
    // Внутренняя логика использует экспоненциальный backoff
};
```

### Обработка retry в коде

```csharp
// В QueueManager
private async Task OnRetryRequiredAsync(string messageId, string queueName) {
    if (!_queues.TryGetValue(queueName, out var queue)) {
        return;
    }

    var message = await _messageStore.GetAsync(messageId);
    if (message == null) {
        return;
    }

    // Увеличиваем счётчик попыток
    message.RetryCount++;

    if (message.RetryCount >= _defaults.MaxRetryAttempts) {
        // Исчерпаны попытки → DLQ
        await _deadLetterQueue.HandleFailedMessageAsync(
            message, 
            FailureReason.MaxRetriesExceeded
        );
        return;
    }

    // Повторная доставка
    await queue.DeliverAsync(message);
}
```

## Dead Letter Queue

DLQ хранит сообщения, которые не удалось доставить после всех попыток.

### Включение DLQ

```csharp
var options = new QueueOptions {
    EnableDeadLetterQueue = true,
    DeadLetterQueueName = "orders-dlq",
    MaxRetryAttempts = 3
};

await queueManager.CreateQueueAsync("orders", options);
```

### Причины попадания в DLQ

```csharp
public enum FailureReason {
    MaxRetriesExceeded,      // Исчерпаны попытки
    MessageExpired,          // Истёк TTL
    DeserializationError,    // Ошибка десериализации
    HandlerException         // Исключение в обработчике
}
```

### Получение сообщений из DLQ

```csharp
var dlq = sp.GetRequiredService<DeadLetterQueue>();

// Получить последние 100 сообщений
var messages = await dlq.GetMessagesAsync("orders-dlq", 100);

foreach (var msg in messages) {
    Console.WriteLine($"ID: {msg.Id}");
    Console.WriteLine($"Reason: {msg.FailureReason}");
    Console.WriteLine($"Retries: {msg.RetryCount}");
    Console.WriteLine($"Payload: {msg.Payload}");
}
```

### Повторная обработка из DLQ

```csharp
// Повторить одно сообщение
await dlq.RetryMessageAsync(messageId);

// Повторить все сообщения
var messages = await dlq.GetMessagesAsync("orders-dlq", 1000);
foreach (var msg in messages) {
    await dlq.RetryMessageAsync(msg.Id);
}
```

### Очистка DLQ

```csharp
// Удалить старое сообщение
await dlq.DeleteMessageAsync(messageId);

// Очистить всю DLQ
await dlq.ClearAsync("orders-dlq");
```

## Time-To-Live (TTL)

TTL ограничивает время жизни сообщения в очереди.

### Настройка TTL

```csharp
var options = new QueueOptions {
    MessageTtl = TimeSpan.FromMinutes(30) // Сообщение действительно 30 минут
};

await queueManager.CreateQueueAsync("notifications", options);
```

### Поведение при истечении TTL

1. Сообщение удаляется из очереди
2. При наличии DLQ — перемещается туда с `FailureReason.MessageExpired`
3. ACK не требуется

### Пример: уведомления с TTL

```csharp
// Уведомления актуальны 1 час
var options = new QueueOptions {
    MessageTtl = TimeSpan.FromHours(1),
    EnableDeadLetterQueue = true
};

await queueManager.CreateQueueAsync("alerts", options);

// Если подписчик не получит сообщение за 1 час, оно истечёт
await client.PublishAsync("alerts", new {
    Title = "System Update",
    Body = "Maintenance in 5 minutes"
});
```

## Идемпотентность

При использовании режима с ACK возможна повторная доставка. Обработчики должны быть идемпотентными.

### Паттерн идемпотентности

```csharp
private readonly HashSet<string> _processedIds = new();

await using var subscription = await client.SubscribeAsync<Order>("orders", async order => {
    // Проверка на дубликат
    if (_processedIds.Contains(order.Id)) {
        logger.LogInformation("Duplicate order {OrderId}, skipping", order.Id);
        return;
    }

    await ProcessOrderAsync(order);
    _processedIds.Add(order.Id);
});
```

### Использование correlation ID

```csharp
// Публикация с correlation ID
var correlationId = Guid.NewGuid().ToString();

await client.PublishAsync("orders", new Order {
    Id = "123",
    // ...
}, headers: new Dictionary<string, string> {
    ["correlationId"] = correlationId
});

// Обработка с проверкой дубликатов
await using var subscription = await client.SubscribeAsync<Order>("orders", async order => {
    var correlationId = order.Headers.GetValueOrDefault("correlationId");
    
    if (await IsDuplicateAsync(correlationId)) {
        return; // Дубликат
    }

    await ProcessOrderAsync(order);
    await MarkAsProcessedAsync(correlationId);
});
```

## Мониторинг доставки

### Метрики

```csharp
var metrics = broker.Metrics;

Console.WriteLine($"Published: {metrics.TotalMessagesPublished}");
Console.WriteLine($"Delivered: {metrics.TotalMessagesDelivered}");
Console.WriteLine($"Acknowledged: {metrics.TotalAcknowledged}");
Console.WriteLine($"Errors: {metrics.TotalErrors}");
```

### Латентность

```csharp
// Гистограмма латентности доставки
var latency = metrics.DeliveryLatency;
var p50 = latency.GetPercentile(50);
var p95 = latency.GetPercentile(95);
var p99 = latency.GetPercentile(99);

Console.WriteLine($"Latency P50: {p50}ms, P95: {p95}ms, P99: {p99}ms");
```

### Логирование

```csharp
// Включите детальное логирование
var broker = BrokerBuilder.Create()
    .UseLoggerFactory(loggerFactory)
    .Build();

// В логах:
// - Доставка сообщений
// - Получение ACK
// - Повторные попытки
// - Перемещение в DLQ
```

## Best Practices

### 1. Выбор режима

| Сценарий | Режим |
|----------|-------|
| Телеметрия | FanOutWithoutAck |
| Уведомления | FanOutWithAck |
| Задачи воркерам | RoundRobin + ACK |
| Финансовые данные | FanOutWithAck + идемпотентность |

### 2. Настройка retry

```csharp
var options = new QueueOptions {
    MaxRetryAttempts = 3,  // Не слишком много
    MessageTtl = TimeSpan.FromHours(1) // Ограничьте время жизни
};
```

### 3. Мониторинг DLQ

```csharp
// Регулярно проверяйте DLQ
var dlq = sp.GetRequiredService<DeadLetterQueue>();
var info = await dlq.GetQueueInfoAsync("orders-dlq");

if (info.MessageCount > 100) {
    logger.LogWarning("DLQ is growing: {Count}", info.MessageCount);
}
```

### 4. Идемпотентность

Всегда предполагайте возможность дубликатов:

```csharp
// Храните ID обработанных сообщений
// Используйте уникальные ключи
// Проверяйте перед обработкой
```

## См. также

- [Queues](queues.md) — управление очередями
- [Broker Server](broker-server.md) — настройка сервера
- [Client](client.md) — клиентская библиотека
