=================
Client Usage
=================

This guide describes various ways to use the VibeMQ client.

.. contents:: Contents
   :local:
   :depth: 2

Connecting to Server
====================

Basic Connection
----------------

.. code-block:: csharp

   using VibeMQ.Client;

   await using var client = await VibeMQClient.ConnectAsync(
       "localhost",
       8080
   );

   Console.WriteLine($"Connected: {client.IsConnected}");

Connection with Authentication
------------------------------

.. code-block:: csharp

   var client = await VibeMQClient.ConnectAsync(
       "localhost",
       8080,
       new ClientOptions {
           AuthToken = "my-secret-token"
       }
   );

Connection with Logging
-----------------------

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

Publishing Messages
===================

Basic Publishing
----------------

.. code-block:: csharp

   await client.PublishAsync("notifications", new {
       Title = "Hello",
       Body = "This is a test message",
       Timestamp = DateTime.Now
   });

Publishing with Priority
------------------------

.. code-block:: csharp

   using VibeMQ.Core.Enums;

   // Critical message
   await client.PublishAsync("alerts", alertData, options => {
       options.Priority = MessagePriority.Critical;
   });

   // High priority
   await client.PublishAsync("orders", orderData, options => {
       options.Priority = MessagePriority.High;
   });

Publishing with Headers
-----------------------

.. code-block:: csharp

   await client.PublishAsync("orders", orderData, options => {
       options.Headers = new Dictionary<string, string> {
           ["correlationId"] = Guid.NewGuid().ToString(),
           ["source"] = "order-service",
           ["version"] = "1.0"
       };
   });

Typed Publishing
----------------

Create a class for the message:

.. code-block:: csharp

   public class OrderCreated {
       public string OrderId { get; set; }
       public decimal Amount { get; set; }
       public DateTime CreatedAt { get; set; }
   }

Use it:

.. code-block:: csharp

   await client.PublishAsync("orders.created", new OrderCreated {
       OrderId = "ORD-123",
       Amount = 99.99m,
       CreatedAt = DateTime.UtcNow
   });

Subscribing to Messages
=======================

Basic Subscription
------------------

.. code-block:: csharp

   await using var subscription = await client.SubscribeAsync<dynamic>(
       "notifications",
       async msg => {
           Console.WriteLine($"Received: {msg.Title} - {msg.Body}");
       }
   );

Typed Subscription
------------------

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

Subscription with Error Handling
--------------------------------

.. code-block:: csharp

   await using var subscription = await client.SubscribeAsync<Notification>(
       "notifications",
       async notification => {
           try {
               await ProcessNotificationAsync(notification);
           } catch (Exception ex) {
               Console.WriteLine($"Processing error: {ex.Message}");
               throw;  // Broker will retry delivery
           }
       }
   );

Multiple Subscriptions
----------------------

.. code-block:: csharp

   var subscriptions = new List<IAsyncDisposable>();

   // Subscribe to multiple queues
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

   // Release resources
   foreach (var subscription in subscriptions) {
       await subscription.DisposeAsync();
   }

Unsubscribing from Queue
========================

Automatic Unsubscribe
---------------------

When using ``await using``, unsubscription happens automatically:

.. code-block:: csharp

   await using var subscription = await client.SubscribeAsync<dynamic>(
       "notifications",
       async msg => { /* processing */ }
   );
   // DisposeAsync() is called automatically

Manual Unsubscribe
------------------

.. code-block:: csharp

   var subscription = await client.SubscribeAsync<dynamic>(
       "notifications",
       async msg => { /* processing */ }
   );

   // When you need to unsubscribe
   await subscription.DisposeAsync();

Or via client method:

.. code-block:: csharp

   await client.UnsubscribeAsync("notifications");

Queue Management
================

Creating a Queue
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

Deleting a Queue
----------------

.. code-block:: csharp

   await client.DeleteQueueAsync("my-queue");

Getting Queue Information
-------------------------

.. code-block:: csharp

   var info = await client.GetQueueInfoAsync("my-queue");

   if (info != null) {
       Console.WriteLine($"Queue: {info.Name}");
       Console.WriteLine($"Messages: {info.MessageCount}");
       Console.WriteLine($"Subscribers: {info.SubscriberCount}");
       Console.WriteLine($"Delivery Mode: {info.DeliveryMode}");
   }

List Queues
-----------

.. code-block:: csharp

   var queues = await client.ListQueuesAsync();

   foreach (var queueName in queues) {
       Console.WriteLine(queueName);
   }

Client Settings
===============

ClientOptions
-------------

.. code-block:: csharp

   var options = new ClientOptions {
       // Authentication
       AuthToken = "my-secret-token",

       // Keep-alive
       KeepAliveInterval = TimeSpan.FromSeconds(30),

       // Command timeout
       CommandTimeout = TimeSpan.FromSeconds(10),

       // TLS
       UseTls = false,
       SkipCertificateValidation = false,

       // Reconnect policy
       ReconnectPolicy = new ReconnectPolicy {
           MaxAttempts = 10,
           InitialDelay = TimeSpan.FromSeconds(1),
           MaxDelay = TimeSpan.FromMinutes(5),
           UseExponentialBackoff = true
       }
   };

ReconnectPolicy
---------------

Configure reconnection policy:

