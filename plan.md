# Детальный план работ по созданию VibeMQ — месседж-брокера на C#

## 📋 Обзор проекта

**Простой, но надёжный месседж-брокер** на C# с использованием TCP в качестве транспорта. Поддерживает pub/sub, очереди с гарантией доставки, keep-alive, реконнекты, аутентификацию по токену и другие возможности. Предназначен для использования в качестве серверной библиотеки.

---

## 🎯 Цели и требования

### Основные требования:
- ✅ Pub/Sub с использованием очередей
- ✅ Гарантия доставки через подтверждение (ack)
- ✅ Keep-alive (PING/PONG) и автоматические реконнекты
- ✅ Поддержка разных режимов доставки (round-robin, fan-out)
- ✅ Опциональная аутентификация по токену
- ✅ Graceful shutdown
- ✅ Управление памятью и backpressure
- ✅ Health checks для оркестрации

### Нефункциональные требования:
- Производительность: 10K+ сообщений/сек на одном узле
- Надёжность: минимизация потерь сообщений при перезапусках
- Масштабируемость: поддержка горизонтального масштабирования (задел на будущее)
- Мониторинг: метрики и логирование

---

## 🏗️ Архитектура

### Высокоуровневая архитектура:
```
┌─────────────────────────────────────────────────────────────┐
│                      VibeMQ.Server                   │
├─────────────────────────────────────────────────────────────┤
│  ┌──────────┐  ┌──────────────┐  ┌────────────────────┐     │
│  │TCP Server│◄─┤Connection    │◄─┤Queue Manager       │     │
│  │          │  │Manager       │  │  - Queues          │     │
│  └──────────┘  │  - Clients   │  │  - Subscriptions   │     │
│                │  - Health    │  │  - Delivery modes  │     │
│                └──────────────┘  └────────────────────┘     │
│                            │                │               │
│                ┌───────────▼────────────────▼───────────┐   │
│                │         Message Router                 │   │
│                │  - Command handlers                    │   │
│                │  - Message dispatcher                  │   │
│                │  - Ack manager                         │   │
│                └────────────────────────────────────────┘   │
│                                   │                         │
│                ┌──────────────────▼────────────────────┐    │
│                │         Storage Layer                 │    │
│                │  - In-memory queues                   │    │
│                │  - Persistent storage (бэклог)        │    │
│                └───────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
```

### Компоненты системы:

1. **BrokerServer** - точка входа, управляет жизненным циклом
2. **ConnectionManager** - управляет TCP-соединениями
3. **IClientConnection** - обёртка над TcpClient с буферизацией
4. **QueueManager** - управляет очередями и подписками
5. **MessageRouter** - маршрутизация сообщений
6. **CommandHandler** - обработка команд (паттерн Command)
7. **HealthCheckService** - health checks для оркестраторов
8. **MetricsCollector** - сбор метрик производительности
9. **ReconnectHandler** - на стороне клиента для переподключений

---

## 📁 Структура решения

```
VibeMQ/
├── src/
│   ├── VibeMQ.Core/           # Ядро системы, общие модели
│   ├── VibeMQ.Server/         # Серверная библиотека
│   ├── VibeMQ.Client/         # Клиентская библиотека
│   ├── VibeMQ.Protocol/       # Протокол, сериализация
│   └── VibeMQ.Health/         # Health checks
├── examples/
│   ├── Example.Server/               # Пример запуска сервера
│   ├── Example.Client/               # Пример использования клиента
│   └── Example.Worker/               # Пример фонового обработчика
├── tools/
│   └── VibeMQ.Cli/            # CLI-утилита для управления
├── tests/
│   ├── VibeMQ.Core.Tests/
│   ├── VibeMQ.Server.Tests/
│   ├── VibeMQ.Client.Tests/
│   └── VibeMQ.Integration.Tests/
├── benchmarks/
│   └── VibeMQ.Benchmarks/     # Бенчмарки производительности
└── Directory.Build.props
```

---

## 🔧 Технологический стек

- **Целевая платформа**: .NET 8.0 (LTS), в дальнейшем — поддержка .NET 10
- **Сериализация**: System.Text.Json (базовый протокол — JSON; в будущем возможна интеграция бинарного формата как альтернативы)
- **DI**: Microsoft.Extensions.DependencyInjection
- **Логирование**: Microsoft.Extensions.Logging
- **Конфигурация**: Microsoft.Extensions.Configuration
- **Тестирование**: xUnit, Moq, TestContainers (для интеграционных тестов)
- **Бенчмарки**: BenchmarkDotNet
- **CLI**: System.CommandLine

