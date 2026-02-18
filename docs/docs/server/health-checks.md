# Health Checks

Health Check Server предоставляет HTTP-эндпоинты для мониторинга состояния брокера.

## Расположение

```
src/VibeMQ.Health/HealthCheckServer.cs
```

## Обзор

`HealthCheckServer` — это лёгкий HTTP-сервер на базе `HttpListener`, который не требует ASP.NET Core.

**Возможности:**
- Проверка состояния (healthy/unhealthy)
- Метрики: подключения, очереди, память
- JSON-ответы
- Настраиваемый порт

## Быстрый старт

### Включение health checks

```csharp
using VibeMQ.Core.Configuration;
using VibeMQ.Server;

var broker = BrokerBuilder.Create()
    .UsePort(8080)
    .ConfigureHealthChecks(options => {
        options.Enabled = true;
        options.Port = 8081;
    })
    .Build();
```

### Проверка состояния

```bash
curl http://localhost:8081/health
```

**Ответ:**
```json
{
  "status": "healthy",
  "active_connections": 42,
  "queue_count": 5,
  "memory_usage_mb": 256
}
```

## Конфигурация

### HealthCheckOptions

```csharp
public sealed class HealthCheckOptions {
    /// <summary>
    /// Включить health check сервер.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// TCP порт для HTTP health check сервера.
    /// </summary>
    public int Port { get; set; } = 8081;
}
```

### Пример конфигурации

```csharp
var broker = BrokerBuilder.Create()
    .ConfigureHealthChecks(options => {
        options.Enabled = true;
        options.Port = 9000; // Кастомный порт
    })
    .Build();
```

## Эндпоинты

### GET /health

Возвращает текущее состояние брокера.

**Запрос:**
```bash
curl http://localhost:8081/health
```

**Успешный ответ (200 OK):**
```json
{
  "status": "healthy",
  "active_connections": 42,
  "queue_count": 5,
  "memory_usage_mb": 256
}
```

**Ответ при unhealthy (503 Service Unavailable):**
```json
{
  "status": "unhealthy",
  "active_connections": 0,
  "queue_count": 0,
  "memory_usage_mb": 512
}
```

### GET /metrics

Возвращает расширенные метрики.

**Запрос:**
```bash
curl http://localhost:8081/metrics
```

**Ответ:**
```json
{
  "total_messages_published": 15000,
  "total_messages_delivered": 14500,
  "total_acknowledged": 14000,
  "total_errors": 50,
  "active_connections": 42,
  "active_queues": 5,
  "memory_usage_bytes": 268435456,
  "delivery_latency_p50_ms": 5.2,
  "delivery_latency_p95_ms": 15.8,
  "delivery_latency_p99_ms": 45.3
}
```

### GET /queues

Список очередей и их состояние.

**Запрос:**
```bash
curl http://localhost:8081/queues
```

**Ответ:**
```json
[
  {
    "name": "notifications",
    "message_count": 150,
    "subscriber_count": 3,
    "mode": "RoundRobin"
  },
  {
    "name": "orders",
    "message_count": 50,
    "subscriber_count": 5,
    "mode": "FanOutWithAck"
  }
]
```

## Интеграция с оркестраторами

### Kubernetes

```yaml
apiVersion: v1
kind: Pod
metadata:
  name: vibemq-broker
spec:
  containers:
  - name: vibemq
    image: vibemq:latest
    ports:
    - containerPort: 8080  # Broker
    - containerPort: 8081  # Health check
    livenessProbe:
      httpGet:
        path: /health
        port: 8081
      initialDelaySeconds: 10
      periodSeconds: 30
    readinessProbe:
      httpGet:
        path: /health
        port: 8081
      initialDelaySeconds: 5
      periodSeconds: 10
```

### Docker Compose

```yaml
version: '3.8'

services:
  vibemq:
    image: vibemq:latest
    ports:
      - "8080:8080"
      - "8081:8081"
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8081/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 10s
```

### Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0

WORKDIR /app
COPY . .

EXPOSE 8080 8081

ENTRYPOINT ["dotnet", "VibeMQ.Server.dll"]
```

## Мониторинг состояния

### Статусы health check

| Статус | HTTP Code | Описание |
|--------|-----------|----------|
| `healthy` | 200 OK | Брокер работает нормально |
| `unhealthy` | 503 Service Unavailable | Критические проблемы |

### Критерии unhealthy

Брокер считается unhealthy если:
- Потребление памяти критическое (>90%)
- Внутренняя ошибка компонента
- Сервер не запустился

### Пример проверки в коде

```csharp
using System.Net.Http;
using System.Text.Json;

public class HealthCheckClient {
    private readonly HttpClient _httpClient = new();

    public async Task<bool> IsHealthyAsync(string host, int port = 8081) {
        try {
            var response = await _httpClient.GetAsync($"http://{host}:{port}/health");
            return response.IsSuccessStatusCode;
        } catch {
            return false;
        }
    }

    public async Task<HealthStatus> GetHealthStatusAsync(string host, int port = 8081) {
        var response = await _httpClient.GetAsync($"http://{host}:{port}/health");
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<HealthStatus>(json);
    }
}

public class HealthStatus {
    public string Status { get; set; }
    public int ActiveConnections { get; set; }
    public int QueueCount { get; set; }
    public long MemoryUsageMb { get; set; }
}
```

## Логирование

Health Check Server логирует свои события:

```csharp
// При запуске
LogHealthCheckStarted(8081);

// При обработке запроса
LogHealthCheckRequested("/health", 200);

// При ошибке
LogHealthCheckError(ex, "Failed to process request");
```

## Безопасность

### Ограничение доступа

Health Check Server не имеет встроенной аутентификации. Для ограничения доступа:

1. **Сетевой уровень:**
   - Bind на localhost (только локальный доступ)
   - Firewall rules
   - Kubernetes NetworkPolicy

2. **Reverse proxy:**
   - Nginx/Apache с аутентификацией
   - API Gateway

### Пример: только localhost

```csharp
// HealthCheckServer по умолчанию слушает localhost
// Для изменения отредактируйте префикс URL в коде
```

## Best Practices

### 1. Отдельный порт

Используйте отдельный порт для health checks:

```csharp
.ConfigureHealthChecks(opts => opts.Port = 8081) // Не 8080!
```

### 2. Частота проверки

- **Liveness probe:** каждые 30 сек
- **Readiness probe:** каждые 10 сек

### 3. Таймауты

```yaml
livenessProbe:
  timeoutSeconds: 5  # Не слишком долго
  failureThreshold: 3 # 3 неудачи = restart
```

### 4. Initial delay

Дайте брокеру время на запуск:

```yaml
livenessProbe:
  initialDelaySeconds: 10 # Подождать 10 сек перед первой проверкой
```

### 5. Мониторинг метрик

Регулярно опрашивайте `/metrics`:

```csharp
// Prometheus scraper
var metrics = await httpClient.GetAsync("http://vibemq:8081/metrics");
```

## Интеграция с системами мониторинга

### Prometheus

```yaml
# prometheus.yml
scrape_configs:
  - job_name: 'vibemq'
    static_configs:
      - targets: ['vibemq:8081']
    metrics_path: /metrics
    scrape_interval: 15s
```

### Grafana

Импортируйте дашборд для визуализации:
- Active connections
- Queue sizes
- Message rates
- Latency percentiles

## См. также

- [Broker Server](broker-server.md) — основной сервер
- [Configuration](configuration.md) — настройка
- [Metrics](broker-server.md#метрики) — метрики брокера
