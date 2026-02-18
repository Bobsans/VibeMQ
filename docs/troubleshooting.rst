===============
Устранение проблем
===============

Это руководство поможет решить распространённые проблемы при использовании VibeMQ.

.. contents:: Содержание
   :local:
   :depth: 2

Проблемы подключения
====================

«Connection refused»
--------------------

**Ошибка:**

.. code-block:: text

   System.Net.Sockets.SocketException: Connection refused

**Причины:**

1. Сервер не запущен
2. Неверный порт
3. Брандмауэр блокирует подключение
4. Сервер слушает только localhost

**Решение:**

.. code-block:: csharp

   // Проверьте, что сервер запущен
   var broker = BrokerBuilder.Create()
       .UsePort(8080)  // Правильный порт
       .Build();
   
   await broker.RunAsync(cancellationToken);

   // Проверьте, что клиент подключается к правильному порту
   var client = await VibeMQClient.ConnectAsync("localhost", 8080);

**Проверка:**

.. code-block:: bash

   # Проверка, что порт слушается
   netstat -an | grep 8080

   # PowerShell
   Get-NetTCPConnection -LocalPort 8080

«Connection timeout»
--------------------

**Ошибка:**

.. code-block:: text

   System.TimeoutException: Connection timeout

**Причины:**

1. Сетевая задержка
2. Сервер перегружен
3. Неправильный таймаут

**Решение:**

.. code-block:: csharp

   var client = await VibeMQClient.ConnectAsync(
       "localhost",
       8080,
       new ClientOptions {
           CommandTimeout = TimeSpan.FromSeconds(30)  // Увеличьте таймаут
       }
   );

«Host unreachable»
------------------

**Ошибка:**

.. code-block:: text

   System.Net.Sockets.SocketException: Host unreachable

**Причины:**

1. Неправильный хост
2. Сетевые проблемы
3. DNS не разрешается

**Решение:**

.. code-block:: csharp

   // Проверьте хост
   var client = await VibeMQClient.ConnectAsync(
       "vibemq-server",  // Правильное имя хоста
       8080
   );

**Проверка DNS:**

.. code-block:: bash

   nslookup vibemq-server
   ping vibemq-server

Проблемы аутентификации
=======================

«Authentication failed»
-----------------------

**Ошибка:**

.. code-block:: text

   Authentication failed: Invalid token

**Причина:** Токены на сервере и клиенте не совпадают.

**Решение:**

.. code-block:: csharp

   // Сервер
   var broker = BrokerBuilder.Create()
       .UseAuthentication("my-secret-token")  // Одинаковый токен
       .Build();

   // Клиент
   var client = await VibeMQClient.ConnectAsync(
       "localhost",
       8080,
       new ClientOptions {
           AuthToken = "my-secret-token"  // Тот же токен
       }
   );

.. warning::

   Токены чувствительны к регистру!

«Authentication required»
-------------------------

**Ошибка:**

.. code-block:: text

   Authentication required: Token not provided

**Причина:** Сервер требует аутентификацию, но клиент не предоставил токен.

**Решение:**

.. code-block:: csharp

   var client = await VibeMQClient.ConnectAsync(
       "localhost",
       8080,
       new ClientOptions {
           AuthToken = "my-token"  // Предоставьте токен
       }
   );

Проблемы с очередями
====================

«Queue not found»
-----------------

**Ошибка:**

.. code-block:: text

   Queue not found: notifications

**Причины:**

1. Очередь не существует
2. Отключено авто-создание

**Решение 1:** Включите авто-создание:

.. code-block:: csharp

   .ConfigureQueues(options => {
       options.EnableAutoCreate = true;
   });

**Решение 2:** Создайте очередь вручную:

.. code-block:: csharp

   await queueManager.CreateQueueAsync("notifications", new QueueOptions {
       DeliveryMode = DeliveryMode.RoundRobin,
       MaxQueueSize = 10_000
   });

«Queue already exists»
----------------------

**Ошибка:**

.. code-block:: text

   Queue already exists: notifications

**Причина:** Попытка создать существующую очередь.

**Решение:**

.. code-block:: csharp

   try {
       await queueManager.CreateQueueAsync("notifications");
   } catch (QueueAlreadyExistsException) {
       // Очередь уже существует, это нормально
   }

Проблемы с доставкой сообщений
==============================

