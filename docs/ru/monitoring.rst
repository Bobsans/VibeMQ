====================
Мониторинг
====================

Это руководство описывает возможности мониторинга и health checks в VibeMQ.

.. contents:: Содержание
   :local:
   :depth: 2

Health Checks
=============

VibeMQ предоставляет HTTP эндпоинты для проверки здоровья и получения метрик.

Включение health checks
-----------------------

.. code-block:: csharp

   .ConfigureHealthChecks(options => {
       options.Enabled = true;
       options.Port = 8081;
   })

Эндпоинты
---------

GET /health/
~~~~~~~~~~~~

Возвращает статус здоровья сервера.

**Запрос:**

.. code-block:: bash

   curl http://localhost:8081/health/

**Ответ (200 OK):**

.. code-block:: json

   {
     "status": "healthy",
     "active_connections": 15,
     "queue_count": 5,
     "memory_usage_mb": 256
   }

**Ответ (503 Service Unavailable):**

.. code-block:: json

   {
     "status": "unhealthy",
     "active_connections": 0,
     "queue_count": 0,
     "memory_usage_mb": 512
   }

**Коды состояния:**

- ``200 OK`` — сервер здоров
- ``503 Service Unavailable`` — сервер нездоров (критическое использование памяти)

GET /metrics/
~~~~~~~~~~~~~

Возвращает подробные метрики сервера.

**Запрос:**

.. code-block:: bash

   curl http://localhost:8081/metrics/

**Ответ:**

.. code-block:: json

   {
     "total_messages_published": 125000,
     "total_messages_delivered": 124850,
     "total_messages_acknowledged": 124800,
     "total_retries": 150,
     "total_dead_lettered": 50,
     "total_errors": 5,
     "total_connections_accepted": 500,
     "total_connections_rejected": 10,
     "active_connections": 15,
     "active_queues": 5,
     "in_flight_messages": 42,
     "memory_usage_bytes": 268435456,
     "average_delivery_latency_ms": 2.5,
     "timestamp": "2026-02-18T10:30:00Z"
   }

Метрики
=======

Счётчики (Counters)
-------------------

Счётчики только увеличиваются и показывают накопленные значения.

TotalMessagesPublished
~~~~~~~~~~~~~~~~~~~~~~

**Тип:** ``long``

**Описание:** Общее количество опубликованных сообщений.

**Использование:**

- Отслеживание нагрузки на сервер
- Расчёт throughput (сообщений/сек)

TotalMessagesDelivered
~~~~~~~~~~~~~~~~~~~~~~

**Тип:** ``long``

**Описание:** Общее количество доставленных сообщений.

**Использование:**

- Мониторинг успешности доставки
- Расчёт процента успешных доставок

TotalMessagesAcknowledged
~~~~~~~~~~~~~~~~~~~~~~~~~

**Тип:** ``long``

**Описание:** Общее количество подтверждённых сообщений.

**Использование:**

- Проверка работы ACK-механизма
- Выявление проблем с подтверждениями

TotalRetries
~~~~~~~~~~~~

**Тип:** ``long``

**Описание:** Общее количество повторных попыток доставки.

**Использование:**

- Выявление проблем с доставкой
- Оптимизация таймаутов

TotalDeadLettered
~~~~~~~~~~~~~~~~~

**Тип:** ``long``

**Описание:** Количество сообщений в Dead Letter Queue.

**Использование:**

- Мониторинг неудачных доставок
- Требует внимания при росте

TotalErrors
~~~~~~~~~~~

**Тип:** ``long``

**Описание:** Общее количество ошибок.

**Использование:**

- Общий показатель здоровья системы
- Требует расследования при росте

TotalConnectionsAccepted
~~~~~~~~~~~~~~~~~~~~~~~~

**Тип:** ``long``

**Описание:** Количество принятых подключений.

**Использование:**

- Мониторинг нагрузки на сервер
- Планирование ресурсов

TotalConnectionsRejected
~~~~~~~~~~~~~~~~~~~~~~~~

**Тип:** ``long``

**Описание:** Количество отклонённых подключений.

**Использование:**

- Выявление проблем с capacity
- Настройка rate limiting

Gauge-метрики
-------------

Gauge-метрики могут увеличиваться и уменьшаться, показывая текущее состояние.

ActiveConnections
~~~~~~~~~~~~~~~~~

**Тип:** ``int``

**Описание:** Текущее количество активных подключений.

**Нормальные значения:** Зависит от нагрузки

**Тревога:** Приближение к ``MaxConnections``

ActiveQueues
~~~~~~~~~~~~

**Тип:** ``int``

**Описание:** Текущее количество активных очередей.

**Использование:**

- Мониторинг использования очередей
- Выявление неиспользуемых очередей

InFlightMessages
~~~~~~~~~~~~~~~~

**Тип:** ``int``

