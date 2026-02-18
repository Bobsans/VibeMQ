# Dependency Injection

Интеграция VibeMQ с Microsoft Dependency Injection.

## Серверная DI-интеграция

### Установка

```bash
dotnet add package VibeMQ.Server.DependencyInjection
```

### Быстрый старт

```csharp
using VibeMQ.Server.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

// Регистрация брокера
builder.Services.AddVibeMQBroker(options => {
    options.Port = 8080;
    options.MaxConnections = 500;
    options.EnableAuthentication = true;
    options.AuthToken = "my-secret-token";
});

// Запуск хоста
await builder.Build().RunAsync();
```

### Как это работает

`AddVibeMQBroker` регистрирует:
1. `BrokerOptions` — конфигурация
2. `BrokerServer` — экземпляр брокера (singleton)
3. `VibeMQBrokerHostedService` — hosted service для запуска

Брокер автоматически запускается при старте хоста и останавливается при shutdown.

### Расширенная конфигурация

```csharp
using VibeMQ.Core.Enums;
using VibeMQ.Server.DependencyInjection;

builder.Services.AddVibeMQBroker(options => {
    options.Port = 8080;
    options.MaxConnections = 1000;
    options.MaxMessageSize = 2_097_152; // 2 MB
    options.EnableAuthentication = true;
    options.AuthToken = "strong-secret-token";
    
    options.QueueDefaults = new QueueDefaults {
        DefaultDeliveryMode = DeliveryMode.RoundRobin,
        MaxQueueSize = 10_000,
        EnableAutoCreate = true,
        EnableDeadLetterQueue = true,
        MaxRetryAttempts = 3,
    };
    
    options.RateLimit = new RateLimitOptions {
        Enabled = true,
        MaxConnectionsPerIpPerWindow = 50,
        MaxMessagesPerClientPerSecond = 1000,
    };
    
    options.Tls = new TlsOptions {
        Enabled = true,
        CertificatePath = "/etc/ssl/certs/vibemq.pfx",
        CertificatePassword = "cert-password",
    };
});

// Health checks
builder.Services.AddVibeMQHealthChecks(options => {
    options.Enabled = true;
    options.Port = 8081;
});
```

### Конфигурация из appsettings.json

```json
{
  "Broker": {
    "Port": 8080,
    "MaxConnections": 500,
    "EnableAuthentication": true,
    "AuthToken": "secret-token",
    "QueueDefaults": {
      "DefaultDeliveryMode": "RoundRobin",
      "MaxQueueSize": 10000,
      "EnableAutoCreate": true
    }
  }
}
```

```csharp
using VibeMQ.Server.DependencyInjection;

// Загрузка из IConfiguration
builder.Services.AddVibeMQBroker(); // Опции загружаются из "Broker" секции
```

### Создание очередей при старте

```csharp
using VibeMQ.Core.Enums;
using VibeMQ.Server;
using VibeMQ.Server.Queues;

builder.Services.AddVibeMQBroker(options => {
    options.Port = 8080;
    options.QueueDefaults.EnableAutoCreate = false;
});

// Создание очередей при старте
builder.Services.AddHostedService<QueueInitializerService>();

public class QueueInitializerService : IHostedService {
    private readonly IQueueManager _queueManager;
    private readonly ILogger<QueueInitializerService> _logger;

    public QueueInitializerService(
        IQueueManager queueManager,
        ILogger<QueueInitializerService> logger)
    {
        _queueManager = queueManager;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken) {
        _logger.LogInformation("Creating queues...");

        await _queueManager.CreateQueueAsync("notifications", new QueueOptions {
            Mode = DeliveryMode.FanOutWithAck,
            MaxQueueSize = 10_000,
            EnableDeadLetterQueue = true,
        }, cancellationToken);

        await _queueManager.CreateQueueAsync("orders", new QueueOptions {
            Mode = DeliveryMode.RoundRobin,
            MaxQueueSize = 5000,
        }, cancellationToken);

        _logger.LogInformation("Queues created");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

### Внедрение зависимостей в сервисы

```csharp
// Сервис для работы с очередями
public class NotificationService {
    private readonly IQueueManager _queueManager;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IQueueManager queueManager,
        ILogger<NotificationService> logger)
    {
        _queueManager = queueManager;
        _logger = logger;
    }

    public async Task SendNotificationAsync(string userId, string message) {
        var info = await _queueManager.GetQueueInfoAsync("notifications");
        _logger.LogInformation("Queue status: {Count} messages", info?.MessageCount ?? 0);

        // Логика отправки
    }
}

