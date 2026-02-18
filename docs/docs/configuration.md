# Configuration

Настройка сервера и клиента VibeMQ.

## Серверная конфигурация

### BrokerOptions

Полная конфигурация брокера:

```csharp
public sealed class BrokerOptions {
    /// <summary>
    /// TCP порт для прослушивания.
    /// По умолчанию: 8080
    /// </summary>
    public int Port { get; set; } = 8080;

    /// <summary>
    /// Максимум клиентских подключений.
    /// По умолчанию: 1000
    /// </summary>
    public int MaxConnections { get; set; } = 1000;

    /// <summary>
    /// Максимальный размер сообщения в байтах.
    /// По умолчанию: 1 MB
    /// </summary>
    public int MaxMessageSize { get; set; } = 1_048_576;

    /// <summary>
    /// Включить аутентификацию по токену.
    /// По умолчанию: false
    /// </summary>
    public bool EnableAuthentication { get; set; }

    /// <summary>
    /// Токен для аутентификации.
    /// </summary>
    public string? AuthToken { get; set; }

    /// <summary>
    /// Настройки очередей по умолчанию.
    /// </summary>
    public QueueDefaults QueueDefaults { get; set; } = new();

    /// <summary>
    /// TLS/SSL конфигурация.
    /// </summary>
    public TlsOptions Tls { get; set; } = new();

    /// <summary>
    /// Rate limiting конфигурация.
    /// </summary>
    public RateLimitOptions RateLimit { get; set; } = new();
}
```

### Пример конфигурации через BrokerBuilder

```csharp
using VibeMQ.Core.Enums;
using VibeMQ.Server;

var broker = BrokerBuilder.Create()
    .UsePort(8080)
    .UseMaxConnections(500)
    .UseMaxMessageSize(2_097_152) // 2 MB
    .UseAuthentication("my-secret-token")
    .ConfigureQueues(options => {
        options.DefaultDeliveryMode = DeliveryMode.RoundRobin;
        options.MaxQueueSize = 10_000;
        options.EnableAutoCreate = true;
        options.EnableDeadLetterQueue = true;
    })
    .ConfigureRateLimiting(options => {
        options.Enabled = true;
        options.MaxConnectionsPerIpPerWindow = 50;
        options.MaxMessagesPerClientPerSecond = 1000;
    })
    .ConfigureTls(options => {
        options.Enabled = true;
        options.CertificatePath = "/path/to/cert.pfx";
        options.CertificatePassword = "cert-password";
    })
    .Build();
```

## QueueDefaults

Настройки по умолчанию для очередей:

```csharp
public sealed class QueueDefaults {
    /// <summary>
    /// Режим доставки по умолчанию.
    /// По умолчанию: RoundRobin
    /// </summary>
    public DeliveryMode DefaultDeliveryMode { get; set; } = DeliveryMode.RoundRobin;

    /// <summary>
    /// Максимальный размер очереди.
    /// По умолчанию: 10000
    /// </summary>
    public int MaxQueueSize { get; set; } = 10_000;

    /// <summary>
    /// Автоматическое создание очередей.
    /// По умолчанию: true
    /// </summary>
    public bool EnableAutoCreate { get; set; } = true;

    /// <summary>
    /// Включить Dead Letter Queue по умолчанию.
    /// По умолчанию: false
    /// </summary>
    public bool EnableDeadLetterQueue { get; set; }

    /// <summary>
    /// Максимум попыток доставки.
    /// По умолчанию: 3
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;
}
```

### Настройка QueueDefaults

```csharp
var broker = BrokerBuilder.Create()
    .ConfigureQueues(options => {
        options.DefaultDeliveryMode = DeliveryMode.FanOutWithAck;
        options.MaxQueueSize = 5000;
        options.EnableAutoCreate = false; // Требовать явного создания
        options.EnableDeadLetterQueue = true;
        options.MaxRetryAttempts = 5;
    })
    .Build();
```

## RateLimitOptions

Ограничение скорости для защиты от перегрузки:

```csharp
public sealed class RateLimitOptions {
    /// <summary>
    /// Включить rate limiting.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Максимум подключений с одного IP в окне.
    /// </summary>
    public int MaxConnectionsPerIpPerWindow { get; set; } = 50;

    /// <summary>
    /// Окно времени для подключения (сек).
    /// </summary>
    public int ConnectionWindowSeconds { get; set; } = 60;

    /// <summary>
    /// Максимум сообщений от клиента в секунду.
    /// </summary>
    public int MaxMessagesPerClientPerSecond { get; set; } = 1000;
}
```

### Настройка rate limiting

```csharp
var broker = BrokerBuilder.Create()
    .ConfigureRateLimiting(options => {
        options.Enabled = true;
        options.MaxConnectionsPerIpPerWindow = 100;
        options.ConnectionWindowSeconds = 60;
        options.MaxMessagesPerClientPerSecond = 5000;
    })
    .Build();
```

## TlsOptions

TLS/SSL конфигурация:

```csharp
public sealed class TlsOptions {
    /// <summary>
    /// Включить TLS.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Путь к PFX сертификату.
    /// </summary>
    public string? CertificatePath { get; set; }

    /// <summary>
    /// Пароль сертификата.
    /// </summary>
    public string? CertificatePassword { get; set; }
}
```

