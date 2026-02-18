=============
Архитектура
=============

Это руководство описывает внутреннюю архитектуру VibeMQ и принципы его работы.

.. contents:: Содержание
   :local:
   :depth: 2

Обзор архитектуры
=================

VibeMQ построен по модульному принципу и состоит из нескольких независимых компонентов, которые взаимодействуют через чётко определённые интерфейсы.

.. code-block:: text

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
   │                │         Command Dispatcher             │   │
   │                │  - ConnectHandler                      │   │
   │                │  - PublishHandler                      │   │
   │                │  - SubscribeHandler                    │   │
   │                │  - AckHandler                          │   │
   │                └────────────────────────────────────────┘   │
   │                                   │                         │
   │                ┌──────────────────▼────────────────────┐    │
   │                │         Delivery Infrastructure       │    │
   │                │  - AckTracker (retry logic)           │    │
   │                │  - DeadLetterQueue                    │    │
   │                └───────────────────────────────────────┘    │
   └─────────────────────────────────────────────────────────────┘
                                 │
          ┌──────────────────────┼──────────────────────┐
          │                      │                      │
    ┌─────▼─────┐          ┌─────▼─────┐          ┌─────▼─────┐
    │  Client 1 │          │  Client 2 │          │  Client 3 │
    │(Publisher)│          │(Subscriber)│          │(Subscriber)│
    └───────────┘          └───────────┘          └───────────┘

Компоненты системы
==================

VibeMQ.Core (Ядро)
------------------

**Назначение:** Базовые модели, интерфейсы и конфигурация.

**Основные типы:**

+------------------------+--------------------------------------------------+
| Тип                    | Описание                                         |
+========================+==================================================+
| ``BrokerMessage``      | Модель сообщения брокера                         |
+------------------------+--------------------------------------------------+
| ``QueueInfo``          | Информация о состоянии очереди                   |
+------------------------+--------------------------------------------------+
| ``BrokerOptions``      | Конфигурация сервера                             |
+------------------------+--------------------------------------------------+
| ``QueueOptions``       | Настройки очереди                                |
+------------------------+--------------------------------------------------+
| ``ClientOptions``      | Настройки клиента                                |
+------------------------+--------------------------------------------------+
| ``IQueueManager``      | Интерфейс управления очередями                   |
+------------------------+--------------------------------------------------+
| ``IMessageStore``      | Интерфейс хранилища сообщений                    |
+------------------------+--------------------------------------------------+
| ``IAuthenticationService`` | Интерфейс аутентификации                      |
+------------------------+--------------------------------------------------+
| ``IBrokerMetrics``     | Интерфейс сбора метрик                           |
+------------------------+--------------------------------------------------+

**Перечисления:**

- ``DeliveryMode`` — режим доставки (RoundRobin, FanOut, Priority)
- ``MessagePriority`` — приоритет сообщения (Low, Normal, High, Critical)
- ``OverflowStrategy`` — стратегия переполнения очереди
- ``FailureReason`` — причина неудачной доставки
- ``CommandType`` — тип команды протокола

VibeMQ.Protocol (Протокол)
--------------------------

**Назначение:** Сериализация и передача сообщений по TCP.

**Фрейминг:**

Используется length-prefix подход для разделения сообщений в TCP-потоке:

.. code-block:: text

   [4 байта: длина в Big Endian][N байт: JSON-тело в UTF-8]

**Компоненты:**

- ``FrameReader`` — чтение фреймов из потока
- ``FrameWriter`` — запись фреймов в поток
- ``WriteBatcher`` — батчинг сообщений для производительности
- ``ProtocolMessage`` — базовый класс протокольного сообщения

**Формат сообщения:**

.. code-block:: json

   {
     "id": "msg_123",
     "type": "publish",
     "queue": "notifications",
     "payload": {"title": "Hello", "body": "World"},
     "headers": {
       "priority": "high",
       "correlationId": "corr_456"
     },
     "schemaVersion": "1.0"
   }

VibeMQ.Server (Сервер)
----------------------

**Назначение:** Реализация сервера брокера.

**Ключевые компоненты:**

**BrokerServer** — главный класс сервера:

.. code-block:: csharp

   public sealed partial class BrokerServer : IAsyncDisposable {
       public IBrokerMetrics Metrics { get; }
       public int ActiveConnections { get; }
       public int InFlightMessages { get; }
       
       public Task RunAsync(CancellationToken cancellationToken = default);
       public Task StopAsync(CancellationToken cancellationToken = default);
       public ValueTask DisposeAsync();
   }

