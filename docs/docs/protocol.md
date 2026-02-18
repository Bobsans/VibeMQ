# Protocol

Описание сетевого протокола VibeMQ.

## Расположение

```
src/VibeMQ.Protocol/
```

## Обзор

VibeMQ использует TCP в качестве транспорта с JSON-сериализацией сообщений.

**Характеристики:**
- Length-prefix framing для разделения сообщений
- JSON-формат для читаемости
- Поддержка сжатия (в будущем)
- Бинарный протокол (в бэклоге)

## Framing (разделение сообщений)

Для разделения сообщений в TCP-потоке используется **length-prefix** подход:

```
┌─────────────────────────────────────────────────────────────┐
│  [4 байта: длина тела]  [N байт: JSON-тело в UTF-8]        │
│  (Big Endian uint32)                                        │
└─────────────────────────────────────────────────────────────┘
```

### Формат фрейма

```
0                   1                   2                   3
0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
│                        Length (uint32)                        │
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
│                        JSON Body (UTF-8)                      │
│                              ...                              │
```

### Пример

**Сообщение:**
```json
{"type":"publish","queue":"test","payload":{"data":"hello"}}
```

**Фрейм:**
```
[0x00 0x00 0x00 0x3A]  // Длина: 58 байт (Big Endian)
[0x7B 0x22 0x74 0x79 ...]  // JSON тело в UTF-8
```

### Максимальный размер

Максимальный размер сообщения ограничивается конфигурацией сервера:

```csharp
var broker = BrokerBuilder.Create()
    .UseMaxMessageSize(1_048_576) // 1 MB (по умолчанию)
    .Build();
```

## Формат сообщения (JSON)

### Базовая структура

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

### Поля сообщения

| Поле | Тип | Обязательное | Описание |
|------|-----|--------------|----------|
| `id` | string | Нет | Уникальный ID сообщения (генерируется автоматически) |
| `type` | string | Да | Тип команды (publish, subscribe, ack, ping, pong, error) |
| `queue` | string | Зависит | Имя очереди |
| `payload` | object | Зависит | Данные сообщения |
| `headers` | object | Нет | Метаданные |
| `schemaVersion` | string | Нет | Версия протокола (по умолчанию: "1.0") |
| `errorMessage` | string | Для error | Текст ошибки |

## Типы команд

### CONNECT

Подключение клиента к брокеру.

**Запрос:**
```json
{
  "type": "connect",
  "payload": {
    "token": "my-secret-token"
  }
}
```

**Ответ (успех):**
```json
{
  "type": "connect_ack",
  "payload": {
    "clientId": "client_123",
    "serverVersion": "1.0.0"
  }
}
```

**Ответ (ошибка):**
```json
{
  "type": "error",
  "errorMessage": "Authentication failed"
}
```

### PUBLISH

Публикация сообщения в очередь.

**Запрос:**
```json
{
  "type": "publish",
  "queue": "notifications",
  "payload": {
    "title": "Hello",
    "body": "World"
  },
  "headers": {
    "correlationId": "corr_123",
    "priority": "high"
  }
}
```

**Ответ (успех):**
```json
{
  "type": "publish_ack",
  "queue": "notifications",
  "id": "msg_456"
}
```

**Ответ (ошибка):**
```json
{
  "type": "error",
  "errorMessage": "Queue not found"
}
```

### SUBSCRIBE

Подписка на очередь.

**Запрос:**
```json
{
  "type": "subscribe",
  "queue": "notifications"
}
```

**Ответ (успех):**
```json
{
  "type": "subscribe_ack",
  "queue": "notifications"
}
```

**Получение сообщения:**
```json
{
  "type": "message",
  "queue": "notifications",
  "id": "msg_789",
  "payload": {
    "title": "Hello",
    "body": "World"
  },
  "headers": {
    "timestamp": "2024-01-01T00:00:00Z"
  }
}
```

### ACK

Подтверждение получения сообщения.

**Запрос:**
```json
{
  "type": "ack",
  "id": "msg_789"
}
```

**Ответ:**
```json
{
  "type": "ack_ok",
  "id": "msg_789"
}
```

### PING / PONG

Keep-alive сообщения.

**Запрос:**
```json
{
  "type": "ping"
}
```

**Ответ:**
```json
{
  "type": "pong"
}
```

### UNSUBSCRIBE

Отписка от очереди.

**Запрос:**
```json
{
  "type": "unsubscribe",
  "queue": "notifications"
}
```

**Ответ:**
```json
{
  "type": "unsubscribe_ack",
  "queue": "notifications"
}
```

### ERROR

Сообщение об ошибке.

```json
{
  "type": "error",
  "errorMessage": "Description of the error"
}
```

## Заголовки (Headers)

### Стандартные заголовки

