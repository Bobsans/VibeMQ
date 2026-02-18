=================
Использование клиента
=================

Это руководство описывает различные способы использования клиента VibeMQ.

.. contents:: Содержание
   :local:
   :depth: 2

Подключение к серверу
=====================

Базовое подключение
-------------------

.. code-block:: csharp

   using VibeMQ.Client;

   await using var client = await VibeMQClient.ConnectAsync(
       "localhost",
       8080
   );

   Console.WriteLine($"Подключено: {client.IsConnected}");

Подключение с аутентификацией
-----------------------------

.. code-block:: csharp

   var client = await VibeMQClient.ConnectAsync(
       "localhost",
       8080,
       new ClientOptions {
           AuthToken = "my-secret-token"
       }
   );

Подключение с логированием
--------------------------

.. code-block:: csharp

   using Microsoft.Extensions.Logging;

   using var loggerFactory = LoggerFactory.Create(builder => {
       builder.SetMinimumLevel(LogLevel.Information).AddConsole();
   });

   var logger = loggerFactory.CreateLogger<VibeMQClient>();

   var client = await VibeMQClient.ConnectAsync(
       "localhost",
       8080,
       new ClientOptions { AuthToken = "my-token" },
       logger
   );

Публикация сообщений
====================

Базовая публикация
------------------

.. code-block:: csharp

   await client.PublishAsync("notifications", new {
       Title = "Привет",
       Body = "Это тестовое сообщение",
       Timestamp = DateTime.Now
   });

Публикация с приоритетом
------------------------

.. code-block:: csharp

   using VibeMQ.Core.Enums;

   // Критическое сообщение
   await client.PublishAsync("alerts", alertData, options => {
       options.Priority = MessagePriority.Critical;
   });

   // Высокий приоритет
   await client.PublishAsync("orders", orderData, options => {
       options.Priority = MessagePriority.High;
   });

Публикация с заголовками
------------------------

.. code-block:: csharp

   await client.PublishAsync("orders", orderData, options => {
       options.Headers = new Dictionary<string, string> {
           ["correlationId"] = Guid.NewGuid().ToString(),
           ["source"] = "order-service",
           ["version"] = "1.0"
       };
   });

Типизированная публикация
-------------------------

Создайте класс для сообщения:

.. code-block:: csharp

   public class OrderCreated {
       public string OrderId { get; set; }
       public decimal Amount { get; set; }
       public DateTime CreatedAt { get; set; }
   }

Используйте его:

.. code-block:: csharp

   await client.PublishAsync("orders.created", new OrderCreated {
       OrderId = "ORD-123",
       Amount = 99.99m,
       CreatedAt = DateTime.UtcNow
   });

Подписка на сообщения
=====================

Базовая подписка
----------------

.. code-block:: csharp

   await using var subscription = await client.SubscribeAsync<dynamic>(
       "notifications",
       async msg => {
           Console.WriteLine($"Получено: {msg.Title} - {msg.Body}");
       }
   );

Типизированная подписка
-----------------------

.. code-block:: csharp

   public class Notification {
       public string Title { get; set; }
       public string Body { get; set; }
   }

   await using var subscription = await client.SubscribeAsync<Notification>(
       "notifications",
       async notification => {
           Console.WriteLine($"{notification.Title}: {notification.Body}");
           await ProcessNotificationAsync(notification);
       }
   );

Подписка с обработкой ошибок
----------------------------

.. code-block:: csharp

   await using var subscription = await client.SubscribeAsync<Notification>(
       "notifications",
       async notification => {
           try {
               await ProcessNotificationAsync(notification);
           } catch (Exception ex) {
               Console.WriteLine($"Ошибка обработки: {ex.Message}");
               throw;  // Брокер попытается доставить сообщение снова
           }
       }
   );

Множественные подписки
----------------------

.. code-block:: csharp

   var subscriptions = new List<IAsyncDisposable>();

   // Подписка на несколько очередей
   subscriptions.Add(await client.SubscribeAsync<Order>(
       "orders.created",
       async order => await HandleOrderAsync(order)
   ));

   subscriptions.Add(await client.SubscribeAsync<Payment>(
       "payments.completed",
       async payment => await HandlePaymentAsync(payment)
   ));

   subscriptions.Add(await client.SubscribeAsync<Notification>(
       "notifications",
       async notification => await ShowNotificationAsync(notification)
   ));

   // Освобождение ресурсов
   foreach (var subscription in subscriptions) {
       await subscription.DisposeAsync();
   }

Отписка от очереди
==================

Автоматическая отписка
----------------------

При использовании ``await using`` отписка происходит автоматически:

.. code-block:: csharp

   await using var subscription = await client.SubscribeAsync<dynamic>(
       "notifications",
       async msg => { /* обработка */ }
   );
   // DisposeAsync() вызывается автоматически

Ручная отписка
--------------