**BrokerBuilder** — Fluent API для настройки:

.. code-block:: csharp

   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .UseAuthentication("token")
       .ConfigureQueues(options => { ... })
       .Build();

**QueueManager** — управление очередями:

- Создание и удаление очередей
- Публикация сообщений
- Подписка и отписка клиентов
- Acknowledgement сообщений

**ConnectionManager** — управление подключениями:

- Трекинг активных соединений
- Маршрутизация сообщений подписчикам
- Управление жизненным циклом подключений

**CommandDispatcher** — обработка команд:

- ``ConnectHandler`` — установка соединения
- ``PublishHandler`` — публикация сообщений
- ``SubscribeHandler`` — подписка на очередь
- ``UnsubscribeHandler`` — отписка от очереди
- ``AckHandler`` — подтверждение получения
- ``PingHandler`` — keep-alive

**AckTracker** — отслеживание подтверждений:

- Трекинг неподтверждённых сообщений
- Таймауты и повторные попытки
- Экспоненциальная задержка между ретраями

**DeadLetterQueue** — очередь неудачных сообщений:

- Хранение сообщений с неудачной доставкой
- Механизм повторной обработки

**RateLimiter** — ограничение скорости:

- Rate limiting по IP для подключений
- Rate limiting по клиенту для сообщений

VibeMQ.Client (Клиент)
----------------------

**Назначение:** Клиент для подключения к брокеру.

**VibeMQClient** — главный класс клиента:

.. code-block:: csharp

   public sealed partial class VibeMQClient : IAsyncDisposable {
       public bool IsConnected { get; }
       
       public static Task<VibeMQClient> ConnectAsync(...);
       public Task PublishAsync<T>(string queueName, T payload, ...);
       public Task<IAsyncDisposable> SubscribeAsync<T>(...);
       public Task UnsubscribeAsync(string queueName, ...);
       public Task DisconnectAsync(...);
       public ValueTask DisposeAsync();
   }

**ReconnectPolicy** — политика переподключения:

.. code-block:: csharp

   public sealed class ReconnectPolicy {
       public int MaxAttempts { get; set; } = int.MaxValue;
       public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);
       public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(5);
       public bool UseExponentialBackoff { get; set; } = true;
       
       public TimeSpan GetDelay(int attempt);
   }

VibeMQ.Health (Health Checks)
-----------------------------

**Назначение:** HTTP сервер для проверок здоровья.

**HealthCheckServer** — HTTP сервер:

.. code-block:: csharp

   public sealed partial class HealthCheckServer : IAsyncDisposable {
       public void Start();
       public ValueTask DisposeAsync();
   }

**Эндпоинты:**

- ``GET /health/`` — статус здоровья (200 OK или 503)
- ``GET /metrics/`` — метрики брокера (JSON)

**HealthStatus** — статус здоровья:

.. code-block:: json

   {
     "isHealthy": true,
     "status": "healthy",
     "activeConnections": 15,
     "queueCount": 5,
     "inFlightMessages": 42,
     "totalMessagesPublished": 125000,
     "totalMessagesDelivered": 124850,
     "memoryUsageMb": 256,
     "timestamp": "2026-02-18T10:30:00Z"
   }

Принципы работы
===============

Жизненный цикл сообщения
------------------------

1. **Публикация:**

   - Клиент отправляет команду ``Publish``
   - Сервер валидирует сообщение
   - Сообщение сохраняется в очередь
   - Отправляется подтверждение ``PublishAck``

2. **Маршрутизация:**

   - ``QueueManager`` определяет режим доставки
   - Для Round-robin выбирается следующий подписчик
   - Для Fan-out сообщение копируется для всех подписчиков
   - Для Priority-based сортируется по приоритету

3. **Доставка:**

   - Сообщение отправляется подписчику командой ``Deliver``
   - Запускается таймер ожидания ACK
   - Сообщение помечается как «в обработке» (in-flight)

4. **Подтверждение:**

   - Подписчик отправляет команду ``Ack``
   - ``AckTracker`` помечает сообщение как доставленное
   - Сообщение удаляется из in-flight
   - Обновляются метрики

