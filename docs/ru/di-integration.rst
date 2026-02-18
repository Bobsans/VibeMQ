=====================
Интеграция с DI
=====================

Это руководство описывает интеграцию VibeMQ с Microsoft.Extensions.DependencyInjection.

.. contents:: Содержание
   :local:
   :depth: 2

Обзор
=====

VibeMQ предоставляет пакеты для удобной интеграции с Dependency Injection:

- ``VibeMQ.Server.DependencyInjection`` — для сервера
- ``VibeMQ.Client.DependencyInjection`` — для клиента

Эти пакеты регистрируют необходимые сервисы в DI-контейнере и автоматически управляют жизненным циклом компонентов.

Установка пакетов
=================

.. code-block:: bash

   dotnet add package VibeMQ.Server.DependencyInjection
   dotnet add package VibeMQ.Client.DependencyInjection

Интеграция сервера
==================

Базовая регистрация
-------------------

.. code-block:: csharp

   using VibeMQ.Server.DependencyInjection;

   var host = Host.CreateDefaultBuilder(args)
       .ConfigureServices(services => {
           services.AddVibeMQBroker();
       })
       .Build();

   await host.RunAsync();

Сервер будет запущен автоматически при старте хоста.

Регистрация с конфигурацией
---------------------------

.. code-block:: csharp

   using VibeMQ.Server.DependencyInjection;
   using VibeMQ.Core.Enums;

   var host = Host.CreateDefaultBuilder(args)
       .ConfigureServices(services => {
           services.AddVibeMQBroker(options => {
               options.Port = 8080;
               options.EnableAuthentication = true;
               options.AuthToken = "my-secret-token";
               options.QueueDefaults.DefaultDeliveryMode = DeliveryMode.RoundRobin;
               options.QueueDefaults.MaxQueueSize = 10_000;
               options.QueueDefaults.EnableAutoCreate = true;
           });
       })
       .Build();

   await host.RunAsync();

Конфигурация из appsettings.json
--------------------------------

**appsettings.json:**

.. code-block:: json

   {
     "VibeMQ": {
       "Port": 8080,
       "EnableAuthentication": true,
       "AuthToken": "my-secret-token",
       "QueueDefaults": {
         "DefaultDeliveryMode": "RoundRobin",
         "MaxQueueSize": 10000,
         "EnableAutoCreate": true
       }
     }
   }

**Program.cs:**

.. code-block:: csharp

   using VibeMQ.Server.DependencyInjection;

   var host = Host.CreateDefaultBuilder(args)
       .ConfigureAppConfiguration((context, config) => {
           config.AddJsonFile("appsettings.json");
       })
       .ConfigureServices((context, services) => {
           services.AddVibeMQBroker(
               context.Configuration.GetSection("VibeMQ")
           );
       })
       .Build();

   await host.RunAsync();

Расширенная конфигурация
------------------------

.. code-block:: csharp

   using VibeMQ.Server.DependencyInjection;
   using VibeMQ.Core.Enums;

   var host = Host.CreateDefaultBuilder(args)
       .ConfigureServices(services => {
           services.AddVibeMQBroker(options => {
               // Основные настройки
               options.Port = 8080;
               options.MaxConnections = 5000;
               options.MaxMessageSize = 2_097_152;
               
               // Аутентификация
               options.EnableAuthentication = true;
               options.AuthToken = Environment.GetEnvironmentVariable("VIBEMQ_TOKEN");
               
               // Очереди
               options.QueueDefaults.DefaultDeliveryMode = DeliveryMode.FanOutWithAck;
               options.QueueDefaults.MaxQueueSize = 100_000;
               options.QueueDefaults.EnableDeadLetterQueue = true;
               options.QueueDefaults.MaxRetryAttempts = 5;
               options.QueueDefaults.MessageTtl = TimeSpan.FromHours(24);
               
               // Rate limiting
               options.RateLimit.Enabled = true;
               options.RateLimit.MaxConnectionsPerIpPerWindow = 100;
               options.RateLimit.MaxMessagesPerClientPerSecond = 5000;
               
               // TLS
               options.Tls.Enabled = true;
               options.Tls.CertificatePath = "/etc/ssl/vibemq.pfx";
               options.Tls.CertificatePassword = Environment.GetEnvironmentVariable("CERT_PASSWORD");
           });
           
           // Health checks
           services.AddHealthChecks()
               .AddCheck<VibeMQHealthCheck>("vibemq");
       })
       .Build();

   await host.RunAsync();