---

## 📋 Этап 1: Подготовка и проектирование (1-2 недели)

### 1.1. Уточнение функционала и API
- [ ] Определение публичного API библиотеки
- [ ] Проектирование fluent API для конфигурации
- [ ] Проектирование модели команд и сообщений
- [ ] Определение контрактов протокола

### 1.2. Проектирование протокола

#### Фрейминг (TCP framing)
Для разделения сообщений в TCP-потоке используется **length-prefix** подход:
```
[4 байта: длина тела в Big Endian uint32] [N байт: JSON-тело сообщения в UTF-8]
```
Максимальный размер сообщения ограничивается конфигурацией (`MaxMessageSize`).

#### Формат сообщения (JSON)
Базовый протокол — JSON. В будущем возможна интеграция бинарного формата (MessagePack, Protobuf) как альтернативного кодека.
```json
{
  "id": "msg_123",
  "type": "publish|subscribe|ack|ping|...",
  "queue": "notifications",
  "payload": {...},
  "headers": {
    "correlationId": "corr_123",
    "priority": "high",
    "timestamp": "2024-01-01T00:00:00Z"
  },
  "schemaVersion": "1.0"
}
```
> **Примечание**: `payload` на транспортном уровне передаётся как `JsonElement`. Типизированная десериализация выполняется на стороне клиента/подписчика.

### 1.3. Проектирование хранения
> **Решение**: на текущем этапе — только in-memory хранение. Persistent storage вынесен в бэклог (Этап 10).

```csharp
// Интерфейсы хранилища (in-memory реализация)
public interface IMessageStore {
    Task<string> AddAsync(MessageDto message);
    Task<MessageDto> GetAsync(string id);
    Task<bool> RemoveAsync(string id);
    Task<IEnumerable<MessageDto>> GetPendingAsync(string queueName);
}

public interface IQueueStore {
    Task CreateQueueAsync(QueueOptions options);
    Task DeleteQueueAsync(string queueName);
    Task<QueueInfo> GetQueueInfoAsync(string queueName);
    Task<IEnumerable<string>> ListQueuesAsync();
}
```

### 1.4. Создание репозитория и CI/CD
- [ ] Настройка Git репозитория
- [ ] Настройка GitHub Actions
- [ ] Настройка code quality tools (SonarQube, Codecov)
- [ ] Шаблоны pull request

---

## 🔨 Этап 2: Базовый каркас (1 неделя)

### 2.1. Настройка проектов
- [ ] Создание решения и проектов
- [ ] Настройка зависимостей
- [ ] Настройка общих свойств сборки

### 2.2. Базовые модели
> **Примечание**: на транспортном уровне `Payload` передаётся как `JsonElement`. Типизированная десериализация — на стороне получателя.  
> Сжатие сообщений не реализуется на текущем этапе. Если потребуется — будет реализовано на уровне протокола (framing), а не отдельных сообщений.

```csharp
// В VibeMQ.Core
public class MessageDto {
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string QueueName { get; set; }
    public JsonElement Payload { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, string> Headers { get; set; } = new();
    public string SchemaVersion { get; set; } = "1.0";
    public MessagePriority Priority { get; set; } = MessagePriority.Normal;
}

public enum MessagePriority {
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}
```

### 2.3. Конфигурация
```json
// appsettings.json сервера
{
  "Broker": {
    "Port": 8080,
    "MaxConnections": 1000,
    "MaxMessageSize": 1048576,
    "EnableAuthentication": false,
    "AuthToken": "secret-token",
    "QueueDefaults": {
      "MaxQueueSize": 10000,
      "DefaultDeliveryMode": "RoundRobin",
      "EnableAutoCreate": true
    }
  },
  "HealthChecks": {
    "Enabled": true,
    "Port": 8081
  }
}
```

---

## 🧠 Этап 3: Реализация ядра (2-3 недели)

### 3.1. Реализация протокола
- [ ] TCP сервер с async/await
- [ ] Парсинг JSON сообщений
- [ ] Валидация сообщений
- [ ] Обработка keep-alive (PING/PONG)

