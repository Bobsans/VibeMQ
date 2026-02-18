# Architecture

VibeMQ построен по модульной архитектуре с чётким разделением ответственности.

## Высокоуровневая архитектура

```
┌─────────────────────────────────────────────────────────────┐
│                      VibeMQ.Server                          │
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

## Компоненты

### 1. BrokerServer

**Расположение:** `VibeMQ.Server/BrokerServer.cs`

Точка входа для сервера. Управляет жизненным циклом:
- Запуск TCP-листенера
- Принятие клиентских соединений
- Graceful shutdown

**Ключевые методы:**
- `RunAsync(CancellationToken)` — запуск сервера
- `StopAsync(CancellationToken)` — остановка
- `DisposeAsync()` — очистка ресурсов

### 2. ConnectionManager

**Расположение:** `VibeMQ.Server/Connections/ConnectionManager.cs`

Управляет TCP-соединениями клиентов:
- Трекинг активных подключений
- Ограничение максимального числа соединений
- Предоставление списка подписчиков для очереди

### 3. IClientConnection

**Расположение:** `VibeMQ.Server/Connections/ClientConnection.cs`

Обёртка над `TcpClient` с буферизацией:
- Чтение фреймов из сокета
- Запись сообщений с батчингом
- Поддержка TLS

### 4. QueueManager

**Расположение:** `VibeMQ.Server/Queues/QueueManager.cs`

Управляет очередями и подписками:
- Создание/удаление очередей
- Публикация сообщений
- Доставка подписчикам (RoundRobin/FanOut)
- Интеграция с AckTracker и DLQ

### 5. MessageRouter / CommandDispatcher

**Расположение:** `VibeMQ.Server/Handlers/CommandDispatcher.cs`

Маршрутизация сообщений по обработчикам:
- `CONNECT` — подключение клиента
- `PUBLISH` — публикация сообщения
- `SUBSCRIBE` — подписка на очередь
- `UNSUBSCRIBE` — отписка от очереди
- `ACK` — подтверждение получения
- `PING` — keep-alive

### 6. AckTracker

**Расположение:** `VibeMQ.Server/Delivery/AckTracker.cs`

Гарантия доставки через подтверждение:
- Трекинг неподтверждённых сообщений
- Таймауты и повторная отправка
- Экспоненциальный backoff

### 7. DeadLetterQueue

**Расположение:** `VibeMQ.Server/Delivery/DeadLetterQueue.cs`

Обработка неудачных сообщений:
- Сохранение сообщений после исчерпания попыток
- Причины неудач (MaxRetries, Expired, DeserializationError)
- Возможность повторной обработки

### 8. HealthCheckServer

**Расположение:** `VibeMQ.Health/HealthCheckServer.cs`

HTTP-сервер для health checks:
- Лёгкая реализация без ASP.NET Core
- Статус брокера (healthy/unhealthy)
- Метрики: подключения, очереди, память

### 9. BrokerMetrics

**Расположение:** `VibeMQ.Server/Metrics/BrokerMetrics.cs`

Сбор метрик производительности:
- Счётчики: опубликовано, доставлено, ACK, ошибки
- Gauge: активные подключения, очереди, память
- Гистограммы: латентность доставки

## Поток обработки сообщения

```
Клиент → TCP Server → ConnectionManager → CommandDispatcher
                                              │
                                              ▼
                                         PublishHandler
                                              │
                                              ▼
                                         QueueManager
                                              │
                    ┌─────────────────────────┼─────────────────────────┐
                    ▼                         ▼                         ▼
              MessageQueue              MessageQueue              MessageQueue
              (notifications)           (tasks)                   (events)
                    │                         │                         │
                    ▼                         ▼                         ▼
              Subscribers               Subscribers               Subscribers
```

## Режимы доставки

### RoundRobin

Сообщение доставляется **одному** подписчику из очереди.

```
Publisher → Queue → Subscriber 1
                  → Subscriber 2 (следующее сообщение)
                  → Subscriber 3 (следующее сообщение)
```

### FanOutWithAck

Сообщение доставляется **всем** подписчикам, требуется ACK.

```
Publisher → Queue → Subscriber 1 ──ACK─┐
                  → Subscriber 2 ──ACK─┤→ AckTracker
                  → Subscriber 3 ──ACK─┘
```

### FanOutWithoutAck

Сообщение доставляется **всем** подписчикам без подтверждения.

```
Publisher → Queue → Subscriber 1
                  → Subscriber 2
                  → Subscriber 3
```

## Хранение данных

### In-Memory (текущая реализация)

- Быстрый доступ к сообщениям
- Нет персистентности между перезапусками
- Ограничено доступной памятью

### Persistent (бэклог)

- SQLite / RocksDB / PostgreSQL
- Сохранение состояния при shutdown
- Восстановление после перезапуска

## Безопасность

### Аутентификация

- Token-based аутентификация при подключении
- Проверка токена через `IAuthenticationService`

### Rate Limiting

- Ограничение подключений по IP
- Ограничение сообщений в секунду на клиента

### TLS

- Опциональное шифрование транспорта
- Настройка через `TlsOptions`

## Масштабируемость

### Текущее состояние

- Один узел (single-node)
- Вертикальное масштабирование (больше CPU/RAM)

### Будущее (бэклог)

- Кластеризация (multi-node)
- Горизонтальное масштабирование
- Репликация между узлами