Интеграция клиента
==================

Базовая регистрация
-------------------

.. code-block:: csharp

   using VibeMQ.Client.DependencyInjection;

   var host = Host.CreateDefaultBuilder(args)
       .ConfigureServices(services => {
           services.AddVibeMQClient();
       })
       .Build();

   var factory = host.Services.GetRequiredService<IVibeMQClientFactory>();
   await using var client = await factory.CreateAsync();

Регистрация с конфигурацией
---------------------------

.. code-block:: csharp

   using VibeMQ.Client.DependencyInjection;

   var host = Host.CreateDefaultBuilder(args)
       .ConfigureServices(services => {
           services.AddVibeMQClient(settings => {
               settings.Host = "localhost";
               settings.Port = 8080;
               settings.ClientOptions.AuthToken = "my-secret-token";
           });
       })
       .Build();

Конфигурация из appsettings.json
--------------------------------

**appsettings.json:**

.. code-block:: json

   {
     "VibeMQClient": {
       "Host": "localhost",
       "Port": 8080,
       "ClientOptions": {
         "AuthToken": "my-secret-token",
         "KeepAliveInterval": "00:00:30",
         "CommandTimeout": "00:00:10",
         "ReconnectPolicy": {
           "MaxAttempts": 10,
           "InitialDelay": "00:00:01",
           "MaxDelay": "00:05:00",
           "UseExponentialBackoff": true
         }
       }
     }
   }

**Program.cs:**

.. code-block:: csharp

   using VibeMQ.Client.DependencyInjection;

   var host = Host.CreateDefaultBuilder(args)
       .ConfigureAppConfiguration((context, config) => {
           config.AddJsonFile("appsettings.json");
       })
       .ConfigureServices((context, services) => {
           services.AddVibeMQClient(
               context.Configuration.GetSection("VibeMQClient")
           );
       })
       .Build();

Расширенная конфигурация клиента
--------------------------------

.. code-block:: csharp

   using VibeMQ.Client.DependencyInjection;

   var host = Host.CreateDefaultBuilder(args)
       .ConfigureServices(services => {
           services.AddVibeMQClient(settings => {
               settings.Host = "vibemq.internal";
               settings.Port = 8080;
               
               settings.ClientOptions.AuthToken = Environment.GetEnvironmentVariable("VIBEMQ_TOKEN");
               settings.ClientOptions.KeepAliveInterval = TimeSpan.FromSeconds(30);
               settings.ClientOptions.CommandTimeout = TimeSpan.FromSeconds(10);
               
               settings.ClientOptions.ReconnectPolicy = new ReconnectPolicy {
                   MaxAttempts = 10,
                   InitialDelay = TimeSpan.FromSeconds(1),
                   MaxDelay = TimeSpan.FromMinutes(5),
                   UseExponentialBackoff = true
               };
               
               settings.ClientOptions.UseTls = true;
               settings.ClientOptions.SkipCertificateValidation = false;
           });
       })
       .Build();

Использование в сервисах
========================

Публикация сообщений из сервиса
-------------------------------

.. code-block:: csharp

   using VibeMQ.Client.DependencyInjection;

   public class OrderService {
       private readonly IVibeMQClientFactory _clientFactory;
       private readonly ILogger<OrderService> _logger;

       public OrderService(
           IVibeMQClientFactory clientFactory,
           ILogger<OrderService> logger) {
           _clientFactory = clientFactory;
           _logger = logger;
       }

       public async Task CreateOrderAsync(Order order) {
           await using var client = await _clientFactory.CreateAsync();
           
           await client.PublishAsync("orders.created", new {
               OrderId = order.Id,
               Amount = order.Amount,
               CustomerId = order.CustomerId,
               CreatedAt = DateTime.UtcNow
           });
           
           _logger.LogInformation("Заказ {OrderId} создан", order.Id);
       }
   }

Регистрация сервиса:

.. code-block:: csharp

   services.AddScoped<OrderService>();

Подписка на сообщения в фоновом сервисе
---------------------------------------

