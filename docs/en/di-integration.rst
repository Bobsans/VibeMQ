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

Basic Registration
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

Publishing Messages from Service
--------------------------------

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

Service registration:

.. code-block:: csharp

   services.AddScoped<OrderService>();

Subscribing to Messages in Background Service
----------------------------------------------

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
           // Order processing
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

Event Bus with DI
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
           
           _logger.LogInformation("Event {EventType} published", eventType);
       }

       public async Task SubscribeAsync<T>(string eventType, Func<T, Task> handler, CancellationToken ct = default) {
           var client = await _clientFactory.CreateAsync(ct);
           
           await client.SubscribeAsync<T>(
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
           // Save order to database
           await SaveOrderAsync(order);
           
           // Publish event
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

Multiple Clients
----------------

To connect to multiple brokers:

.. code-block:: csharp

   using VibeMQ.Client.DependencyInjection;

   var host = Host.CreateDefaultBuilder(args)
       .ConfigureServices(services => {
           // Client for primary broker
           services.AddVibeMQClient(settings => {
               settings.Host = "vibemq-primary";
               settings.Port = 8080;
           });
           
           // Client for backup broker
           services.AddKeyedVibeMQClient("backup", settings => {
               settings.Host = "vibemq-backup";
               settings.Port = 8080;
           });
       })
       .Build();

Usage:

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

**Solution:** Create client after host starts:

.. code-block:: csharp

   await host.RunAsync();  // Server started
   
   // Now clients can be created
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
