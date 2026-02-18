============
Настройка сервера
============

Это руководство описывает различные способы настройки и запуска сервера VibeMQ.

.. contents:: Содержание
   :local:
   :depth: 2

Базовая настройка
=================

Минимальная конфигурация
------------------------

Самый простой способ запустить сервер:

.. code-block:: csharp

   using VibeMQ.Server;

   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .Build();

   await broker.RunAsync(CancellationToken.None);

Этот код запустит сервер на порту 8080 без аутентификации и с настройками по умолчанию.

Расширенная конфигурация
------------------------

.. code-block:: csharp

   using Microsoft.Extensions.Logging;
   using VibeMQ.Server;
   using VibeMQ.Core.Enums;

   using var loggerFactory = LoggerFactory.Create(builder => {
       builder.SetMinimumLevel(LogLevel.Information).AddConsole();
   });

   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .UseAuthentication("my-secret-token")
       .UseMaxConnections(1000)
       .UseMaxMessageSize(1_048_576)  // 1 MB
       .ConfigureQueues(options => {
           options.DefaultDeliveryMode = DeliveryMode.RoundRobin;
           options.MaxQueueSize = 10_000;
           options.EnableAutoCreate = true;
           options.MessageTtl = TimeSpan.FromHours(24);
           options.EnableDeadLetterQueue = true;
           options.MaxRetryAttempts = 3;
       })
       .ConfigureRateLimiting(options => {
           options.Enabled = true;
           options.MaxConnectionsPerIpPerWindow = 50;
           options.MaxMessagesPerClientPerSecond = 1000;
       })
       .ConfigureHealthChecks(options => {
           options.Enabled = true;
           options.Port = 8081;
       })
       .UseTls(options => {
           options.Enabled = false;  // Включите для production
           options.CertificatePath = "/path/to/cert.pfx";
           options.CertificatePassword = "cert-password";
       })
       .UseLoggerFactory(loggerFactory)
       .Build();

   await broker.RunAsync(CancellationToken.None);

Параметры конфигурации
======================

Основные параметры
------------------

+------------------------+------------------+----------------------------------+
| Параметр               | По умолчанию     | Описание                         |
+========================+==================+==================================+
| ``Port``               | 8080             | TCP порт для подключений клиентов|
+------------------------+------------------+----------------------------------+
| ``MaxConnections``     | 1000             | Максимальное количество подключе-|
|                        |                  | ний                              |
+------------------------+------------------+----------------------------------+
| ``MaxMessageSize``     | 1 MB             | Максимальный размер сообщения    |
+------------------------+------------------+----------------------------------+
| ``EnableAuthentication`` | false          | Включить аутентификацию          |
+------------------------+------------------+----------------------------------+
| ``AuthToken``          | null             | Токен для аутентификации         |
+------------------------+------------------+----------------------------------+

Настройки очередей по умолчанию
-------------------------------

+------------------------+------------------+----------------------------------+
| Параметр               | По умолчанию     | Описание                         |
+========================+==================+==================================+
| ``DefaultDeliveryMode``| RoundRobin       | Режим доставки по умолчанию      |
+------------------------+------------------+----------------------------------+
| ``MaxQueueSize``       | 10,000           | Максимальный размер очереди      |
+------------------------+------------------+----------------------------------+
| ``EnableAutoCreate``   | true             | Автоматическое создание очередей |
+------------------------+------------------+----------------------------------+
| ``MessageTtl``         | null             | Время жизни сообщений (TTL)      |
+------------------------+------------------+----------------------------------+
| ``EnableDeadLetterQueue`` | false         | Включить DLQ                     |
+------------------------+------------------+----------------------------------+
| ``MaxRetryAttempts``   | 3                | Макс. попыток доставки           |
+------------------------+------------------+----------------------------------+
| ``OverflowStrategy``   | DropOldest       | Стратегия переполнения           |
+------------------------+------------------+----------------------------------+

Rate limiting
-------------

+------------------------+------------------+----------------------------------+
| Параметр               | По умолчанию     | Описание                         |
+========================+==================+==================================+
| ``Enabled``            | false            | Включить rate limiting           |
+------------------------+------------------+----------------------------------+
| ``MaxConnectionsPerIpPerWindow`` | 100    | Макс. подключений с IP в окно    |
+------------------------+------------------+----------------------------------+
| ``ConnectionWindowSeconds`` | 60          | Окно времени для подключений (с) |
+------------------------+------------------+----------------------------------+
| ``MaxMessagesPerClientPerSecond`` | 1000  | Макс. сообщений от клиента в сек |
+------------------------+------------------+----------------------------------+

TLS/SSL настройки
-----------------