.. code-block:: csharp

   using VibeMQ.Client.DependencyInjection;

   public class OrderProcessor : BackgroundService {
       private readonly IVibeMQClientFactory _clientFactory;
       private readonly ILogger<OrderProcessor> _logger;

       public OrderProcessor(
           IVibeMQClientFactory clientFactory,
           ILogger<OrderProcessor> logger) {
           _clientFactory = clientFactory;
           _logger = logger;
       }

       protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
           await using var client = await _clientFactory.CreateAsync(stoppingToken);
           
           await using var subscription = await client.SubscribeAsync<OrderCreated>(
               "orders.created",
               async order => {
                   _logger.LogInformation("Обработка заказа {OrderId}", order.OrderId);
                   await ProcessOrderAsync(order, stoppingToken);
               },
               stoppingToken
           );

           _logger.LogInformation("OrderProcessor запущен");
           
           try {
               await Task.Delay(Timeout.Infinite, stoppingToken);
           } catch (OperationCanceledException) {
               _logger.LogInformation("OrderProcessor остановлен");
           }
       }

       private Task ProcessOrderAsync(OrderCreated order, CancellationToken ct) {
           // Обработка заказа
           return Task.CompletedTask;
       }
   }

   public class OrderCreated {
       public string OrderId { get; set; }
       public decimal Amount { get; set; }
       public string CustomerId { get; set; }
       public DateTime CreatedAt { get; set; }
   }

Регистрация фонового сервиса:

.. code-block:: csharp

   services.AddHostedService<OrderProcessor>();

Шина событий с DI
-----------------

.. code-block:: csharp

   using VibeMQ.Client.DependencyInjection;

   public interface IEventBus {
       Task PublishAsync<T>(string eventType, T eventData, CancellationToken ct = default);
       Task SubscribeAsync<T>(string eventType, Func<T, Task> handler, CancellationToken ct = default);
   }

   public class VibeMQEventBus : IEventBus {
       private readonly IVibeMQClientFactory _clientFactory;
       private readonly ILogger<VibeMQEventBus> _logger;

       public VibeMQEventBus(
           IVibeMQClientFactory clientFactory,
           ILogger<VibeMQEventBus> logger) {
           _clientFactory = clientFactory;
           _logger = logger;
       }

       public async Task PublishAsync<T>(string eventType, T eventData, CancellationToken ct = default) {
           await using var client = await _clientFactory.CreateAsync(ct);
           
           await client.PublishAsync($"events.{eventType}", eventData, options => {
               options.Headers = new Dictionary<string, string> {
                   ["event_type"] = eventType,
                   ["timestamp"] = DateTime.UtcNow.ToString("O")
               };
           });
           
           _logger.LogInformation("Событие {EventType} опубликовано", eventType);
       }

       public async Task SubscribeAsync<T>(string eventType, Func<T, Task> handler, CancellationToken ct = default) {
           var client = await _clientFactory.CreateAsync(ct);
           
           await client.SubscribeAsync<T>(
               $"events.{eventType}",
               async eventData => {
                   _logger.LogInformation("Получено событие {EventType}", eventType);
                   await handler(eventData);
               },
               ct
           );
       }
   }

Регистрация:

.. code-block:: csharp

   services.AddSingleton<IEventBus, VibeMQEventBus>();

Использование:

.. code-block:: csharp

   public class OrderService {
       private readonly IEventBus _eventBus;

       public OrderService(IEventBus eventBus) {
           _eventBus = eventBus;
       }

       public async Task CreateOrderAsync(Order order) {
           // Сохранение заказа в БД
           await SaveOrderAsync(order);
           
           // Публикация события
           await _eventBus.PublishAsync("order.created", new {
               OrderId = order.Id,
               Amount = order.Amount
           });
       }
   }

Комбинированное использование
=============================

Сервер + Клиент в одном приложении
----------------------------------

.. code-block:: csharp

   using VibeMQ.Server.DependencyInjection;
   using VibeMQ.Client.DependencyInjection;

   var host = Host.CreateDefaultBuilder(args)
       .ConfigureServices(services => {
           // Сервер брокера
           services.AddVibeMQBroker(options => {
               options.Port = 8080;
               options.EnableAuthentication = true;
               options.AuthToken = "my-token";
           });
           
           // Клиент для локальной отправки
           services.AddVibeMQClient(settings => {
               settings.Host = "localhost";
               settings.Port = 8080;
               settings.ClientOptions.AuthToken = "my-token";
           });
       })
       .Build();

   await host.RunAsync();