### 3.2. Система очередей
```csharp
public class QueueManager : IQueueManager {
    private readonly ConcurrentDictionary<string, IMessageQueue> _queues;
    private readonly QueueOptions _defaultOptions;
    
    public Task CreateQueueAsync(string name, QueueOptions options = null);
    public Task PublishAsync<T>(string queueName, MessageDto<T> message);
    public Task<IDisposable> SubscribeAsync(string queueName, 
        Func<MessageDto<object>, Task> handler);
    public Task<bool> AcknowledgeAsync(string messageId);
}
```

### 3.3. Режимы доставки
```csharp
public enum DeliveryMode {
    RoundRobin,          // Одному подписчику
    FanOutWithAck,       // Всем, с подтверждением
    FanOutWithoutAck,    // Всем, без подтверждения
    PriorityBased        // По приоритету сообщений
}

public class QueueOptions {
    public DeliveryMode Mode { get; set; } = DeliveryMode.RoundRobin;
    public int MaxQueueSize { get; set; } = 10000;
    public TimeSpan? MessageTtl { get; set; }
    public bool EnableDeadLetterQueue { get; set; }
    public string DeadLetterQueueName { get; set; }
    public OverflowStrategy OverflowStrategy { get; set; } = OverflowStrategy.DropOldest;
    public int MaxRetryAttempts { get; set; } = 3;
}
```

### 3.4. Управление памятью
```csharp
public enum OverflowStrategy {
    DropOldest,     // Удалить самое старое сообщение
    DropNewest,     // Отклонить новое сообщение
    BlockPublisher, // Блокировать отправителя
    RedirectToDlq   // Перенаправить в Dead Letter Queue
}

public class MemoryManager {
    private readonly long _maxMemoryBytes;
    private readonly double _highWatermark;
    private readonly double _lowWatermark;
    
    public bool IsMemoryCritical { get; private set; }
    
    public void MonitorMemoryUsage();
    public void ApplyBackpressure();
}
```

---

## 🔄 Этап 4: Гарантия доставки и надёжность (2 недели)

### 4.1. Система подтверждений (ACK)
- [ ] Трекинг неподтверждённых сообщений
- [ ] Повторная отправка при таймауте
- [ ] Обработка дубликатов ACK
- [ ] Экспоненциальный backoff для ретраев

### 4.2. Dead Letter Queue
```csharp
public class DeadLetterQueue {
    public Task HandleFailedMessageAsync(MessageDto message, FailureReason reason);
    
    public Task<IEnumerable<MessageDto>> GetMessagesAsync(int count);
    public Task<bool> RetryMessageAsync(string messageId);
}

public enum FailureReason {
    MaxRetriesExceeded,
    MessageExpired,
    DeserializationError,
    HandlerException
}
```

### 4.3. Graceful Shutdown
```csharp
public class BrokerServer : IAsyncDisposable {
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly TaskCompletionSource _shutdownTcs = new();
    
    public async Task StopAsync(CancellationToken cancellationToken = default) {
        // 1. Остановить прием новых соединений
        _listener.Stop();
        
        // 2. Уведомить клиентов о shutdown
        await NotifyClientsAboutShutdownAsync();
        
        // 3. Подождать обработки in-flight сообщений
        await WaitForInFlightMessagesAsync(TimeSpan.FromSeconds(30));
        
        // 4. Сохранить состояние (если нужно)
        await PersistStateAsync();
        
        // 5. Закрыть все соединения
        await CloseAllConnectionsAsync();
        
        // 6. Очистить ресурсы
        DisposeResources();
        
        _shutdownTcs.SetResult();
    }
}
```

### 4.4. Реконнекты на клиенте
```csharp
public class ReconnectPolicy {
    public int MaxAttempts { get; set; } = int.MaxValue;
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(5);
    public bool UseExponentialBackoff { get; set; } = true;
    
    public TimeSpan GetDelay(int attempt) {
        if (!UseExponentialBackoff) {
            return InitialDelay;
        }
            
        var delay = TimeSpan.FromTicks(InitialDelay.Ticks * (long)Math.Pow(2, attempt - 1));
        return delay > MaxDelay ? MaxDelay : delay;
    }
}
```

---

## 🔐 Этап 5: Безопасность и аутентификация (1 неделя)

### 5.1. Аутентификация
> **Решение**: на текущем этапе — простая проверка валидности токена (совпадение с серверным). Гранулярная авторизация по очередям/операциям — в бэклоге.

```csharp
public interface IAuthenticationService {
    /// <summary>
    /// Validates the provided token against the configured server token.
    /// </summary>
    Task<bool> AuthenticateAsync(string token);
}
```

