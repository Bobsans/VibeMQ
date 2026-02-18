# Reconnect

Автоматическое переподключение клиента при разрыве соединения.

## Расположение

```
src/VibeMQ.Client/ReconnectPolicy.cs
src/VibeMQ.Client/VibeMQClient.cs
```

## Обзор

VibeMQ Client поддерживает автоматическое переподключение при потере соединения с брокером.

**Возможности:**
- Автоматическое обнаружение разрыва
- Экспоненциальный backoff
- Настраиваемое количество попыток
- Сохранение подписок после реконнекта

## Настройка ReconnectPolicy

### Базовая конфигурация

```csharp
var options = new ClientOptions {
    ReconnectPolicy = new ReconnectPolicy {
        MaxAttempts = 10,
        InitialDelay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromMinutes(5),
        UseExponentialBackoff = true,
    }
};

await using var client = await VibeMQClient.ConnectAsync("localhost", 8080, options);
```

### ReconnectPolicy свойства

```csharp
public sealed class ReconnectPolicy {
    /// <summary>
    /// Максимальное количество попыток подключения.
    /// По умолчанию: int.MaxValue (бесконечно)
    /// </summary>
    public int MaxAttempts { get; set; } = int.MaxValue;

    /// <summary>
    /// Начальная задержка между попытками.
    /// По умолчанию: 1 секунда
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Максимальная задержка между попытками.
    /// По умолчанию: 5 минут
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Использовать экспоненциальное увеличение задержки.
    /// По умолчанию: true
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;
}
```

## Режимы работы

### Экспоненциальный backoff

```csharp
var policy = new ReconnectPolicy {
    MaxAttempts = 10,
    InitialDelay = TimeSpan.FromSeconds(1),
    MaxDelay = TimeSpan.FromMinutes(5),
    UseExponentialBackoff = true,
};
```

**Задержки между попытками:**

| Попытка | Задержка |
|---------|----------|
| 1 | 1 сек |
| 2 | 2 сек |
| 3 | 4 сек |
| 4 | 8 сек |
| 5 | 16 сек |
| ... | ... |
| 10 | 5 мин ( capped by MaxDelay) |

### Фиксированная задержка

```csharp
var policy = new ReconnectPolicy {
    MaxAttempts = 10,
    InitialDelay = TimeSpan.FromSeconds(5),
    MaxDelay = TimeSpan.FromSeconds(5),
    UseExponentialBackoff = false, // Фиксированная задержка
};
```

**Задержки между попытками:**

| Попытка | Задержка |
|---------|----------|
| 1-10 | 5 сек |

### Без реконнекта

```csharp
var options = new ClientOptions {
    ReconnectPolicy = null // Отключить авто-реконнект
};
```

## Сценарии использования

### Production (рекомендуемая)

```csharp
var policy = new ReconnectPolicy {
    MaxAttempts = int.MaxValue,      // Бесконечные попытки
    InitialDelay = TimeSpan.FromSeconds(1),
    MaxDelay = TimeSpan.FromMinutes(5),
    UseExponentialBackoff = true,
};
```

**Преимущества:**
- Клиент всегда пытается переподключиться
- Экспоненциальная задержка снижает нагрузку
- Подходит для долгосрочных соединений

### Development

```csharp
var policy = new ReconnectPolicy {
    MaxAttempts = 5,                 // Ограниченное количество попыток
    InitialDelay = TimeSpan.FromSeconds(1),
    MaxDelay = TimeSpan.FromSeconds(10),
    UseExponentialBackoff = false,
};
```

**Преимущества:**
- Быстрое обнаружение проблем
- Не бесконечные попытки в dev-среде

### Временное подключение

```csharp
var policy = new ReconnectPolicy {
    MaxAttempts = 3,                 // Несколько попыток
    InitialDelay = TimeSpan.FromSeconds(2),
    MaxDelay = TimeSpan.FromSeconds(10),
    UseExponentialBackoff = true,
};
```

**Использование:**
- Краткосрочные операции
- Когда реконнект не критичен

## Обработка событий реконнекта

### Логирование попыток

```csharp
// В клиенте реализовано внутреннее логирование:
// - LogReconnectStarted(attempt)
// - LogReconnectSuccess(elapsed)
// - LogReconnectFailed(attempt, ex)
// - LogMaxAttemptsReached(maxAttempts)
```

### Мониторинг состояния

```csharp
// Проверка подключения
if (client.IsConnected) {
    Console.WriteLine("Connected");
} else {
    Console.WriteLine("Disconnected - attempting reconnect...");
}
```