Множественные клиенты
---------------------

Для подключения к нескольким брокерам:

.. code-block:: csharp

   using VibeMQ.Client.DependencyInjection;

   var host = Host.CreateDefaultBuilder(args)
       .ConfigureServices(services => {
           // Клиент для основного брокера
           services.AddVibeMQClient(settings => {
               settings.Host = "vibemq-primary";
               settings.Port = 8080;
           });
           
           // Клиент для резервного брокера
           services.AddKeyedVibeMQClient("backup", settings => {
               settings.Host = "vibemq-backup";
               settings.Port = 8080;
           });
       })
       .Build();

Использование:

.. code-block:: csharp

   public class MultiBrokerService {
       private readonly IVibeMQClientFactory _primaryFactory;
       private readonly IVibeMQClientFactory _backupFactory;

       public MultiBrokerService(
           IVibeMQClientFactory primaryFactory,
           [FromKeyedServices("vibemq-client-backup")] IVibeMQClientFactory backupFactory) {
           _primaryFactory = primaryFactory;
           _backupFactory = backupFactory;
       }
   }

Конфигурация через Environment Variables
========================================

Для Docker и cloud-развёртываний:

**Environment variables:**

.. code-block:: bash

   # Сервер
   VIBEMQ__PORT=8080
   VIBEMQ__ENABLEAUTHENTICATION=true
   VIBEMQ__AUTHTOKEN=my-secret-token
   VIBEMQ__QUEUEDEFAULTS__DEFAULTDELIVERYMODE=RoundRobin
   VIBEMQ__QUEUEDEFAULTS__MAXQUEUESIZE=10000

   # Клиент
   VIBEMQCLIENT__HOST=vibemq-server
   VIBEMQCLIENT__PORT=8080
   VIBEMQCLIENT__CLIENTOPTIONS__AUTHTOKEN=my-secret-token

**Program.cs:**

.. code-block:: csharp

   var host = Host.CreateDefaultBuilder(args)
       .ConfigureServices((context, services) => {
           services.AddVibeMQBroker(
               context.Configuration.GetSection("VibeMQ")
           );
           
           services.AddVibeMQClient(
               context.Configuration.GetSection("VibeMQClient")
           );
       })
       .Build();

   await host.RunAsync();

Docker Compose пример
=====================

.. code-block:: yaml

   version: '3.8'

   services:
     vibemq:
       image: vibemq-server:latest
       environment:
         - VIBEMQ__PORT=8080
         - VIBEMQ__ENABLEAUTHENTICATION=true
         - VIBEMQ__AUTHTOKEN=${VIBEMQ_TOKEN}
       ports:
         - "8080:8080"
         - "8081:8081"

     my-app:
       image: my-app:latest
       environment:
         - VIBEMQCLIENT__HOST=vibemq
         - VIBEMQCLIENT__PORT=8080
         - VIBEMQCLIENT__CLIENTOPTIONS__AUTHTOKEN=${VIBEMQ_TOKEN}
       depends_on:
         - vibemq

Устранение проблем
==================

Ошибка: «Broker already registered»
----------------------------------

**Причина:** Попытка зарегистрировать брокер несколько раз.

**Решение:** Убедитесь, что ``AddVibeMQBroker`` вызывается один раз.

Ошибка: «Unable to connect»
---------------------------

**Причина:** Сервер ещё не запущен при создании клиента.

**Решение:** Создавайте клиента после запуска хоста:

.. code-block:: csharp

   await host.RunAsync();  // Сервер запущен
   
   // Теперь можно создавать клиентов
   var factory = host.Services.GetRequiredService<IVibeMQClientFactory>();
   await using var client = await factory.CreateAsync();

Автозапуск сервера
------------------

Сервер запускается автоматически как ``IHostedService``. Для ручного управления:

.. code-block:: csharp

   services.AddVibeMQBroker(options => { ... });
   
   // Получение экземпляра
   var broker = host.Services.GetRequiredService<BrokerServer>();

Следующие шаги
==============

- :doc:`server-setup` — настройка сервера
- :doc:`client-usage` — использование клиента
- :doc:`monitoring` — мониторинг