### 5.2. Безопасность транспорта
- [ ] Поддержка TLS (опционально)
- [ ] Валидация сертификатов
- [ ] Ограничение скорости соединений

### 5.3. Защита от атак
- [ ] Rate limiting по IP/клиенту
- [ ] Валидация размера сообщений
- [ ] Защита от DDOS (basic level)

---

## 📊 Этап 6: Мониторинг и метрики (1 неделя)

### 6.1. Сбор метрик
```csharp
public class BrokerMetrics {
    // Counters
    public long TotalMessagesPublished { get; private set; }
    public long TotalMessagesDelivered { get; private set; }
    public long TotalAcknowledged { get; private set; }
    public long TotalErrors { get; private set; }
    
    // Gauges
    public int ActiveConnections { get; private set; }
    public int ActiveQueues { get; private set; }
    public long MemoryUsageBytes { get; private set; }
    
    // Histograms
    public Histogram DeliveryLatency { get; private set; }
    public Histogram ProcessingTime { get; private set; }
    
    public void IncrementPublished() => Interlocked.Increment(ref TotalMessagesPublished);
    public void RecordDeliveryLatency(TimeSpan latency) => DeliveryLatency.Record(latency.TotalMilliseconds);
}
```

### 6.2. Health Checks
> **Решение**: простой HTTP-обработчик на базе `HttpListener` без зависимости от ASP.NET Core.

```csharp
// Лёгкий HTTP health check сервер (без ASP.NET Core)
public class HealthCheckServer : IAsyncDisposable {
    private readonly HttpListener _listener;
    private readonly IBrokerMetrics _metrics;
    private readonly IConnectionManager _connectionManager;
    private readonly IQueueManager _queueManager;

    public async Task HandleRequestAsync(HttpListenerContext context) {
        var response = new {
            status = _memoryManager.IsMemoryCritical ? "unhealthy" : "healthy",
            active_connections = _connectionManager.ActiveCount,
            queue_count = _queueManager.QueueCount,
            memory_usage_mb = Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024
        };

        context.Response.StatusCode = _memoryManager.IsMemoryCritical ? 503 : 200;
        context.Response.ContentType = "application/json";
        await JsonSerializer.SerializeAsync(context.Response.OutputStream, response);
    }
}
```

### 6.3. Логирование
```csharp
public static class LoggerExtensions {
    public static void LogMessageReceived(this ILogger logger, string messageId, string queueName, LogLevel level = LogLevel.Debug) {
        logger.Log(level, "Message {MessageId} received for queue {QueueName}", messageId, queueName);
    }
    
    public static void LogMessageDelivered(this ILogger logger, string messageId, string clientId, TimeSpan latency) {
        logger.LogInformation("Message {MessageId} delivered to {ClientId} in {Latency}ms", messageId, clientId, latency.TotalMilliseconds);
    }
}
```

---

## 🧪 Этап 7: Тестирование (2 недели)

### 7.1. Unit тесты
- [ ] Тестирование модели команд
- [ ] Тестирование сериализации
- [ ] Тестирование логики очередей
- [ ] Тестирование обработчиков

### 7.2. Интеграционные тесты
```csharp
[Collection("BrokerIntegration")]
public class BrokerIntegrationTests : IAsyncLifetime {
    private TestBroker _broker;
    
    [Fact]
    public async Task PublishSubscribe_RoundRobin_WorksCorrectly() {
        // Arrange
        var publisher = await _broker.CreateClientAsync();
        var subscriber1 = await _broker.CreateClientAsync();
        var subscriber2 = await _broker.CreateClientAsync();
        
        // Act & Assert
        // ... тестовая логика
    }
}
```

### 7.3. Нагрузочное тестирование
```csharp
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class BrokerBenchmarks {
    private BrokerServer _broker;
    private VibeMQClient _client;
    
    [Benchmark]
    public async Task Publish_1000_Messages() {
        for (int i = 0; i < 1000; i++) {
            await _client.PublishAsync("test-queue", new { Index = i, Data = "test" });
        }
    }
    
    [Benchmark]
    public async Task Subscribe_Process_1000_Messages() {
        var processed = 0;
        using var subscription = await _client.SubscribeAsync<TestMessage>("test-queue", msg => {
            processed++; 
            return Task.CompletedTask; 
        });

        await Task.Delay(1000);
        return processed;
    }
}
```

