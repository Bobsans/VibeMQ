=====================
DI Integration
=====================

This guide describes VibeMQ integration with Microsoft.Extensions.DependencyInjection.

.. contents:: Contents
   :local:
   :depth: 2

Overview
========

VibeMQ provides packages for convenient Dependency Injection integration:

- ``VibeMQ.Server.DependencyInjection`` — for server
- ``VibeMQ.Client.DependencyInjection`` — for client

These packages register necessary services in the DI container and automatically manage component lifecycle.

Package Installation
====================

.. code-block:: bash

   dotnet add package VibeMQ.Server.DependencyInjection
   dotnet add package VibeMQ.Client.DependencyInjection

Server Integration
==================

Basic Registration
-------------------

.. code-block:: csharp

   using VibeMQ.Server.DependencyInjection;

   var host = Host.CreateDefaultBuilder(args)
       .ConfigureServices(services => {
           services.AddVibeMQBroker();
       })
       .Build();

   await host.RunAsync();

The server will start automatically when the host starts.

Registration with Configuration
-------------------------------

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

Configuration from appsettings.json
-----------------------------------

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

Advanced Configuration
-----------------------

.. code-block:: csharp

   using VibeMQ.Server.DependencyInjection;
   using VibeMQ.Core.Enums;

   var host = Host.CreateDefaultBuilder(args)
       .ConfigureServices(services => {
           services.AddVibeMQBroker(options => {
               // Basic settings
               options.Port = 8080;
               options.MaxConnections = 5000;
               options.MaxMessageSize = 2_097_152;
               
               // Authentication
               options.EnableAuthentication = true;
               options.AuthToken = Environment.GetEnvironmentVariable("VIBEMQ_TOKEN");
               
               // Queues
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

Client Integration
==================

A single call to ``AddVibeMQClient`` registers:

- **``IVibeMQClient``** — a shared, lazily-connected client (Singleton). Inject it into any service and use ``PublishAsync`` / ``SubscribeAsync``; the connection is established on first use. No need to call ``ConnectAsync`` or dispose the client yourself; the host manages its lifecycle.

- **``IVibeMQClientFactory``** — use when you need a dedicated client instance that you create and dispose yourself (e.g. ``await using var client = await factory.CreateAsync()``).

Basic Registration
-------------------

.. code-block:: csharp

   using VibeMQ.Client;
   using VibeMQ.Client.DependencyInjection;

   var host = Host.CreateDefaultBuilder(args)
       .ConfigureServices(services => {
           services.AddVibeMQClient();
       })
       .Build();

   // Option A: inject IVibeMQClient (shared client, connects on first use)
   var client = host.Services.GetRequiredService<IVibeMQClient>();
   await client.PublishAsync("queue", new { Message = "Hello" });

   // Option B: create a dedicated client (you dispose it)
   var factory = host.Services.GetRequiredService<IVibeMQClientFactory>();
   await using var dedicatedClient = await factory.CreateAsync();
   await dedicatedClient.PublishAsync("queue", new { Message = "Hello" });

Registration with Configuration
-------------------------------

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

Configuration from appsettings.json
-----------------------------------

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

Advanced Client Configuration
------------------------------

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

Using in Services
=================

Publishing Messages from Service (IVibeMQClient)
-------------------------------------------------

The simplest approach is to inject ``IVibeMQClient``. The client connects lazily on first use; no manual ``ConnectAsync`` or disposal in your service.

.. code-block:: csharp

   using VibeMQ.Client;
   using VibeMQ.Client.DependencyInjection;

   public class OrderService {
       private readonly IVibeMQClient _vibeMQ;
       private readonly ILogger<OrderService> _logger;

       public OrderService(IVibeMQClient vibeMQ, ILogger<OrderService> logger) {
           _vibeMQ = vibeMQ;
           _logger = logger;
       }

       public async Task CreateOrderAsync(Order order) {
           await _vibeMQ.PublishAsync("orders.created", new {
               OrderId = order.Id,
               Amount = order.Amount,
               CustomerId = order.CustomerId,
               CreatedAt = DateTime.UtcNow
           });
           _logger.LogInformation("Order {OrderId} created", order.Id);
       }
   }

Service registration:

.. code-block:: csharp

   services.AddScoped<OrderService>();

Publishing with IVibeMQClientFactory (dedicated client)
--------------------------------------------------------

If you prefer a dedicated client instance per operation (you create and dispose it):

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
           _logger.LogInformation("Order {OrderId} created", order.Id);
       }
   }

   services.AddScoped<OrderService>();

Subscribing to Messages in Background Service
----------------------------------------------

You can use either ``IVibeMQClient`` (shared, lazy-connected) or ``IVibeMQClientFactory``. Example with ``IVibeMQClient``:

.. code-block:: csharp

   using VibeMQ.Client;
   using VibeMQ.Client.DependencyInjection;

   public class OrderProcessor : BackgroundService {
       private readonly IVibeMQClient _vibeMQ;
       private readonly ILogger<OrderProcessor> _logger;

       public OrderProcessor(IVibeMQClient vibeMQ, ILogger<OrderProcessor> logger) {
           _vibeMQ = vibeMQ;
           _logger = logger;
       }

       protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
           await using var subscription = await _vibeMQ.SubscribeAsync<OrderCreated>(
               "orders.created",
               async order => {
                   _logger.LogInformation("Processing order {OrderId}", order.OrderId);
                   await ProcessOrderAsync(order, stoppingToken);
               },
               stoppingToken
           );

           _logger.LogInformation("OrderProcessor started");
           try {
               await Task.Delay(Timeout.Infinite, stoppingToken);
           } catch (OperationCanceledException) {
               _logger.LogInformation("OrderProcessor stopped");
           }
       }

       private Task ProcessOrderAsync(OrderCreated order, CancellationToken ct) {
           return Task.CompletedTask;
       }
   }

   public class OrderCreated {
       public string OrderId { get; set; }
       public decimal Amount { get; set; }
       public string CustomerId { get; set; }
       public DateTime CreatedAt { get; set; }
   }

Background service registration:

.. code-block:: csharp

   services.AddHostedService<OrderProcessor>();

Class-based Subscriptions with Automatic Registration
------------------------------------------------------

You can use class-based message handlers with automatic subscription on application start:

.. code-block:: csharp

   using VibeMQ.Core.Attributes;
   using VibeMQ.Core.Interfaces;
   using VibeMQ.Client.DependencyInjection;

   // Define handler with Queue attribute
   [Queue("orders.created")]
   public class OrderHandler : IMessageHandler<OrderCreated> {
       private readonly ILogger<OrderHandler> _logger;

       public OrderHandler(ILogger<OrderHandler> logger) {
           _logger = logger;
       }

       public async Task HandleAsync(OrderCreated message, CancellationToken cancellationToken) {
           _logger.LogInformation("Processing order {OrderId}", message.OrderId);
           // Process order
       }
   }

   // Register handler and enable automatic subscriptions
   services.AddVibeMQClient(settings => {
       settings.Host = "localhost";
       settings.Port = 8080;
   })
   .AddMessageHandler<OrderCreated, OrderHandler>()  // Register handler
   .AddMessageHandlerSubscriptions();  // Enable automatic subscription on startup

Alternatively, scan assembly for all handlers:

.. code-block:: csharp

   services.AddVibeMQClient(settings => { ... })
       .AddMessageHandlers(Assembly.GetExecutingAssembly())  // Scan and register all handlers
       .AddMessageHandlerSubscriptions();  // Auto-subscribe on startup

Handlers are resolved from DI container, so you can inject dependencies:

.. code-block:: csharp

   [Queue("orders.created")]
   public class OrderHandler : IMessageHandler<OrderCreated> {
       private readonly IOrderRepository _repository;
       private readonly ILogger<OrderHandler> _logger;

       public OrderHandler(IOrderRepository repository, ILogger<OrderHandler> logger) {
           _repository = repository;
           _logger = logger;
       }

       public async Task HandleAsync(OrderCreated message, CancellationToken cancellationToken) {
           await _repository.SaveAsync(message);
           _logger.LogInformation("Order {OrderId} saved", message.OrderId);
       }
   }

   // Register dependencies
   services.AddScoped<IOrderRepository, OrderRepository>();
   services.AddMessageHandler<OrderCreated, OrderHandler>();

Event Bus with DI
-----------------

A thin event-bus wrapper around ``IVibeMQClient`` (shared client, lazy connection):

.. code-block:: csharp

   using VibeMQ.Client;
   using VibeMQ.Client.DependencyInjection;

   public interface IEventBus {
       Task PublishAsync<T>(string eventType, T eventData, CancellationToken ct = default);
       Task<IAsyncDisposable> SubscribeAsync<T>(string eventType, Func<T, Task> handler, CancellationToken ct = default);
   }

   public class VibeMQEventBus : IEventBus {
       private readonly IVibeMQClient _client;
       private readonly ILogger<VibeMQEventBus> _logger;

       public VibeMQEventBus(IVibeMQClient client, ILogger<VibeMQEventBus> logger) {
           _client = client;
           _logger = logger;
       }

       public async Task PublishAsync<T>(string eventType, T eventData, CancellationToken ct = default) {
           await _client.PublishAsync($"events.{eventType}", eventData, ct);
           _logger.LogInformation("Event {EventType} published", eventType);
       }

       public async Task<IAsyncDisposable> SubscribeAsync<T>(string eventType, Func<T, Task> handler, CancellationToken ct = default) {
           return await _client.SubscribeAsync<T>(
               $"events.{eventType}",
               async eventData => {
                   _logger.LogInformation("Received event {EventType}", eventType);
                   await handler(eventData);
               },
               ct
           );
       }
   }

Registration:

.. code-block:: csharp

   services.AddSingleton<IEventBus, VibeMQEventBus>();

Usage:

.. code-block:: csharp

   public class OrderService {
       private readonly IEventBus _eventBus;

       public OrderService(IEventBus eventBus) {
           _eventBus = eventBus;
       }

       public async Task CreateOrderAsync(Order order) {
           await SaveOrderAsync(order);
           await _eventBus.PublishAsync("order.created", new {
               OrderId = order.Id,
               Amount = order.Amount
           });
       }
   }

Combined Usage
==============

Server + Client in One Application
----------------------------------

.. code-block:: csharp

   using VibeMQ.Server.DependencyInjection;
   using VibeMQ.Client.DependencyInjection;

   var host = Host.CreateDefaultBuilder(args)
       .ConfigureServices(services => {
           // Broker server
           services.AddVibeMQBroker(options => {
               options.Port = 8080;
               options.EnableAuthentication = true;
               options.AuthToken = "my-token";
           });
           
           // Client for local publishing
           services.AddVibeMQClient(settings => {
               settings.Host = "localhost";
               settings.Port = 8080;
               settings.ClientOptions.AuthToken = "my-token";
           });
       })
       .Build();

   await host.RunAsync();

Multiple brokers
----------------

To use multiple brokers, register multiple named configurations and resolve the appropriate factory or client by name (e.g. via a custom factory or keyed services if your app supports them). By default, one ``AddVibeMQClient`` call registers a single shared ``IVibeMQClient`` and one ``IVibeMQClientFactory`` for that configuration.

Configuration via Environment Variables
======================================

For Docker and cloud deployments:

**Environment variables:**

.. code-block:: bash

   # Server
   VIBEMQ__PORT=8080
   VIBEMQ__ENABLEAUTHENTICATION=true
   VIBEMQ__AUTHTOKEN=my-secret-token
   VIBEMQ__QUEUEDEFAULTS__DEFAULTDELIVERYMODE=RoundRobin
   VIBEMQ__QUEUEDEFAULTS__MAXQUEUESIZE=10000

   # Client
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

Docker Compose Example
======================

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

Troubleshooting
===============

Error: "Broker already registered"
----------------------------------

**Cause:** Attempting to register broker multiple times.

**Solution:** Make sure ``AddVibeMQBroker`` is called only once.

Error: "Unable to connect"
---------------------------

**Cause:** Server is not started yet when creating client.

**Solution:** When using ``IVibeMQClient``, the client connects lazily on first use (e.g. first ``PublishAsync`` or ``SubscribeAsync``), so ensure the broker is running by then. When using ``IVibeMQClientFactory``, create the client after the host (and broker) have started:

.. code-block:: csharp

   await host.StartAsync();  // Server started
   
   var factory = host.Services.GetRequiredService<IVibeMQClientFactory>();
   await using var client = await factory.CreateAsync();

Auto-start Server
-----------------

Server starts automatically as ``IHostedService``. For manual control:

.. code-block:: csharp

   services.AddVibeMQBroker(options => { ... });
   
   // Get instance
   var broker = host.Services.GetRequiredService<BrokerServer>();

Next Steps
==========

- :doc:`server-setup` — server setup
- :doc:`client-usage` — client usage
- :doc:`monitoring` — monitoring