.. code-block:: csharp

   ReconnectPolicy = new ReconnectPolicy {
       MaxAttempts = int.MaxValue,      // Max attempts
       InitialDelay = TimeSpan.FromSeconds(1),  // Initial delay
       MaxDelay = TimeSpan.FromMinutes(5),      // Maximum delay
       UseExponentialBackoff = true     // Exponential increase
   }

**How it works:**

- Attempt 1: immediately
- Attempt 2: after 1s
- Attempt 3: after 2s
- Attempt 4: after 4s
- Attempt 5: after 8s
- ...
- Attempt N: after 5min (maximum)

TLS/SSL Connection
==================

Connection with TLS:

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

   For production, use valid certificates.
   ``SkipCertificateValidation = true`` only for tests!

Handling Disconnections
=======================

Automatic Reconnection
----------------------

Client automatically reconnects on connection loss:

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

   // Subscription will be restored after reconnect
   await using var subscription = await client.SubscribeAsync<dynamic>(
       "notifications",
       async msg => { /* processing */ }
   );

Status Check
------------

.. code-block:: csharp

   if (client.IsConnected) {
       await client.PublishAsync("queue", data);
   } else {
       Console.WriteLine("Client disconnected");
   }

Events (Optional)
-----------------

For status tracking, you can use periodic checks:

.. code-block:: csharp

   _ = Task.Run(async () => {
       while (true) {
           await Task.Delay(5000);
           Console.WriteLine($"Status: {(client.IsConnected ? "Connected" : "Disconnected")}");
       }
   });

Disconnecting
=============

Graceful Disconnect
-------------------

.. code-block:: csharp

   await client.DisconnectAsync();

Using Using
-----------

.. code-block:: csharp

   await using var client = await VibeMQClient.ConnectAsync("localhost", 8080);

   // Work with client

   // DisposeAsync() is called automatically

Full resource release:

.. code-block:: csharp

   await client.DisposeAsync();

Usage Examples
==============

Simple Publisher
----------------

.. code-block:: csharp

   using VibeMQ.Client;

   await using var publisher = await VibeMQClient.ConnectAsync("localhost", 8080, new ClientOptions {
       AuthToken = "my-token"
   });

   Console.WriteLine("Publisher connected. Enter message (Enter to exit):");

   while (true) {
       var input = Console.ReadLine();
       if (string.IsNullOrWhiteSpace(input)) break;

       await publisher.PublishAsync("messages", new {
           Text = input,
           Timestamp = DateTime.Now
       });

       Console.WriteLine("✓ Message sent");
   }

Simple Subscriber
-----------------

.. code-block:: csharp

   using VibeMQ.Client;

   await using var subscriber = await VibeMQClient.ConnectAsync("localhost", 8080, new ClientOptions {
       AuthToken = "my-token"
   });

   await using var subscription = await subscriber.SubscribeAsync<dynamic>(
       "messages",
       async msg => {
           Console.WriteLine($"📨 {msg.Text} (at {msg.Timestamp})");
       }
   );

   Console.WriteLine("Subscriber started. Press Enter to exit...");
   Console.ReadLine();

Task Processing Worker
----------------------

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
                       Console.WriteLine($"✓ Order {order.Id} processed");
                   } catch (Exception ex) {
                       Console.WriteLine($"✗ Error: {ex.Message}");
                       throw;  // For retry
                   }
               },
               cancellationToken
           );

           await Task.Delay(Timeout.Infinite, cancellationToken);
       }

       private Task ProcessOrderAsync(Order order) {
           // Order processing
           return Task.CompletedTask;
       }
   }

   public class Order {
       public string Id { get; set; }
       public decimal Amount { get; set; }
       public string Customer { get; set; }
   }

Event Bus for Microservices
---------------------------

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

           _logger.LogInformation("Event {EventType} published", eventType);
       }

       public async Task SubscribeAsync<T>(string eventType, Func<T, Task> handler) {
           await _client.SubscribeAsync<T>(
               $"events.{eventType}",
               async eventData => {
                   _logger.LogInformation("Received event {EventType}", eventType);
                   await handler(eventData);
               }
           );
       }
   }

Troubleshooting
===============

Connection Error
----------------

**Error:** ``Connection refused``

**Causes:**

- Server not running
- Wrong port
- Firewall blocking connection

**Solution:**

.. code-block:: csharp

   // Check connection parameters
   var client = await VibeMQClient.ConnectAsync(
       "localhost",  // Or correct host
       8080,         // Or correct port
       new ClientOptions { ... }
   );

Authentication Error
--------------------

**Error:** ``Authentication failed``

**Solution:** Make sure tokens match:

.. code-block:: csharp

   // Server
   .UseAuthentication("my-token")

   // Client
   new ClientOptions { AuthToken = "my-token" }

Connection Timeout
------------------

**Error:** ``Connection timeout``

**Solution:** Increase timeout:

.. code-block:: csharp

   new ClientOptions {
       CommandTimeout = TimeSpan.FromSeconds(30)
   }

Frequent Disconnections
-----------------------

**Cause:** Network or server issues

**Solution:** Configure reconnect policy:

.. code-block:: csharp

   new ClientOptions {
       ReconnectPolicy = new ReconnectPolicy {
           MaxAttempts = 50,  // Increase attempts
           InitialDelay = TimeSpan.FromSeconds(2),
           MaxDelay = TimeSpan.FromMinutes(1)
       }
   }

Next Steps
==========

- :doc:`server-setup` — server configuration
- :doc:`configuration` — configuration parameters
- :doc:`di-integration` — DI integration