+------------------------+------------------+----------------------------------+
| Параметр               | По умолчанию     | Описание                         |
+========================+==================+==================================+
| ``Enabled``            | false            | Включить TLS                     |
+------------------------+------------------+----------------------------------+
| ``CertificatePath``    | null             | Путь к PFX-сертификату           |
+------------------------+------------------+----------------------------------+
| ``CertificatePassword``| null             | Пароль к сертификату             |
+------------------------+------------------+----------------------------------+

Health check настройки
----------------------

+------------------------+------------------+----------------------------------+
| Параметр               | По умолчанию     | Описание                         |
+========================+==================+==================================+
| ``Enabled``            | true             | Включить health check сервер     |
+------------------------+------------------+----------------------------------+
| ``Port``               | 8081             | HTTP порт для health checks      |
+------------------------+------------------+----------------------------------+

Режимы доставки
===============

Round-robin
-----------

Каждое сообщение доставляется одному подписчику циклически:

.. code-block:: csharp

   .ConfigureQueues(options => {
       options.DefaultDeliveryMode = DeliveryMode.RoundRobin;
   });

**Использование:**

- Обработка задач несколькими воркерами
- Балансировка нагрузки
- Очереди задач (task queues)

Fan-out с подтверждением
------------------------

Сообщение доставляется всем подписчикам с гарантией доставки:

.. code-block:: csharp

   .ConfigureQueues(options => {
       options.DefaultDeliveryMode = DeliveryMode.FanOutWithAck;
       options.MaxRetryAttempts = 3;
   });

**Использование:**

- Рассылка уведомлений
- Репликация данных
- Аудит и логирование

Fan-out без подтверждения
-------------------------

Сообщение доставляется всем подписчикам без подтверждения:

.. code-block:: csharp

   .ConfigureQueues(options => {
       options.DefaultDeliveryMode = DeliveryMode.FanOutWithoutAck;
   });

**Использование:**

- Broadcas-сообщения
- Обновления в реальном времени
- Стриминг данных

Priority-based
--------------

Доставка по приоритету сообщений:

.. code-block:: csharp

   .ConfigureQueues(options => {
       options.DefaultDeliveryMode = DeliveryMode.PriorityBased;
   });

**Приоритеты:**

- ``Critical`` — критические (доставляются первыми)
- ``High`` — высокие
- ``Normal`` — обычные (по умолчанию)
- ``Low`` — низкие (доставляются последними)

**Пример публикации:**

.. code-block:: csharp

   await client.PublishAsync("alerts", message, options => {
       options.Priority = MessagePriority.Critical;
   });

Стратегии переполнения
======================

DropOldest
----------

Удаление старейшего сообщения при переполнении:

.. code-block:: csharp

   .ConfigureQueues(options => {
       options.OverflowStrategy = OverflowStrategy.DropOldest;
   });

**Когда использовать:**

- Важны свежие данные
- Старые сообщения теряют актуальность

DropNewest
----------

Отклонение нового сообщения:

.. code-block:: csharp

   .ConfigureQueues(options => {
       options.OverflowStrategy = OverflowStrategy.DropNewest;
   });

**Когда использовать:**

- Все существующие сообщения важны
- Новые сообщения могут подождать

BlockPublisher
--------------

Блокировка отправителя до освобождения места:

.. code-block:: csharp

   .ConfigureQueues(options => {
       options.OverflowStrategy = OverflowStrategy.BlockPublisher;
   });

**Когда использовать:**

- Потеря сообщений недопустима
- Издатель может ждать

RedirectToDlq
-------------

Перенаправление в Dead Letter Queue:

.. code-block:: csharp

   .ConfigureQueues(options => {
       options.OverflowStrategy = OverflowStrategy.RedirectToDlq;
       options.EnableDeadLetterQueue = true;
       options.DeadLetterQueueName = "dlq";
   });

**Когда использовать:**

- Требуется сохранность всех сообщений
- Планируется последующая обработка

Dead Letter Queue
=================

Настройка DLQ:

.. code-block:: csharp

   .ConfigureQueues(options => {
       options.EnableDeadLetterQueue = true;
       options.DeadLetterQueueName = "dead-letters";
       options.MaxRetryAttempts = 3;
   });

**Причины попадания в DLQ:**

- Превышено максимальное количество попыток доставки
- Истёк TTL сообщения
- Ошибка десериализации
- Исключение в обработчике

**Получение сообщений из DLQ:**

.. code-block:: csharp

   var dlqMessages = await queueManager.GetDeadLetterMessagesAsync(100);
   
   foreach (var message in dlqMessages) {
       // Обработка неудачного сообщения
   }

Аутентификация
==============

Включение аутентификации:

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

   Используйте сложные токены в production (минимум 32 символа).
   Храните токены в защищённом месте (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault).

TLS/SSL шифрование
==================

Настройка TLS:

.. code-block:: csharp

   .UseTls(options => {
       options.Enabled = true;
       options.CertificatePath = "/path/to/cert.pfx";
       options.CertificatePassword = "cert-password";
   })