### 7.4. Тестирование отказоустойчивости
- [ ] Тестирование отключения сети
- [ ] Тестирование перезапуска сервера
- [ ] Тестирование нехватки памяти
- [ ] Тестирование обработки некорректных сообщений

---

## 🚀 Этап 8: Документация и примеры (1 неделя)

### 8.1. Документация API
- [ ] XML documentation для публичных API
- [ ] README с примерами использования
- [ ] Документация по протоколу
- [ ] Руководство по развёртыванию

### 8.2. Примеры использования
```csharp
// Пример сервера
var broker = BrokerBuilder.Create()
    .UsePort(8080)
    .UseAuthentication("my-secret-token")
    .ConfigureQueues(options => {
        options.DefaultDeliveryMode = DeliveryMode.RoundRobin;
        options.MaxQueueSize = 5000;
        options.EnableDeadLetterQueue = true;
    })
    .ConfigureHealthChecks(options => {
        options.Enabled = true;
        options.Port = 8081;
    })
    .Build();

await broker.RunAsync();

// Пример клиента
var client = await VibeMQClient.ConnectAsync("localhost", 8080, new ClientOptions {
    AuthToken = "my-secret-token",
    ReconnectPolicy = new ReconnectPolicy {
        MaxAttempts = 10,
        UseExponentialBackoff = true
    }
});

await client.SubscribeAsync<Notification>("notifications", async notification => {
    Console.WriteLine($"Received: {notification.Title}");
    await ProcessNotificationAsync(notification);
});

await client.PublishAsync("notifications", new Notification { Title = "Hello", Body = "World" });
```

### 8.3. CLI утилита
```bash
# Примеры использования CLI
vibemq subscribe notifications --format json
vibemq publish notifications '{"title": "Hello"}'
vibemq queue list
vibemq queue info notifications
vibemq queue create orders --mode round-robin --max-size 10000
vibemq health check
```

### 8.4. Docker образ
```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app
EXPOSE 8080 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
# ... сборка

FROM base AS final
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "VibeMQ.Server.dll"]
```

---

## 📈 Этап 9: Производительность и оптимизация (1 неделя)

### 9.1. Профилирование
- [ ] Анализ производительности с помощью dotTrace
- [ ] Выявление узких мест
- [ ] Оптимизация аллокаций памяти
- [ ] Оптимизация сериализации

### 9.2. Оптимизации
```csharp
// Пул объектов для сообщений
public class MessageObjectPool {
    private readonly ConcurrentBag<MessageDto<object>> _pool = new();
    
    public MessageDto<object> Rent() {
        if (_pool.TryTake(out var message)) {
            return message;
        }
            
        return new MessageDto<object>();
    }
    
    public void Return(MessageDto<object> message) {
        message.Id = null;
        message.QueueName = null;
        message.Payload = null;
        message.Headers.Clear();
        _pool.Add(message);
    }
}

// Батчинг для записи в сокет
public class SocketBatcher {
    private readonly List<byte[]> _batch = new();
    private readonly int _maxBatchSize;
    private readonly TimeSpan _maxBatchDelay;
    
    public async Task SendBatchAsync(NetworkStream stream) {
        if (_batch.Count == 0) {
            return;
        }
            
        var combined = CombineBuffers(_batch);
        await stream.WriteAsync(combined);
        _batch.Clear();
    }
}
```

### 9.3. Настройка GC
```csharp
// Рекомендации для high-throughput
GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
```

---

## 🔮 Этап 10: Будущие улучшения (бэклог)

### 10.1. Persistence слой
```csharp
public interface IPersistentMessageStore : IMessageStore {
    Task InitializeAsync();
    Task BackupAsync(string path);
    Task RestoreAsync(string path);
    Task CompactAsync(); // Для журнальных структур
}

// Варианты реализации:
// - SQLite для embedded
// - RocksDB для high-write
// - PostgreSQL для production
```

### 10.2. Кластеризация
```csharp
public interface IClusterManager {
    Task<NodeInfo> JoinClusterAsync(string clusterAddress);
    Task LeaveClusterAsync();
    Task SyncStateAsync(NodeInfo node);
    Task<LeaderElectionResult> ElectLeaderAsync();
}

public class ClusterOptions {
    public DiscoveryMode DiscoveryMode { get; set; }
    public ReplicationFactor ReplicationFactor { get; set; } = 3;
    public ConsistencyLevel ConsistencyLevel { get; set; } = ConsistencyLevel.Quorum;
}
```