### Настройка TLS

```csharp
var broker = BrokerBuilder.Create()
    .ConfigureTls(options => {
        options.Enabled = true;
        options.CertificatePath = "/etc/vibemq/cert.pfx";
        options.CertificatePassword = "my-password";
    })
    .Build();
```

## HealthCheckOptions

Настройка health check сервера:

```csharp
public sealed class HealthCheckOptions {
    /// <summary>
    /// Включить health check сервер.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Порт для HTTP health check.
    /// </summary>
    public int Port { get; set; } = 8081;
}
```

### Включение health checks

```csharp
var broker = BrokerBuilder.Create()
    .ConfigureHealthChecks(options => {
        options.Enabled = true;
        options.Port = 9000;
    })
    .Build();
```

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
      "DefaultDeliveryMode": "RoundRobin",
      "MaxQueueSize": 10000,
      "EnableAutoCreate": true,
      "EnableDeadLetterQueue": true,
      "MaxRetryAttempts": 3
    },
    "RateLimit": {
      "Enabled": true,
      "MaxConnectionsPerIpPerWindow": 50,
      "ConnectionWindowSeconds": 60,
      "MaxMessagesPerClientPerSecond": 1000
    },
    "Tls": {
      "Enabled": false
    },
    "HealthChecks": {
      "Enabled": true,
      "Port": 8081
    }
  }
}
```

### Загрузка из IConfiguration

```csharp
using Microsoft.Extensions.Configuration;
using VibeMQ.Core.Configuration;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var brokerOptions = new BrokerOptions();
configuration.GetSection("Broker").Bind(brokerOptions);

var broker = BrokerBuilder.Create()
    .ConfigureFrom(brokerOptions)
    .Build();
```

## Клиентская конфигурация

### ClientOptions

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

### Пример конфигурации клиента

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

await using var client = await VibeMQClient.ConnectAsync("localhost", 8080, options);
```

### ReconnectPolicy

```csharp
public sealed class ReconnectPolicy {
    /// <summary>
    /// Максимум попыток подключения.
    /// </summary>
    public int MaxAttempts { get; set; } = int.MaxValue;

    /// <summary>
    /// Начальная задержка.
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Максимальная задержка.
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Экспоненциальный backoff.
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;
}
```

## DI-конфигурация

### Сервер

```csharp
using VibeMQ.Server.DependencyInjection;

services.AddVibeMQBroker(options => {
    options.Port = 8080;
    options.MaxConnections = 500;
    options.EnableAuthentication = true;
    options.AuthToken = "secret";
    options.QueueDefaults = new QueueDefaults {
        DefaultDeliveryMode = DeliveryMode.RoundRobin,
        MaxQueueSize = 10000,
    };
});
```

### Клиент

```csharp
using VibeMQ.Client.DependencyInjection;

services.AddVibeMQClient(options => {
    options.Host = "localhost";
    options.Port = 8080;
    options.AuthToken = "secret";
    options.KeepAliveInterval = TimeSpan.FromSeconds(30);
});
```

## Best Practices

### 1. Production настройки сервера

```csharp
var broker = BrokerBuilder.Create()
    .UsePort(8080)
    .UseMaxConnections(1000)
    .UseAuthentication("strong-secret-token")
    .ConfigureQueues(opts => {
        opts.DefaultDeliveryMode = DeliveryMode.RoundRobin;
        opts.MaxQueueSize = 10_000;
        opts.EnableDeadLetterQueue = true;
        opts.MaxRetryAttempts = 3;
    })
    .ConfigureRateLimiting(opts => {
        opts.Enabled = true;
        opts.MaxMessagesPerClientPerSecond = 1000;
    })
    .ConfigureHealthChecks(opts => {
        opts.Enabled = true;
        opts.Port = 8081;
    })
    .Build();
```

### 2. Production настройки клиента

```csharp
var options = new ClientOptions {
    AuthToken = "client-token",
    ReconnectPolicy = new ReconnectPolicy {
        MaxAttempts = int.MaxValue,
        UseExponentialBackoff = true,
    },
    KeepAliveInterval = TimeSpan.FromSeconds(30),
};
```

### 3. Development настройки

```csharp
var broker = BrokerBuilder.Create()
    .UsePort(8080)
    .UseMaxConnections(100) // Меньше для dev
    .ConfigureQueues(opts => {
        opts.EnableAutoCreate = true;
        opts.MaxQueueSize = 1000;
    })
    .Build();
```

### 4. High-throughput настройки

```csharp
var broker = BrokerBuilder.Create()
    .UseMaxConnections(5000)
    .UseMaxMessageSize(10_485_760) // 10 MB
    .ConfigureQueues(opts => {
        opts.MaxQueueSize = 100_000;
        opts.DefaultDeliveryMode = DeliveryMode.FanOutWithoutAck;
    })
    .ConfigureRateLimiting(opts => {
        opts.MaxMessagesPerClientPerSecond = 10_000;
    })
    .Build();
```

## См. также

- [Broker Server](server/broker-server.md) — сервер
- [Client](client/client.md) — клиент
- [Queues](server/queues.md) — очереди