// Регистрация
builder.Services.AddSingleton<NotificationService>();
```

### Мониторинг метрик

```csharp
public class MetricsReporter : BackgroundService {
    private readonly IBrokerMetrics _metrics;
    private readonly ILogger<MetricsReporter> _logger;

    public MetricsReporter(
        IBrokerMetrics metrics,
        ILogger<MetricsReporter> logger)
    {
        _metrics = metrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        while (!stoppingToken.IsCancellationRequested) {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            _logger.LogInformation(
                "Published: {Published}, Delivered: {Delivered}, ACK: {Ack}",
                _metrics.TotalMessagesPublished,
                _metrics.TotalMessagesDelivered,
                _metrics.TotalAcknowledged
            );
        }
    }
}

builder.Services.AddSingleton<MetricsReporter>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MetricsReporter>());
```

## Клиентская DI-интеграция

### Установка

```bash
dotnet add package VibeMQ.Client.DependencyInjection
```

### Быстрый старт

```csharp
using VibeMQ.Client.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddVibeMQClient(options => {
    options.Host = "localhost";
    options.Port = 8080;
    options.AuthToken = "my-secret-token";
    options.KeepAliveInterval = TimeSpan.FromSeconds(30);
});

await builder.Build().RunAsync();
```

### Как это работает

`AddVibeMQClient` регистрирует:
1. `VibeMQClientSettings` — настройки клиента
2. `IVibeMQClientFactory` — фабрика для создания клиентов

### Использование фабрики

```csharp
public class OrderProcessor : BackgroundService {
    private readonly IVibeMQClientFactory _clientFactory;
    private readonly ILogger<OrderProcessor> _logger;

