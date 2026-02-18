====================
Возможности
====================

Это руководство описывает все возможности и функции VibeMQ.

.. contents:: Содержание
   :local:
   :depth: 2

Публикация/Подписка (Pub/Sub)
=============================

VibeMQ реализует паттерн публикации/подписки через очереди сообщений.

**Основные понятия:**

- **Издатель (Publisher)** — клиент, отправляющий сообщения
- **Подписчик (Subscriber)** — клиент, получающий сообщения
- **Очередь (Queue)** — буфер для хранения сообщений
- **Брокер (Broker)** — сервер, управляющий очередями

**Базовый пример:**

.. code-block:: csharp

   // Издатель
   await client.PublishAsync("notifications", new {
       Title = "Hello",
       Body = "World"
   });

   // Подписчик
   await using var subscription = await client.SubscribeAsync<dynamic>(
       "notifications",
       async msg => {
           Console.WriteLine($"{msg.Title}: {msg.Body}");
       }
   );

Режимы доставки
===============

VibeMQ поддерживает четыре режима доставки сообщений:

Round-robin
-----------

**Описание:** Каждое сообщение доставляется одному подписчику циклически.

.. code-block:: text

   Publisher → [Queue] → Subscriber 1 (сообщение 1)
                       → Subscriber 2 (сообщение 2)
                       → Subscriber 1 (сообщение 3)

**Настройка:**

.. code-block:: csharp

   options.DefaultDeliveryMode = DeliveryMode.RoundRobin;

**Использование:**

- Обработка задач несколькими воркерами
- Балансировка нагрузки
- Очереди задач

Fan-out с подтверждением
------------------------

**Описание:** Сообщение доставляется всем подписчикам, каждый должен подтвердить получение.

.. code-block:: text

   Publisher → [Queue] → Subscriber 1 (копия + ACK)
                       → Subscriber 2 (копия + ACK)
                       → Subscriber 3 (копия + ACK)

**Настройка:**

.. code-block:: csharp

   options.DefaultDeliveryMode = DeliveryMode.FanOutWithAck;
   options.MaxRetryAttempts = 3;

**Использование:**

- Рассылка уведомлений
- Репликация данных
- Аудит и логирование

Fan-out без подтверждения
-------------------------

**Описание:** Сообщение доставляется всем подписчикам без подтверждения.

.. code-block:: text

   Publisher → [Queue] → Subscriber 1 (копия)
                       → Subscriber 2 (копия)
                       → Subscriber 3 (копия)

**Настройка:**

.. code-block:: csharp

   options.DefaultDeliveryMode = DeliveryMode.FanOutWithoutAck;

**Использование:**

- Broadcast-сообщения
- Обновления в реальном времени
- Стриминг данных

Priority-based
--------------

**Описание:** Сообщения доставляются по приоритету.

.. code-block:: text

   [Critical] → [High] → [Normal] → [Low]

**Приоритеты:**

+----------------+-------+----------------------------------+
| Приоритет      | Значе-| Описание                         |
|                | ние   |                                  |
+================+=======+==================================+
| Critical       | 3     | Критические, доставляются первыми|
+----------------+-------+----------------------------------+
| High           | 2     | Высокий приоритет                |
+----------------+-------+----------------------------------+
| Normal         | 1     | Обычный (по умолчанию)           |
+----------------+-------+----------------------------------+
| Low            | 0     | Низкий, доставляются последними  |
+----------------+-------+----------------------------------+

**Настройка:**

.. code-block:: csharp

   options.DefaultDeliveryMode = DeliveryMode.PriorityBased;

**Публикация с приоритетом:**

.. code-block:: csharp

   await client.PublishAsync("alerts", message, options => {
       options.Priority = MessagePriority.Critical;
   });

Гарантии доставки
=================

Подтверждения (ACK)
-------------------

VibeMQ использует механизм подтверждений для гарантии доставки:

.. code-block:: text

   Брокер → Доставить сообщение → Клиент
                                  │
                                  │ Обработка...
                                  │
          ◀────── ACK ────────────┘

**Как работает:**

1. Брокер отправляет сообщение клиенту
2. Запускается таймер ожидания ACK
3. Клиент обрабатывает сообщение и отправляет ACK
4. Брокер получает ACK и помечает сообщение как доставленное

**Автоматические ACK:**

По умолчанию клиент автоматически отправляет ACK после успешной обработки:

.. code-block:: csharp

   await using var subscription = await client.SubscribeAsync<dynamic>(
       "notifications",
       async msg => {
           await ProcessMessageAsync(msg);
           // ACK отправляется автоматически
       }
   );

Повторные попытки
-----------------

Если ACK не получен в течение таймаута, сообщение отправляется повторно:

.. code-block:: text

   Попытка 1 → Таймаут → Попытка 2 → Таймаут → Попытка 3 → DLQ

**Настройка:**

.. code-block:: csharp

   options.MaxRetryAttempts = 3;

**Экспоненциальная задержка:**

Между попытками используется экспоненциальная задержка:

- Попытка 1: немедленно
- Попытка 2: через 1с
- Попытка 3: через 2с
- Попытка 4: через 4с
- ...

Dead Letter Queue (DLQ)
-----------------------

Сообщения, которые не удалось доставить после всех попыток, перемещаются в Dead Letter Queue:

.. code-block:: csharp

   options.EnableDeadLetterQueue = true;
   options.DeadLetterQueueName = "dead-letters";
   options.MaxRetryAttempts = 3;

**Причины попадания в DLQ:**

- Превышено количество попыток доставки
- Истёк TTL сообщения
- Ошибка десериализации
- Исключение в обработчике

**Обработка DLQ:**

.. code-block:: csharp

   var dlqMessages = await queueManager.GetDeadLetterMessagesAsync(100);
   
   foreach (var message in dlqMessages) {
       // Повторная обработка или логирование
       await RetryOrLogAsync(message);
   }

Управление очередями
====================

Создание очереди
----------------

**Автоматическое создание:**

При публикации в несуществующую очередь она создаётся автоматически:

.. code-block:: csharp

   options.EnableAutoCreate = true;

**Ручное создание:**

.. code-block:: csharp

   await queueManager.CreateQueueAsync("my-queue", new QueueOptions {
       DeliveryMode = DeliveryMode.RoundRobin,
       MaxQueueSize = 10_000,
       MessageTtl = TimeSpan.FromHours(1),
   });

Удаление очереди
----------------

.. code-block:: csharp

   await queueManager.DeleteQueueAsync("my-queue");

Получение информации
--------------------

.. code-block:: csharp

   var info = await queueManager.GetQueueInfoAsync("my-queue");
   
   Console.WriteLine($"Очередь: {info.Name}");
   Console.WriteLine($"Сообщений: {info.MessageCount}");
   Console.WriteLine($"Подписчиков: {info.SubscriberCount}");
   Console.WriteLine($"Режим: {info.DeliveryMode}");

Список очередей
---------------

.. code-block:: csharp

   var queues = await queueManager.ListQueuesAsync();
   
   foreach (var queueName in queues) {
       Console.WriteLine(queueName);
   }

Приоритеты сообщений
====================

VibeMQ поддерживает приоритеты сообщений для важной доставки.

**Уровни приоритета:**

.. code-block:: csharp

   public enum MessagePriority {
       Low = 0,      // Низкий
       Normal = 1,   // Обычный (по умолчанию)
       High = 2,     // Высокий
       Critical = 3  // Критический
   }

**Публикация с приоритетом:**

.. code-block:: csharp

   // Критическое сообщение
   await client.PublishAsync("alerts", alertData, options => {
       options.Priority = MessagePriority.Critical;
   });

   // Высокий приоритет
   await client.PublishAsync("notifications", data, options => {
       options.Priority = MessagePriority.High;
   });

   // Обычный приоритет (по умолчанию)
   await client.PublishAsync("logs", logData);

Keep-alive (PING/PONG)
======================

Для поддержания активных соединений используется механизм keep-alive:

.. code-block:: text

   Клиент                    Сервер
      │                         │
      │─── PING (30с) ─────────▶│
      │                         │
      │◄── PONG (немедленно) ───│

**Настройка на клиенте:**

.. code-block:: csharp

   var client = await VibeMQClient.ConnectAsync(
       "localhost",
       8080,
       new ClientOptions {
           KeepAliveInterval = TimeSpan.FromSeconds(30)
       }
   );

Автоматические реконнекты
=========================

Клиент автоматически переподключается при разрыве соединения.

**Настройка политики реконнекта:**

.. code-block:: csharp

   var client = await VibeMQClient.ConnectAsync(
       "localhost",
       8080,
       new ClientOptions {
           ReconnectPolicy = new ReconnectPolicy {
               MaxAttempts = 10,
               InitialDelay = TimeSpan.FromSeconds(1),
               MaxDelay = TimeSpan.FromMinutes(5),
               UseExponentialBackoff = true
           }
       }
   );

**Параметры:**

+------------------------+------------------+----------------------------------+
| Параметр               | По умолчанию     | Описание                         |
+========================+==================+==================================+
| ``MaxAttempts``        | int.MaxValue     | Макс. количество попыток         |
+------------------------+------------------+----------------------------------+
| ``InitialDelay``       | 1с               | Начальная задержка               |
+------------------------+------------------+----------------------------------+
| ``MaxDelay``           | 5мин             | Максимальная задержка            |
+------------------------+------------------+----------------------------------+
| ``UseExponentialBackoff`` | true          | Экспоненциальное увеличение      |
+------------------------+------------------+----------------------------------+