5. **Повторная попытка (если нет ACK):**

   - Таймер истекает
   - Счётчик попыток увеличивается
   - Если попытки не исчерпаны — повторная отправка
   - Если исчерпаны — перемещение в Dead Letter Queue

Режимы доставки
---------------

**Round-robin:**

.. code-block:: text

   Publisher → Queue → Subscriber 1 (сообщение 1)
                     → Subscriber 2 (сообщение 2)
                     → Subscriber 1 (сообщение 3)
                     → Subscriber 2 (сообщение 4)

Каждое сообщение доставляется одному подписчику циклически.

**Fan-out с подтверждением:**

.. code-block:: text

   Publisher → Queue → Subscriber 1 (копия 1, требуется ACK)
                     → Subscriber 2 (копия 1, требуется ACK)
                     → Subscriber 3 (копия 1, требуется ACK)

Сообщение доставляется всем подписчикам, каждый должен подтвердить.

**Fan-out без подтверждения:**

.. code-block:: text

   Publisher → Queue → Subscriber 1 (копия 1)
                     → Subscriber 2 (копия 1)
                     → Subscriber 3 (копия 1)

Сообщение доставляется всем без ожидания подтверждения.

**Priority-based:**

.. code-block:: text

   Queue: [Critical:1] [High:2] [High:3] [Normal:4] [Low:5]
   
   Доставка: Critical → High → High → Normal → Low

Сообщения доставляются в порядке приоритета.

Keep-alive механизм
-------------------

Для поддержания активных соединений используется механизм PING/PONG:

.. code-block:: text

   Клиент                          Сервер
      │                              │
      │────── PING (каждые 30с) ────▶│
      │                              │
      │◄───── PONG (немедленно) ─────│
      │                              │

Если сервер не получает PING в течение таймаута, соединение закрывается.

Автоматические реконнекты
-------------------------

Клиент автоматически переподключается при разрыве соединения:

.. code-block:: text

   Попытка 1:等待 1с
   Попытка 2:等待 2с
   Попытка 3:等待 4с
   Попытка 4:等待 8с
   Попытка 5:等待 16с
   ...
   Попытка N:等待 5мин (максимум)

Используется экспоненциальная задержка с максимумом 5 минут.

Graceful Shutdown
-----------------

При остановке сервера выполняется корректное завершение:

1. Останавливается приём новых соединений
2. Отправляется уведомление клиентам о shutdown
3. Ожидается обработка in-flight сообщений (до 30с)
4. Закрываются все соединения
5. Очищаются ресурсы

Управление памятью
==================

Backpressure
------------

При достижении высокого уровня использования памяти:

1. **Watermark 80%:** Включается backpressure
2. **Watermark 90%:** Блокируются новые публикации
3. **Watermark 95%:** Применяется стратегия переполнения

Стратегии переполнения:

- **DropOldest** — удаление старейшего сообщения
- **DropNewest** — отклонение нового сообщения
- **BlockPublisher** — блокировка отправителя
- **RedirectToDlq** — перенаправление в DLQ

Пул объектов
------------

Для уменьшения аллокаций используется пул объектов сообщений:

.. code-block:: csharp

   public class MessageObjectPool {
       private readonly ConcurrentBag<BrokerMessage> _pool = new();
       
       public BrokerMessage Rent() { ... }
       public void Return(BrokerMessage message) { ... }
   }

Метрики и мониторинг
====================

Собираемые метрики:

**Счётчики:**

- ``TotalMessagesPublished`` — всего опубликовано
- ``TotalMessagesDelivered`` — всего доставлено
- ``TotalMessagesAcknowledged`` — всего подтверждено
- ``TotalRetries`` — всего повторных попыток
- ``TotalDeadLettered`` — всего в DLQ
- ``TotalErrors`` — всего ошибок
- ``TotalConnectionsAccepted`` — всего подключений
- ``TotalConnectionsRejected`` — всего отказов

**Gauge-метрики:**

- ``ActiveConnections`` — активные подключения
- ``ActiveQueues`` — активные очереди
- ``InFlightMessages`` — сообщения в обработке
- ``MemoryUsageBytes`` — использование памяти

**Латентность:**

- ``AverageDeliveryLatencyMs`` — средняя задержка доставки

Следующие шаги
==============

- :doc:`features` — подробный обзор возможностей
- :doc:`protocol` — детали протокола связи
- :doc:`monitoring` — мониторинг и метрики