### 10.3. Поддержка других протоколов
- [ ] AMQP 1.0 адаптер
- [ ] MQTT адаптер
- [ ] HTTP REST API
- [ ] WebSocket поддержка

### 10.4. Расширенный мониторинг
- [ ] Prometheus метрики
- [ ] Grafana дашборды
- [ ] Distributed tracing (OpenTelemetry)
- [ ] Audit logging

### 10.5. Управление через веб-интерфейс
- [ ] Веб-админка на Blazor/React
- [ ] Real-time мониторинг очередей
- [ ] Управление подписками
- [ ] Настройка политик

### 10.6. Поддержка .NET 10
- [ ] Multi-targeting .NET 8 / .NET 10
- [ ] Использование новых API и оптимизаций .NET 10

### 10.7. Сжатие на уровне протокола
- [ ] Опциональное сжатие фреймов (gzip, brotli, lz4)
- [ ] Negotiation сжатия при подключении клиента

### 10.8. Гранулярная авторизация
- [ ] Авторизация по очередям и операциям (publish, subscribe, create, delete)
- [ ] Ролевая модель доступа

### 10.9. Бинарный протокол
- [ ] Альтернативный кодек (MessagePack / Protobuf)
- [ ] Negotiation формата при подключении

---

## 📅 Оценка времени и ресурсов

### Общая оценка: 12-16 недель

| Этап | Время | Основные задачи |
|------|-------|-----------------|
| 1. Подготовка | 1-2 недели | Проектирование, настройка инфраструктуры |
| 2. Каркас | 1 неделя | Базовые модели, конфигурация |
| 3. Ядро | 2-3 недели | Очереди, протокол, доставка |
| 4. Надёжность | 2 недели | ACK, DLQ, graceful shutdown |
| 5. Безопасность | 1 неделя | Аутентификация, TLS |
| 6. Мониторинг | 1 неделя | Метрики, health checks |
| 7. Тестирование | 2 недели | Unit, интеграционные, нагрузочные тесты |
| 8. Документация | 1 неделя | Примеры, документация, CLI |
| 9. Оптимизация | 1 неделя | Профилирование, оптимизация |
| **Итого** | **12-16 недель** | |

### Требуемая команда:
- 1 Senior .NET разработчик (архитектура, ядро)
- 1 Middle .NET разработчик (клиент, тесты, документация)
- Опционально: DevOps для CI/CD и развёртывания

---

## ✅ Критерии успеха

### Функциональные:
1. ✅ Поддержка pub/sub с гарантией доставки
2. ✅ Обработка 10K+ сообщений/сек на commodity hardware
3. ✅ Автоматические реконнекты клиентов
4. ✅ Graceful shutdown без потери сообщений
5. ✅ Health checks для оркестраторов

### Нефункциональные:
1. ✅ 99.9% доступность в рамках одного узла
2. ✅ Латентность < 10ms для 95% сообщений
3. ✅ Потребление памяти < 1GB на 1M сообщений в очереди
4. ✅ Полное покрытие unit-тестами критического кода
5. ✅ Комплексная документация с примерами

---

## 🚨 Риски и митигация

### Риск 1: Производительность при гарантии доставки
**Митигация**: Прототипирование системы ACK на раннем этапе, нагрузочное тестирование после каждой итерации

### Риск 2: Сложность отладки распределённой системы
**Митигация**: Подробное логирование, correlation IDs, distributed tracing из коробки

### Риск 3: Управление памятью при больших очередях
**Митигация**: Реализация backpressure, мониторинг памяти, graceful degradation

### Риск 4: Совместимость с существующими системами
**Митигация**: Следование стандартным паттернам месседжинга, предоставление адаптеров

---

## 📚 Ресурсы и зависимости

### Внешние зависимости:
- .NET 8.0 Runtime
- Для TLS: сертификаты (самоподписанные или от CA)
- Для persistence: выбранная БД (опционально)

### Рекомендуемая литература:
1. "Designing Data-Intensive Applications" - Martin Kleppmann
2. "Enterprise Integration Patterns" - Gregor Hohpe
3. RabbitMQ, Kafka, NATS документация
4. .NET Performance и Memory Management

---

Этот план обеспечивает создание надёжного, производительного и расширяемого месседж-брокера с фокусом на production-готовность. Итеративный подход позволяет получить работающее решение уже через 4-6 недель и постепенно наращивать функциональность.