| Заголовок | Описание |
|-----------|----------|
| `correlationId` | ID для корреляции запросов/ответов |
| `replyTo` | Очередь для ответа |
| `priority` | Приоритет сообщения (low, normal, high, critical) |
| `timestamp` | Время создания сообщения (ISO 8601) |
| `contentType` | Тип контента payload (application/json) |
| `schemaVersion` | Версия схемы payload |

### Пример использования заголовков

**Request/Response:**
```json
{
  "type": "publish",
  "queue": "requests",
  "payload": {...},
  "headers": {
    "correlationId": "req_123",
    "replyTo": "responses"
  }
}
```

**Приоритет:**
```json
{
  "type": "publish",
  "queue": "alerts",
  "payload": {...},
  "headers": {
    "priority": "critical"
  }
}
```

## Сериализация

### System.Text.Json

VibeMQ использует `System.Text.Json` для сериализации:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

var options = new JsonSerializerOptions {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

var json = JsonSerializer.Serialize(message, options);
```

### ProtocolSerializer

Встроенный сериализатор:

```csharp
using VibeMQ.Protocol;

// Сериализация
var bytes = ProtocolSerializer.Serialize(message);

// Десериализация
var message = ProtocolSerializer.Deserialize(bytes);
```

### ProtocolJsonContext

Для лучшей производительности используется source generator:

```csharp
[JsonSerializable(typeof(ProtocolMessage))]
[JsonSerializable(typeof(CommandType))]
public partial class ProtocolJsonContext : JsonSerializerContext {
}
```

## Пул сообщений

Для уменьшения аллокаций используется пул объектов:

```csharp
using VibeMQ.Protocol;

// Взять из пула
var message = ProtocolMessagePool.Rent();

// Использовать
message.Type = CommandType.Publish;
message.Queue = "test";

// Вернуть в пул
ProtocolMessagePool.Return(message);
```

## Write Batcher

Батчинг для оптимизации записи в сокет:

```csharp
using VibeMQ.Protocol.Framing;

var batcher = new WriteBatcher(maxBatchSize: 100, maxBatchDelay: TimeSpan.FromMilliseconds(10));

// Добавить сообщения
await batcher.AddAsync(message1);
await batcher.AddAsync(message2);

// Отправить батч
await batcher.FlushAsync(stream);
```

## Примеры

### Полная последовательность: Publish/Subscribe

**1. Клиент подключается:**
```
Client → Server: {"type":"connect","payload":{"token":"secret"}}
Server → Client: {"type":"connect_ack","payload":{"clientId":"c1"}}
```

**2. Клиент подписывается:**
```
Client → Server: {"type":"subscribe","queue":"notifications"}
Server → Client: {"type":"subscribe_ack","queue":"notifications"}
```

**3. Другой клиент публикует:**
```
Client2 → Server: {"type":"publish","queue":"notifications","payload":{"title":"Hello"}}
Server → Client2: {"type":"publish_ack","id":"msg_123"}
```

**4. Сервер доставляет сообщение:**
```
Server → Client: {"type":"message","queue":"notifications","id":"msg_123","payload":{...}}
```

**5. Клиент подтверждает:**
```
Client → Server: {"type":"ack","id":"msg_123"}
Server → Client: {"type":"ack_ok","id":"msg_123"}
```

### Keep-Alive

```
Client → Server: {"type":"ping"}
Server → Client: {"type":"pong"}
```

## Безопасность

### Аутентификация

Токен передаётся при подключении:

```json
{
  "type": "connect",
  "payload": {
    "token": "my-secret-token"
  }
}
```

### Валидация размера

Сервер отклоняет сообщения больше `MaxMessageSize`:

```json
{
  "type": "error",
  "errorMessage": "Message size exceeds limit (1 MB)"
}
```

## Расширения (в будущем)

### Сжатие

```json
{
  "type": "publish",
  "queue": "test",
  "headers": {
    "compression": "gzip"
  },
  "payload": "H4sIA..." // Сжатые данные в base64
}
```

### Бинарный формат

Negotiation при подключении:

```json
{
  "type": "connect",
  "payload": {
    "token": "secret",
    "codec": "messagepack" // или "protobuf"
  }
}
```

## Best Practices

### 1. Используйте correlation ID

Для request/response:

```json
{
  "type": "publish",
  "queue": "requests",
  "headers": {
    "correlationId": "req_123",
    "replyTo": "responses"
  }
}
```

### 2. Добавляйте timestamp

```json
{
  "type": "publish",
  "payload": {...},
  "headers": {
    "timestamp": "2024-01-01T00:00:00Z"
  }
}
```

### 3. Обрабатывайте ошибки

```json
{
  "type": "error",
  "errorMessage": "..."
}
```

### 4. Keep-Alive

Отправляйте PING каждые 30-60 сек для поддержания соединения.

## См. также

- [Client](client.md) — клиентская реализация
- [Configuration](configuration.md) — настройка