.. code-block:: csharp

   var subscription = await client.SubscribeAsync<dynamic>(
       "notifications",
       async msg => { /* обработка */ }
   );

   // Когда нужно отписаться
   await subscription.DisposeAsync();

Или через метод клиента:

.. code-block:: csharp

   await client.UnsubscribeAsync("notifications");

Управление очередями
====================

Создание очереди
----------------

.. code-block:: csharp

   using VibeMQ.Core.Enums;

   await client.CreateQueueAsync("my-queue", new QueueOptions {
       DeliveryMode = DeliveryMode.RoundRobin,
       MaxQueueSize = 10_000,
       MessageTtl = TimeSpan.FromHours(1),
       EnableDeadLetterQueue = true,
       MaxRetryAttempts = 3
   });

Удаление очереди
----------------

.. code-block:: csharp

   await client.DeleteQueueAsync("my-queue");

Получение информации об очереди
-------------------------------

.. code-block:: csharp

   var info = await client.GetQueueInfoAsync("my-queue");
   
   if (info != null) {
       Console.WriteLine($"Очередь: {info.Name}");
       Console.WriteLine($"Сообщений: {info.MessageCount}");
       Console.WriteLine($"Подписчиков: {info.SubscriberCount}");
       Console.WriteLine($"Режим доставки: {info.DeliveryMode}");
   }

Список очередей
---------------

.. code-block:: csharp

   var queues = await client.ListQueuesAsync();
   
   foreach (var queueName in queues) {
       Console.WriteLine(queueName);
   }

Настройки клиента
=================

ClientOptions
-------------

.. code-block:: csharp

   var options = new ClientOptions {
       // Аутентификация
       AuthToken = "my-secret-token",
       
       // Keep-alive
       KeepAliveInterval = TimeSpan.FromSeconds(30),
       
       // Таймаут для команд
       CommandTimeout = TimeSpan.FromSeconds(10),
       
       // TLS
       UseTls = false,
       SkipCertificateValidation = false,
       
       // Политика реконнекта
       ReconnectPolicy = new ReconnectPolicy {
           MaxAttempts = 10,
           InitialDelay = TimeSpan.FromSeconds(1),
           MaxDelay = TimeSpan.FromMinutes(5),
           UseExponentialBackoff = true
       }
   };

ReconnectPolicy
---------------

Настройка политики переподключения:

.. code-block:: csharp

   ReconnectPolicy = new ReconnectPolicy {
       MaxAttempts = int.MaxValue,      // Макс. попыток
       InitialDelay = TimeSpan.FromSeconds(1),  // Начальная задержка
       MaxDelay = TimeSpan.FromMinutes(5),      // Максимальная задержка
       UseExponentialBackoff = true     // Экспоненциальное увеличение
   }

**Как работает:**

- Попытка 1: немедленно
- Попытка 2: через 1с
- Попытка 3: через 2с
- Попытка 4: через 4с
- Попытка 5: через 8с
- ...
- Попытка N: через 5мин (максимум)

TLS/SSL подключение
==================

Подключение с TLS:

.. code-block:: csharp

   var client = await VibeMQClient.ConnectAsync(
       "localhost",
       8080,
       new ClientOptions {
           UseTls = true,
           AuthToken = "my-token"
       }
   );

.. warning::

   Для production используйте валидные сертификаты.
   ``SkipCertificateValidation = true`` только для тестов!

Обработка отключений
====================

Автоматический реконнект
------------------------

Клиент автоматически переподключается при разрыве:

.. code-block:: csharp

   var client = await VibeMQClient.ConnectAsync(
       "localhost",
       8080,
       new ClientOptions {
           ReconnectPolicy = new ReconnectPolicy {
               MaxAttempts = 10,
               UseExponentialBackoff = true
           }
       }
   );

   // Подписка восстановится после реконнекта
   await using var subscription = await client.SubscribeAsync<dynamic>(
       "notifications",
       async msg => { /* обработка */ }
   );

Проверка состояния
------------------

.. code-block:: csharp

   if (client.IsConnected) {
       await client.PublishAsync("queue", data);
   } else {
       Console.WriteLine("Клиент отключён");
   }

События (опционально)
---------------------

Для отслеживания состояния можно использовать периодическую проверку:

.. code-block:: csharp

   _ = Task.Run(async () => {
       while (true) {
           await Task.Delay(5000);
           Console.WriteLine($"Статус: {(client.IsConnected ? "Подключено" : "Отключено")}");
       }
   });

Отключение
==========

Корректное отключение
---------------------

.. code-block:: csharp

   await client.DisconnectAsync();

Использование using
-------------------

.. code-block:: csharp

   await using var client = await VibeMQClient.ConnectAsync("localhost", 8080);
   
   // Работа с клиентом
   
   // DisposeAsync() вызывается автоматически
   await client.DisconnectAsync();

Полное освобождение ресурсов:

.. code-block:: csharp

   await client.DisposeAsync();

Примеры использования
=====================

Простой издатель
----------------