Сообщения не доставляются
-------------------------

**Проблема:** Сообщения публикуются, но подписчики не получают.

**Возможные причины:**

1. Подписчик не подписан на очередь
2. Неправильное имя очереди
3. Проблемы с сетью

**Решение:**

.. code-block:: csharp

   // Проверьте имя очереди
   await client.PublishAsync("notifications", message);  // Одинаковое имя

   await using var subscription = await client.SubscribeAsync<dynamic>(
       "notifications",  // То же имя
       async msg => { /* обработка */ }
   );

**Проверка:**

.. code-block:: csharp

   var info = await queueManager.GetQueueInfoAsync("notifications");
   Console.WriteLine($"Подписчиков: {info.SubscriberCount}");

Сообщения доставляются медленно
-------------------------------

**Проблема:** Высокая латентность доставки.

**Причины:**

1. Перегрузка сервера
2. Медленные обработчики
3. Сетевые задержки

**Решение:**

.. code-block:: csharp

   // Оптимизируйте обработчик
   await using var subscription = await client.SubscribeAsync<dynamic>(
       "notifications",
       async msg => {
           // Быстрая обработка
           await ProcessFastAsync(msg);
           
           // Или асинхронная обработка в фоне
           _ = Task.Run(() => ProcessInBackgroundAsync(msg));
       }
   );

**Мониторинг латентности:**

.. code-block:: bash

   curl http://localhost:8081/metrics/ | jq .average_delivery_latency_ms

«Message timeout»
-----------------

**Ошибка:**

.. code-block:: text

   Message timeout: No ACK received

**Причина:** Подписчик не отправил ACK в течение таймаута.

**Решение:**

.. code-block:: csharp

   // Увеличьте таймаут на сервере
   .ConfigureQueues(options => {
       options.MessageTtl = TimeSpan.FromMinutes(1);  // Увеличьте TTL
   });

   // Убедитесь, что обработчик быстрый
   await using var subscription = await client.SubscribeAsync<dynamic>(
       "notifications",
       async msg => {
           try {
               await ProcessMessageAsync(msg);
               // ACK отправляется автоматически
           } catch (Exception ex) {
               // Обработка ошибки
               throw;  // Для повторной попытки
           }
       }
   );

Проблемы с памятью
==================

«Out of memory»
---------------

**Ошибка:**

.. code-block:: text

   System.OutOfMemoryException

**Причины:**

1. Слишком большие очереди
2. Много неподтверждённых сообщений
3. Утечки памяти

**Решение:**

.. code-block:: csharp

   // Ограничьте размер очередей
   .ConfigureQueues(options => {
       options.MaxQueueSize = 10_000;  // Ограничьте размер
       options.MessageTtl = TimeSpan.FromHours(1);  // Установите TTL
   });

   // Включите Dead Letter Queue
   options.EnableDeadLetterQueue = true;
   options.MaxRetryAttempts = 3;

**Мониторинг памяти:**

.. code-block:: bash

   curl http://localhost:8081/health/ | jq .memory_usage_mb

Backpressure
------------

**Проблема:** Публикации блокируются.

**Причина:** Включён backpressure из-за высокого использования памяти.

**Решение:**

.. code-block:: csharp

   // Увеличьте лимит памяти или уменьшите нагрузку
   .ConfigureQueues(options => {
       options.MaxQueueSize = 5_000;  // Уменьшите размер
       options.OverflowStrategy = OverflowStrategy.DropOldest;  // Стратегия
   });

Проблемы с реконнектом
======================

Частые отключения
-----------------

**Проблема:** Клиент часто отключается.

**Причины:**

1. Проблемы с сетью
2. Сервер перезапускается
3. Таймаут keep-alive

**Решение:**

.. code-block:: csharp

   var client = await VibeMQClient.ConnectAsync(
       "localhost",
       8080,
       new ClientOptions {
           ReconnectPolicy = new ReconnectPolicy {
               MaxAttempts = 50,  // Увеличьте попытки
               InitialDelay = TimeSpan.FromSeconds(2),
               MaxDelay = TimeSpan.FromMinutes(1),
               UseExponentialBackoff = true
           },
           KeepAliveInterval = TimeSpan.FromSeconds(30)  // Keep-alive
       }
   );

«Max reconnect attempts exceeded»
---------------------------------