**Описание:** Количество сообщений в обработке (ожидающих ACK).

**Нормальные значения:** Зависит от нагрузки

**Тревога:** Высокие значения могут указывать на:
- Проблемы с обработчиками
- Медленных подписчиков
- Проблемы с сетью

MemoryUsageBytes
~~~~~~~~~~~~~~~~

**Тип:** ``long``

**Описание:** Текущее использование памяти в байтах.

**Нормальные значения:** < 80% от доступной памяти

**Тревога:** > 90% — возможен backpressure

Латентность
-----------

AverageDeliveryLatencyMs
~~~~~~~~~~~~~~~~~~~~~~~~

**Тип:** ``double``

**Описание:** Средняя задержка доставки сообщений (мс).

**Нормальные значения:** < 10 мс

**Тревога:** > 50 мс — требует оптимизации

Мониторинг с Prometheus
=======================

Пример экспортера метрик:

.. code-block:: csharp

   using Prometheus;

   public class VibeMQMetricsExporter : BackgroundService {
       private readonly IBrokerMetrics _metrics;
       private readonly ILogger<VibeMQMetricsExporter> _logger;

       // Counters
       private readonly Counter _messagesPublished;
       private readonly Counter _messagesDelivered;
       private readonly Counter _messagesAcknowledged;
       private readonly Counter _errors;

       // Gauges
       private readonly Gauge _activeConnections;
       private readonly Gauge _activeQueues;
       private readonly Gauge _inFlightMessages;
       private readonly Gauge _memoryUsage;

       // Histogram
       private readonly Histogram _deliveryLatency;

       public VibeMQMetricsExporter(
           IBrokerMetrics metrics,
           ILogger<VibeMQMetricsExporter> logger) {
           _metrics = metrics;
           _logger = logger;

           _messagesPublished = Metrics.CreateCounter(
               "vibemq_messages_published_total",
               "Total number of messages published");

           _messagesDelivered = Metrics.CreateCounter(
               "vibemq_messages_delivered_total",
               "Total number of messages delivered");

           _messagesAcknowledged = Metrics.CreateCounter(
               "vibemq_messages_acknowledged_total",
               "Total number of messages acknowledged");

           _errors = Metrics.CreateCounter(
               "vibemq_errors_total",
               "Total number of errors");

           _activeConnections = Metrics.CreateGauge(
               "vibemq_active_connections",
               "Number of active connections");

           _activeQueues = Metrics.CreateGauge(
               "vibemq_active_queues",
               "Number of active queues");

           _inFlightMessages = Metrics.CreateGauge(
               "vibemq_in_flight_messages",
               "Number of messages in flight");

           _memoryUsage = Metrics.CreateGauge(
               "vibemq_memory_usage_bytes",
               "Memory usage in bytes");

           _deliveryLatency = Metrics.CreateHistogram(
               "vibemq_delivery_latency_seconds",
               "Delivery latency histogram",
               new HistogramConfiguration {
                   Buckets = new[] { 0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1.0 }
               });
       }

       protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
           long lastPublished = 0;
           long lastDelivered = 0;
           long lastAcknowledged = 0;
           long lastErrors = 0;

           while (!stoppingToken.IsCancellationRequested) {
               await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

               var snapshot = _metrics.GetSnapshot();

               // Update counters (only increments)
               if (snapshot.TotalMessagesPublished > lastPublished) {
                   _messagesPublished.Inc(snapshot.TotalMessagesPublished - lastPublished);
                   lastPublished = snapshot.TotalMessagesPublished;
               }

               if (snapshot.TotalMessagesDelivered > lastDelivered) {
                   _messagesDelivered.Inc(snapshot.TotalMessagesDelivered - lastDelivered);
                   lastDelivered = snapshot.TotalMessagesDelivered;
               }

               if (snapshot.TotalMessagesAcknowledged > lastAcknowledged) {
                   _messagesAcknowledged.Inc(snapshot.TotalMessagesAcknowledged - lastAcknowledged);
                   lastAcknowledged = snapshot.TotalMessagesAcknowledged;
               }

               if (snapshot.TotalErrors > lastErrors) {
                   _errors.Inc(snapshot.TotalErrors - lastErrors);
                   lastErrors = snapshot.TotalErrors;
               }

               // Update gauges
               _activeConnections.Set(snapshot.ActiveConnections);
               _activeQueues.Set(snapshot.ActiveQueues);
               _inFlightMessages.Set(snapshot.InFlightMessages);
               _memoryUsage.Set(snapshot.MemoryUsageBytes);
               _deliveryLatency.Observe(snapshot.AverageDeliveryLatencyMs / 1000.0);
           }
       }
   }

Регистрация:

.. code-block:: csharp

   services.AddHostedService<VibeMQMetricsExporter>();

   // Prometheus server
   var server = new MetricServer(port: 9090);
   server.Start();