.. code-block:: csharp

   using VibeMQ.Client;

   await using var publisher = await VibeMQClient.ConnectAsync("localhost", 8080, new ClientOptions {
       AuthToken = "my-token"
   });

   Console.WriteLine("Издатель подключён. Введите сообщение (Enter для выхода):");

   while (true) {
       var input = Console.ReadLine();
       if (string.IsNullOrWhiteSpace(input)) break;

       await publisher.PublishAsync("messages", new {
           Text = input,
           Timestamp = DateTime.Now
       });

       Console.WriteLine("✓ Сообщение отправлено");
   }

Простой подписчик
-----------------

.. code-block:: csharp

   using VibeMQ.Client;

   await using var subscriber = await VibeMQClient.ConnectAsync("localhost", 8080, new ClientOptions {
       AuthToken = "my-token"
   });

   await using var subscription = await subscriber.SubscribeAsync<dynamic>(
       "messages",
       async msg => {
           Console.WriteLine($"📨 {msg.Text} (в {msg.Timestamp})");
       }
   );

   Console.WriteLine("Подписчик запущен. Нажмите Enter для выхода...");
   Console.ReadLine();

Воркер для обработки задач
--------------------------

.. code-block:: csharp

   using VibeMQ.Client;

   public class OrderProcessor {
       private readonly VibeMQClient _client;

       public OrderProcessor(VibeMQClient client) {
           _client = client;
       }

       public async Task StartAsync(CancellationToken cancellationToken) {
           await using var subscription = await _client.SubscribeAsync<Order>(
               "orders.process",
               async order => {
                   try {
                       await ProcessOrderAsync(order);
                       Console.WriteLine($"✓ Заказ {order.Id} обработан");
                   } catch (Exception ex) {
                       Console.WriteLine($"✗ Ошибка: {ex.Message}");
                       throw;  // Для повторной попытки
                   }
               },
               cancellationToken
           );

           await Task.Delay(Timeout.Infinite, cancellationToken);
       }

       private Task ProcessOrderAsync(Order order) {
           // Обработка заказа
           return Task.CompletedTask;
       }
   }

   public class Order {
       public string Id { get; set; }
       public decimal Amount { get; set; }
       public string Customer { get; set; }
   }

Шина событий для микросервисов
------------------------------

.. code-block:: csharp

   using VibeMQ.Client;

   public class EventBus {
       private readonly VibeMQClient _client;
       private readonly ILogger<EventBus> _logger;

       public EventBus(VibeMQClient client, ILogger<EventBus> logger) {
           _client = client;
           _logger = logger;
       }

       public async Task PublishAsync<T>(string eventType, T eventData) {
           await _client.PublishAsync($"events.{eventType}", eventData, options => {
               options.Headers = new Dictionary<string, string> {
                   ["event_type"] = eventType,
                   ["timestamp"] = DateTime.UtcNow.ToString("O")
               };
           });

           _logger.LogInformation("Событие {EventType} опубликовано", eventType);
       }

       public async Task SubscribeAsync<T>(string eventType, Func<T, Task> handler) {
           await _client.SubscribeAsync<T>(
               $"events.{eventType}",
               async eventData => {
                   _logger.LogInformation("Получено событие {EventType}", eventType);
                   await handler(eventData);
               }
           );
       }
   }

Устранение проблем
==================

Ошибка подключения
------------------

**Ошибка:** ``Connection refused``

**Причины:**

- Сервер не запущен
- Неверный порт
- Брандмауэр блокирует подключение

**Решение:**

.. code-block:: csharp

   // Проверьте параметры подключения
   var client = await VibeMQClient.ConnectAsync(
       "localhost",  // Или правильный хост
       8080,         // Или правильный порт
       new ClientOptions { ... }
   );

Ошибка аутентификации
---------------------

**Ошибка:** ``Authentication failed``

**Решение:** Убедитесь, что токены совпадают:

.. code-block:: csharp

   // Сервер
   .UseAuthentication("my-token")

   // Клиент
   new ClientOptions { AuthToken = "my-token" }

Таймаут подключения
-------------------

**Ошибка:** ``Connection timeout``

**Решение:** Увеличьте таймаут:

.. code-block:: csharp

   new ClientOptions {
       CommandTimeout = TimeSpan.FromSeconds(30)
   }

Частые отключения
-----------------

**Причина:** Проблемы с сетью или сервером

**Решение:** Настройте политику реконнекта:

.. code-block:: csharp

   new ClientOptions {
       ReconnectPolicy = new ReconnectPolicy {
           MaxAttempts = 50,  // Увеличьте количество попыток
           InitialDelay = TimeSpan.FromSeconds(2),
           MaxDelay = TimeSpan.FromMinutes(1)
       }
   }

Следующие шаги
==============

- :doc:`server-setup` — настройка сервера
- :doc:`configuration` — параметры конфигурации
- :doc:`di-integration` — интеграция с DI