**Ошибка:**

.. code-block:: text

   Max reconnect attempts exceeded

**Причина:** Исчерпаны попытки переподключения.

**Решение:**

.. code-block:: csharp

   // Увеличьте попытки или обработайте ошибку
   try {
       var client = await VibeMQClient.ConnectAsync(
           "localhost",
           8080,
           new ClientOptions {
               ReconnectPolicy = new ReconnectPolicy {
                   MaxAttempts = 100  // Увеличьте
               }
           }
       );
   } catch (MaxReconnectAttemptsExceededException) {
       // Обработайте ошибку
       Console.WriteLine("Не удалось подключиться. Проверьте сервер.");
   }

Проблемы с TLS/SSL
==================

«Certificate validation failed»
-------------------------------

**Ошибка:**

.. code-block:: text

   The remote certificate is invalid according to the validation procedure

**Причины:**

1. Самоподписанный сертификат
2. Истёк срок действия
3. Неправильное имя хоста

**Решение (только для тестов):**

.. code-block:: csharp

   var client = await VibeMQClient.ConnectAsync(
       "localhost",
       8080,
       new ClientOptions {
           UseTls = true,
           SkipCertificateValidation = true  // Только для тестов!
       }
   );

**Решение (для production):**

.. code-block:: bash

   # Создайте валидный сертификат
   openssl req -x509 -newkey rsa:4096 -keyout key.pem -out cert.pem -days 365

.. warning::

   Не используйте ``SkipCertificateValidation = true`` в production!

«TLS handshake failed»
----------------------

**Ошибка:**

.. code-block:: text

   TLS handshake failed

**Причины:**

1. Сервер не настроен на TLS
2. Неправильный сертификат

**Решение:**

.. code-block:: csharp

   // Сервер
   .UseTls(options => {
       options.Enabled = true;
       options.CertificatePath = "/path/to/cert.pfx";
       options.CertificatePassword = "password";
   });

   // Клиент
   new ClientOptions {
       UseTls = true
   };

Проблемы с rate limiting
========================

«Rate limit exceeded»
---------------------

**Ошибка:**

.. code-block:: text

   Rate limit exceeded: Too many messages

**Причина:** Превышен лимит сообщений.

**Решение:**

.. code-block:: csharp

   // Увеличьте лимит на сервере
   .ConfigureRateLimiting(options => {
       options.MaxMessagesPerClientPerSecond = 5000;  // Увеличьте
   });

   // Или уменьшите частоту отправки на клиенте
   await client.PublishAsync("queue", message);
   await Task.Delay(100);  // Пауза между сообщениями

«Too many connections»
----------------------

**Ошибка:**

.. code-block:: text

   Too many connections from this IP

**Причина:** Превышен лимит подключений.

**Решение:**

.. code-block:: csharp

   // Увеличьте лимит на сервере
   .ConfigureRateLimiting(options => {
       options.MaxConnectionsPerIpPerWindow = 500;  // Увеличьте
   });

Диагностика
===========

Включение подробного логирования
--------------------------------

.. code-block:: csharp

   using Microsoft.Extensions.Logging;

   using var loggerFactory = LoggerFactory.Create(builder => {
       builder
           .SetMinimumLevel(LogLevel.Debug)  // Подробное логирование
           .AddConsole()
           .AddDebug();
   });

   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .UseLoggerFactory(loggerFactory)
       .Build();

Проверка здоровья
-----------------

.. code-block:: bash

   # Health check
   curl http://localhost:8081/health/

   # Метрики
   curl http://localhost:8081/metrics/ | jq

Проверка очередей
-----------------

.. code-block:: csharp

   var queues = await queueManager.ListQueuesAsync();
   
   foreach (var queueName in queues) {
       var info = await queueManager.GetQueueInfoAsync(queueName);
       Console.WriteLine($"{queueName}: {info.MessageCount} сообщений, {info.SubscriberCount} подписчиков");
   }

Проверка подключений
--------------------

.. code-block:: csharp

   Console.WriteLine($"Активных подключений: {broker.ActiveConnections}");
   Console.WriteLine($"Сообщений в обработке: {broker.InFlightMessages}");

Следующие шаги
==============

- :doc:`monitoring` — мониторинг
- :doc:`health-checks` — health checks
- :doc:`configuration` — конфигурирование