Grafana дашборд
---------------

Пример JSON-дашборда для Grafana:

.. code-block:: json

   {
     "dashboard": {
       "title": "VibeMQ Monitor",
       "panels": [
         {
           "title": "Messages Published/Delivered",
           "targets": [
             {
               "expr": "rate(vibemq_messages_published_total[5m])",
               "legendFormat": "Published"
             },
             {
               "expr": "rate(vibemq_messages_delivered_total[5m])",
               "legendFormat": "Delivered"
             }
           ]
         },
         {
           "title": "Active Connections",
           "targets": [
             {
               "expr": "vibemq_active_connections",
               "legendFormat": "Connections"
             }
           ]
         },
         {
           "title": "Delivery Latency",
           "targets": [
             {
               "expr": "vibemq_delivery_latency_seconds",
               "legendFormat": "Latency (p50)"
             }
           ]
         },
         {
           "title": "Memory Usage",
           "targets": [
             {
               "expr": "vibemq_memory_usage_bytes",
               "legendFormat": "Memory"
             }
           ]
         }
       ]
     }
   }

Логирование
===========

Настройка логирования
---------------------

.. code-block:: csharp

   using Microsoft.Extensions.Logging;

   using var loggerFactory = LoggerFactory.Create(builder => {
       builder
           .SetMinimumLevel(LogLevel.Information)
           .AddConsole()
           .AddDebug()
           .AddFile("logs/vibemq-.log");
   });

   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .UseLoggerFactory(loggerFactory)
       .Build();

Уровни логирования
------------------

**Trace:**

- Детальная информация о каждом сообщении
- Для отладки протокола

**Debug:**

- Информация о подключениях
- Статус операций

**Information:**

- Запуск/остановка сервера
- Создание/удаление очередей
- Ошибки аутентификации

**Warning:**

- Превышение лимитов
- Проблемы с доставкой
- Высокое использование памяти

**Error:**

- Ошибки обработки сообщений
- Ошибки подключения
- Исключения в обработчиках

**Critical:**

- Критические ошибки сервера
- Невозможность запуска
- Потеря данных

Примеры логов
-------------

**Запуск сервера:**

.. code-block:: text

   [10:30:00 INF] VibeMQ server starting on port 8080
   [10:30:00 INF] Health check server started on port 8081
   [10:30:00 INF] Server ready to accept connections

**Подключение клиента:**

.. code-block:: text

   [10:31:00 INF] Client connected from 192.168.1.100:54321
   [10:31:00 INF] Client authenticated successfully
   [10:31:00 DBG] Connection ID: srv_100

**Публикация сообщения:**

.. code-block:: text

   [10:32:00 DBG] Message msg_123 published to queue notifications
   [10:32:00 DBG] Message delivered to subscriber sub_456
   [10:32:01 DBG] Message msg_123 acknowledged

**Предупреждения:**

.. code-block:: text

   [10:33:00 WRN] Memory usage high: 85%
   [10:33:00 WRN] Backpressure applied for queue notifications
   [10:33:00 WRN] Rate limit exceeded for client srv_100

**Ошибки:**

.. code-block:: text

   [10:34:00 ERR] Failed to deliver message msg_123: timeout
   [10:34:00 ERR] Authentication failed for client 192.168.1.100
   [10:34:00 ERR] Queue notifications not found

Алертинг
========

Примеры правил для Prometheus Alertmanager:

.. code-block:: yaml

   groups:
     - name: vibemq
       rules:
         - alert: VibeMQHighMemory
           expr: vibemq_memory_usage_bytes / 1073741824 > 0.9
           for: 5m
           labels:
             severity: warning
           annotations:
             summary: "VibeMQ high memory usage"
             description: "Memory usage is above 90% for more than 5 minutes"

         - alert: VibeMQHighLatency
           expr: histogram_quantile(0.95, vibemq_delivery_latency_seconds_bucket) > 0.05
           for: 5m
           labels:
             severity: warning
           annotations:
             summary: "VibeMQ high delivery latency"
             description: "95th percentile latency is above 50ms"

         - alert: VibeMQHighErrorRate
           expr: rate(vibemq_errors_total[5m]) > 0.1
           for: 2m
           labels:
             severity: critical
           annotations:
             summary: "VibeMQ high error rate"
             description: "Error rate is above 0.1 per second"

         - alert: VibeMQDeadLetterQueueGrowing
           expr: rate(vibemq_messages_dead_lettered_total[5m]) > 0
           for: 10m
           labels:
             severity: warning
           annotations:
             summary: "VibeMQ DLQ growing"
             description: "Messages are being dead lettered"

Следующие шаги
==============

- :doc:`server-setup` — настройка сервера
- :doc:`troubleshooting` — устранение проблем
- :doc:`health-checks` — health checks для оркестраторов