    public OrderProcessor(
        IVibeMQClientFactory clientFactory,
        ILogger<OrderProcessor> logger)
    {
        _clientFactory = clientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        await using var client = await _clientFactory.CreateAsync(stoppingToken);

        await using var subscription = await client.SubscribeAsync<Order>("orders", async order => {
            await ProcessOrderAsync(order);
        });

        await Task.Delay(Timeout.Infinite, stoppingToken);
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

### Расширенная конфигурация

```csharp
builder.Services.AddVibeMQClient(options => {
    options.Host = "broker.example.com";
    options.Port = 8080;
    options.AuthToken = "client-token";
    
    options.ReconnectPolicy = new ReconnectPolicy {
        MaxAttempts = int.MaxValue,
        InitialDelay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromMinutes(5),
        UseExponentialBackoff = true,
    };
    
    options.KeepAliveInterval = TimeSpan.FromSeconds(30);
    options.ConnectionTimeout = TimeSpan.FromSeconds(5);
    options.OperationTimeout = TimeSpan.FromSeconds(30);
});
```

### Конфигурация из appsettings.json

```json
{
  "VibeMQClient": {
    "Host": "localhost",
    "Port": 8080,
    "AuthToken": "client-token",
    "KeepAliveInterval": "00:00:30",
    "ReconnectPolicy": {
      "MaxAttempts": 10,
      "InitialDelay": "00:00:01",
      "MaxDelay": "00:05:00",
      "UseExponentialBackoff": true
    }
  }
}
```

```csharp
// Загрузка из IConfiguration
builder.Services.AddVibeMQClient(); // Опции из "VibeMQClient"
```

### Publisher сервис

```csharp
public class NotificationPublisher {
    private readonly IVibeMQClientFactory _clientFactory;
    private readonly ILogger<NotificationPublisher> _logger;

    public NotificationPublisher(
        IVibeMQClientFactory clientFactory,
        ILogger<NotificationPublisher> logger)
    {
        _clientFactory = clientFactory;
        _logger = logger;
    }

    public async Task PublishAsync(string title, string body) {
        await using var client = await _clientFactory.CreateAsync();

        await client.PublishAsync("notifications", new {
            Title = title,
            Body = body,
            Timestamp = DateTime.UtcNow
        });

        _logger.LogInformation("Notification published: {Title}", title);
    }
}

// Использование в API
[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase {
    private readonly NotificationPublisher _publisher;

    public NotificationsController(NotificationPublisher publisher) {
        _publisher = publisher;
    }

    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendNotificationRequest request) {
        await _publisher.PublishAsync(request.Title, request.Body);
        return Ok();
    }
}

public class SendNotificationRequest {
    public string Title { get; set; }
    public string Body { get; set; }
}
```

### Worker с подпиской

```csharp
public class EmailWorker : BackgroundService {
    private readonly IVibeMQClientFactory _clientFactory;
    private readonly IEmailService _emailService;
    private readonly ILogger<EmailWorker> _logger;

    public EmailWorker(
        IVibeMQClientFactory clientFactory,
        IEmailService emailService,
        ILogger<EmailWorker> logger)
    {
        _clientFactory = clientFactory;
        _emailService = emailService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        await using var client = await _clientFactory.CreateAsync(stoppingToken);

        await using var subscription = await client.SubscribeAsync<EmailRequest>("emails", async request => {
            try {
                await _emailService.SendAsync(request.To, request.Subject, request.Body);
                _logger.LogInformation("Email sent to {To}", request.To);
            } catch (Exception ex) {
                _logger.LogError(ex, "Failed to send email to {To}", request.To);
                throw; // Для retry
            }
        }, stoppingToken);

        _logger.LogInformation("Email worker started");

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}

public class EmailRequest {
    public string To { get; set; }
    public string Subject { get; set; }
    public string Body { get; set; }
}

builder.Services.AddHostedService<EmailWorker>();
builder.Services.AddSingleton<IEmailService, SmtpEmailService>();
```

## Полный пример: Server + Client

### Server (Program.cs)

```csharp
using VibeMQ.Server.DependencyInjection;
using VibeMQ.Core.Enums;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddVibeMQBroker(options => {
    options.Port = 8080;
    options.EnableAuthentication = true;
    options.AuthToken = "secret";
    options.QueueDefaults = new QueueDefaults {
        DefaultDeliveryMode = DeliveryMode.RoundRobin,
        EnableAutoCreate = true,
    };
});

builder.Services.AddHostedService<QueueInitializerService>();

await builder.Build().RunAsync();
```

### Client (Program.cs)

```csharp
using VibeMQ.Client.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddVibeMQClient(options => {
    options.Host = "localhost";
    options.Port = 8080;
    options.AuthToken = "secret";
});

builder.Services.AddHostedService<MessageWorker>();

await builder.Build().RunAsync();
```

## Best Practices

### 1. Используйте Hosted Services

Для долгосрочных подписчиков:

```csharp
builder.Services.AddHostedService<MessageWorker>();
```

### 2. Правильное управление временем жизни

```csharp
// ✅ Правильно
await using var client = await _clientFactory.CreateAsync();

// ❌ Неправильно
private readonly VibeMQClient _client; // Долгоживущая ссылка
```

### 3. Обработка ошибок в workers

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
    try {
        await using var client = await _clientFactory.CreateAsync(stoppingToken);
        
        await using var subscription = await client.SubscribeAsync<...>("queue", async msg => {
            try {
                await HandleMessageAsync(msg);
            } catch (Exception ex) {
                _logger.LogError(ex, "Message handling failed");
                throw; // Для retry
            }
        }, stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    } catch (OperationCanceledException) {
        // Graceful shutdown
    } catch (Exception ex) {
        _logger.LogError(ex, "Worker failed");
        throw;
    }
}
```

### 4. Мониторинг и логирование

```csharp
builder.Services.AddHostedService<MetricsReporter>();
builder.Logging.SetMinimumLevel(LogLevel.Information);
```

## См. также

- [Broker Server](server/broker-server.md) — сервер
- [Client](client/client.md) — клиент
- [Examples](examples.md) — примеры