Аутентификация
==============

Токен-базированная аутентификация:

**На сервере:**

.. code-block:: csharp

   var broker = BrokerBuilder.Create()
       .UseAuthentication("my-secret-token")
       .Build();

**На клиенте:**

.. code-block:: csharp

   var client = await VibeMQClient.ConnectAsync(
       "localhost",
       8080,
       new ClientOptions {
           AuthToken = "my-secret-token"
       }
   );

.. warning::

   Используйте сложные токены (32+ символа) и храните их в защищённом месте.

TLS/SSL шифрование
==================

Поддержка шифрования транспорта:

**На сервере:**

.. code-block:: csharp

   .UseTls(options => {
       options.Enabled = true;
       options.CertificatePath = "/path/to/cert.pfx";
       options.CertificatePassword = "cert-password";
   })

**На клиенте:**

.. code-block:: csharp

   var client = await VibeMQClient.ConnectAsync(
       "localhost",
       8080,
       new ClientOptions {
           UseTls = true,
           SkipCertificateValidation = false  // Только для тестов!
       }
   );

Rate Limiting
=============

Защита от перегрузок:

**Настройка:**

.. code-block:: csharp

   .ConfigureRateLimiting(options => {
       options.Enabled = true;
       options.MaxConnectionsPerIpPerWindow = 100;
       options.ConnectionWindowSeconds = 60;
       options.MaxMessagesPerClientPerSecond = 1000;
   });

**Параметры:**

+------------------------+------------------+----------------------------------+
| Параметр               | По умолчанию     | Описание                         |
+========================+==================+==================================+
| ``Enabled``            | false            | Включить rate limiting           |
+------------------------+------------------+----------------------------------+
| ``MaxConnectionsPerIpPerWindow`` | 100    | Макс. подключений с IP в окно    |
+------------------------+------------------+----------------------------------+
| ``ConnectionWindowSeconds`` | 60          | Окно времени (секунды)           |
+------------------------+------------------+----------------------------------+
| ``MaxMessagesPerClientPerSecond`` | 1000  | Макс. сообщений в секунду        |
+------------------------+------------------+----------------------------------+

Graceful Shutdown
=================

Корректная остановка сервера без потери сообщений:

.. code-block:: csharp

   var cts = new CancellationTokenSource();
   Console.CancelKeyPress += (_, e) => {
       e.Cancel = true;
       cts.Cancel();
   };

   await broker.RunAsync(cts.Token);
   // Автоматически выполняется StopAsync()

**Этапы shutdown:**

1. Остановка приёма новых соединений
2. Уведомление клиентов о shutdown
3. Ожидание обработки in-flight сообщений (до 30с)
4. Закрытие всех соединений
5. Очистка ресурсов

Health Checks
=============

HTTP эндпоинты для мониторинга:

**Включение:**

.. code-block:: csharp

   .ConfigureHealthChecks(options => {
       options.Enabled = true;
       options.Port = 8081;
   })

**Эндпоинты:**

- ``GET /health/`` — статус здоровья (200 OK или 503)
- ``GET /metrics/`` — метрики брокера (JSON)

**Пример ответа /health/:**

.. code-block:: json

   {
     "status": "healthy",
     "active_connections": 15,
     "queue_count": 5,
     "memory_usage_mb": 256
   }

**Пример ответа /metrics/:**

.. code-block:: json

   {
     "total_messages_published": 125000,
     "total_messages_delivered": 124850,
     "total_acknowledged": 124800,
     "active_connections": 15,
     "active_queues": 5,
     "memory_usage_bytes": 268435456,
     "average_delivery_latency_ms": 2.5
   }

Метрики
=======

**Счётчики:**

- ``TotalMessagesPublished`` — всего опубликовано
- ``TotalMessagesDelivered`` — всего доставлено
- ``TotalMessagesAcknowledged`` — всего подтверждено
- ``TotalRetries`` — повторных попыток
- ``TotalDeadLettered`` — в DLQ
- ``TotalErrors`` — ошибок
- ``TotalConnectionsAccepted`` — подключений принято
- ``TotalConnectionsRejected`` — подключений отклонено

**Gauge-метрики:**

- ``ActiveConnections`` — активные подключения
- ``ActiveQueues`` — активные очереди
- ``InFlightMessages`` — в обработке
- ``MemoryUsageBytes`` — использование памяти

**Латентность:**

- ``AverageDeliveryLatencyMs`` — средняя задержка доставки

Следующие шаги
==============

- :doc:`server-setup` — настройка сервера
- :doc:`client-usage` — использование клиента
- :doc:`monitoring` — мониторинг