## Восстановление подписок

При реконнекте клиент автоматически восстанавливает подписки:

```csharp
// Подписка будет восстановлена после реконнекта
await using var subscription = await client.SubscribeAsync<Order>("orders", HandleOrderAsync);

// ... разрыв соединения ...
// ... автоматический реконнект ...
// ... подписка восстановлена ...
```

### Внутренняя логика

1. Клиент сохраняет список подписок
2. При реконнекте переподписывается на очереди
3. Продолжает обработку сообщений

## Таймауты

### Connection Timeout

```csharp
var options = new ClientOptions {
    ConnectionTimeout = TimeSpan.FromSeconds(5), // Таймаут подключения
    ReconnectPolicy = new ReconnectPolicy { ... }
};
```

### Operation Timeout

```csharp
var options = new ClientOptions {
    OperationTimeout = TimeSpan.FromSeconds(30), // Таймаут операций
    ReconnectPolicy = new ReconnectPolicy { ... }
};
```

## Best Practices

### 1. Production настройки

```csharp
var policy = new ReconnectPolicy {
    MaxAttempts = int.MaxValue,
    InitialDelay = TimeSpan.FromSeconds(1),
    MaxDelay = TimeSpan.FromMinutes(5),
    UseExponentialBackoff = true,
};
```

### 2. Логирование

Используйте logger для отслеживания реконнектов:

```csharp
var logger = loggerFactory.CreateLogger<VibeMQClient>();

await using var client = await VibeMQClient.ConnectAsync(
    "localhost",
    8080,
    options,
    logger: logger // Логи будут включать реконнекты
);
```

### 3. Keep-Alive + Reconnect

Комбинируйте для надёжности:

```csharp
var options = new ClientOptions {
    KeepAliveInterval = TimeSpan.FromSeconds(30), // Обнаружение разрыва
    ReconnectPolicy = new ReconnectPolicy {       // Авто-восстановление
        MaxAttempts = int.MaxValue,
        UseExponentialBackoff = true,
    }
};
```

### 4. Обработка неудач

```csharp
try {
    await using var client = await VibeMQClient.ConnectAsync(
        "localhost",
        8080,
        new ClientOptions {
            ReconnectPolicy = new ReconnectPolicy {
                MaxAttempts = 5 // Ограниченные попытки
            }
        }
    );
    
    // Работа с клиентом
} catch (InvalidOperationException ex) {
    // Максимум попыток исчерпан
    logger.LogError(ex, "Failed to connect after max attempts");
}
```

### 5. Graceful Shutdown

```csharp
using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) => {
    e.Cancel = true;
    cts.Cancel(); // Остановить реконнекты при shutdown
};

await using var client = await VibeMQClient.ConnectAsync(...);

try {
    // Работа
} catch (OperationCanceledException) {
    // Graceful shutdown
}
```

## Примеры

### Долгосрочный воркер

```csharp
var policy = new ReconnectPolicy {
    MaxAttempts = int.MaxValue,
    InitialDelay = TimeSpan.FromSeconds(1),
    MaxDelay = TimeSpan.FromMinutes(5),
    UseExponentialBackoff = true,
};

var options = new ClientOptions {
    AuthToken = "worker-token",
    ReconnectPolicy = policy,
    KeepAliveInterval = TimeSpan.FromSeconds(30),
};

await using var client = await VibeMQClient.ConnectAsync("broker", 8080, options);

// Подписка будет восстановлена после любого разрыва
await using var subscription = await client.SubscribeAsync<Task>("tasks", async task => {
    await ProcessTaskAsync(task);
});

// Воркер работает бесконечно
await Task.Delay(TimeSpan.FromDays(1));
```

### Краткосрочная операция

```csharp
var policy = new ReconnectPolicy {
    MaxAttempts = 3,
    InitialDelay = TimeSpan.FromSeconds(1),
    MaxDelay = TimeSpan.FromSeconds(5),
};

var options = new ClientOptions {
    ReconnectPolicy = policy,
};

try {
    await using var client = await VibeMQClient.ConnectAsync("broker", 8080, options);
    
    // Быстрая операция
    await client.PublishAsync("queue", data);
    
} catch (InvalidOperationException) {
    // Не удалось подключиться
    Console.WriteLine("Failed to connect");
}
```

## См. также

- [Client](client.md) — клиентская библиотека
- [Configuration](configuration.md) — настройка клиента