**Создание самоподписанного сертификата (для тестов):**

.. code-block:: bash

   dotnet dev-certs https -ep vibemq.pfx -p cert-password
   dotnet dev-certs https --trust

**На клиенте:**

.. code-block:: csharp

   var client = await VibeMQClient.ConnectAsync(
       "localhost",
       8080,
       new ClientOptions {
           UseTls = true,
           // Только для тестов!
           SkipCertificateValidation = true
       }
   );

.. warning::

   Не используйте ``SkipCertificateValidation = true`` в production!

Запуск и остановка
==================

Запуск сервера
--------------

**Асинхронный запуск:**

.. code-block:: csharp

   await broker.RunAsync(cancellationToken);

**Запуск с обработкой сигналов:**

.. code-block:: csharp

   var cts = new CancellationTokenSource();
   Console.CancelKeyPress += (_, e) => {
       e.Cancel = true;
       cts.Cancel();
   };

   await broker.RunAsync(cts.Token);

Остановка сервера
-----------------

**Корректная остановка:**

.. code-block:: csharp

   await broker.StopAsync(cancellationToken);

**Освобождение ресурсов:**

.. code-block:: csharp

   await broker.DisposeAsync();

**Использование using:**

.. code-block:: csharp

   await using var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .Build();

   await broker.RunAsync(cancellationToken);
   // DisposeAsync вызывается автоматически

Логирование
===========

Настройка логирования:

.. code-block:: csharp

   using Microsoft.Extensions.Logging;

   using var loggerFactory = LoggerFactory.Create(builder => {
       builder
           .SetMinimumLevel(LogLevel.Debug)
           .AddConsole()
           .AddDebug()
           .AddFile("logs/vibemq-.log");  // Требуется пакет Microsoft.Extensions.Logging.File
   });

   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .UseLoggerFactory(loggerFactory)
       .Build();

**Уровни логирования:**

- ``Trace`` — детальная отладка
- ``Debug`` — отладочная информация
- ``Information`` — информационные сообщения
- ``Warning`` — предупреждения
- ``Error`` — ошибки
- ``Critical`` — критические ошибки

Примеры конфигурации
====================

Минимальный сервер для разработки
---------------------------------

.. code-block:: csharp

   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .Build();

   await broker.RunAsync(CancellationToken.None);

Сервер для production
---------------------

.. code-block:: csharp

   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .UseAuthentication(Environment.GetEnvironmentVariable("VIBEMQ_TOKEN"))
       .UseMaxConnections(5000)
       .ConfigureQueues(options => {
           options.DefaultDeliveryMode = DeliveryMode.RoundRobin;
           options.MaxQueueSize = 100_000;
           options.EnableDeadLetterQueue = true;
           options.MaxRetryAttempts = 5;
       })
       .ConfigureRateLimiting(options => {
           options.Enabled = true;
           options.MaxConnectionsPerIpPerWindow = 100;
           options.MaxMessagesPerClientPerSecond = 5000;
       })
       .UseTls(options => {
           options.Enabled = true;
           options.CertificatePath = "/etc/ssl/vibemq.pfx";
           options.CertificatePassword = Environment.GetEnvironmentVariable("CERT_PASSWORD");
       })
       .UseLoggerFactory(loggerFactory)
       .Build();

Сервер для микросервисов
------------------------

.. code-block:: csharp

   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .UseAuthentication("microservice-token")
       .ConfigureQueues(options => {
           options.DefaultDeliveryMode = DeliveryMode.FanOutWithAck;
           options.EnableAutoCreate = true;
           options.MessageTtl = TimeSpan.FromMinutes(30);
       })
       .ConfigureHealthChecks(options => {
           options.Enabled = true;
           options.Port = 8081;
       })
       .Build();

Сервер для IoT
--------------

.. code-block:: csharp

   var broker = BrokerBuilder.Create()
       .UsePort(8883)  // Стандартный порт MQTT
       .UseAuthentication("iot-token")
       .UseMaxConnections(10000)
       .ConfigureQueues(options => {
           options.DefaultDeliveryMode = DeliveryMode.RoundRobin;
           options.MaxQueueSize = 1000;  // Маленький размер для экономии памяти
           options.MessageTtl = TimeSpan.FromSeconds(60);  // Короткий TTL
       })
       .ConfigureRateLimiting(options => {
           options.Enabled = true;
           options.MaxConnectionsPerIpPerWindow = 500;
           options.MaxMessagesPerClientPerSecond = 100;  // Ограничение для устройств
       })
       .Build();

Следующие шаги
==============

- :doc:`client-usage` — использование клиента
- :doc:`configuration` — детальная конфигурация
- :doc:`monitoring` — мониторинг и health checks
- :doc:`di-integration` — интеграция с DI
