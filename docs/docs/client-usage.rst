=================
Client Usage
=================

This guide describes various ways to use the VibeMQ client.

.. contents:: Contents
   :local:
   :depth: 2

Using with Dependency Injection (IVibeMQClient)
================================================

In ASP.NET Core or Worker Service you can inject **``IVibeMQClient``** and use it without calling ``ConnectAsync`` or managing disposal. The client is shared (Singleton) and connects lazily on first ``PublishAsync`` or ``SubscribeAsync``. See :doc:`di-integration` for registration and examples.

.. code-block:: csharp

   using VibeMQ.Client;
   using VibeMQ.Client.DependencyInjection;

   // Registration (e.g. in Program.cs)
   services.AddLogging();
   services.AddVibeMQClient(settings => {
       settings.Host = "localhost";
       settings.Port = 2925;
       settings.ClientOptions.AuthToken = "my-token";
   });

   // In any service
   public class MyService {
       private readonly IVibeMQClient _vibeMQ;
       public MyService(IVibeMQClient vibeMQ) => _vibeMQ = vibeMQ;

       public async Task SendAsync() {
           await _vibeMQ.PublishAsync("queue", new { Text = "Hello" });
       }
   }

The concrete type ``VibeMQClient`` implements ``IVibeMQClient``. For manual connection and full control, use ``VibeMQClient.ConnectAsync`` as in the sections below.

Connecting to Server
====================

Connection String
-----------------

You can connect using a single connection string (URL or key=value format). This is convenient for
environment variables (e.g. ``VIBEMQ_CONNECTION_STRING``) or configuration (e.g. ``ConnectionStrings:VibeMQ``).

**URL format:** ``vibemq://[username[:password]@]host[:port][?query]``

.. code-block:: csharp

   using VibeMQ.Client;

   // Minimal
   await using var client = await VibeMQClient.ConnectAsync("vibemq://localhost");

   // With port and options
   await using var client2 = await VibeMQClient.ConnectAsync(
       "vibemq://user:secret@broker.example.com:2925?tls=true&keepAlive=60&compression=brotli,gzip"
   );

**Key=value format:** ``Host=...;Port=...;Username=...;Password=...;UseTls=...`` (semicolon-separated pairs).

.. code-block:: csharp

   await using var client = await VibeMQClient.ConnectAsync(
       "Host=localhost;Port=2925;Username=user;Password=secret;UseTls=true"
   );

Supported query/keys: ``tls``, ``skipCertValidation``, ``keepAlive``, ``commandTimeout``,
``compression`` (``none``, ``brotli``, ``gzip`` or comma-separated), ``compressionThreshold``,
``reconnectMaxAttempts``, ``reconnectInitialDelay``, ``reconnectMaxDelay``, ``reconnectExponentialBackoff``,
``queues`` (comma-separated queue names for declare-on-connect). Invalid strings throw
``VibeMQConnectionStringException``. Use ``VibeMQConnectionString.Parse`` or ``TryParse`` to obtain
host, port, and ``ClientOptions`` without connecting.

Basic Connection
----------------

.. code-block:: csharp

   using VibeMQ.Client;

   await using var client = await VibeMQClient.ConnectAsync(
       "localhost",
       2925
   );

   Console.WriteLine($"Connected: {client.IsConnected}");

Connection with Authentication
------------------------------

Legacy token:

.. code-block:: csharp

   var client = await VibeMQClient.ConnectAsync(
       "localhost",
       2925,
       new ClientOptions {
           AuthToken = "my-secret-token"
       }
   );

Username/password (see :doc:`authorization`):

.. code-block:: csharp

   var client = await VibeMQClient.ConnectAsync(
       "localhost",
       2925,
       new ClientOptions {
           Username = "alice",
           Password = "alice-secret"
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
       2925,
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

Publishing with Headers
-----------------------

You can attach custom headers to messages, including priority:

.. code-block:: csharp

   using VibeMQ.Enums;

   // Publish with custom headers
   await client.PublishAsync("orders", orderData, new Dictionary<string, string> {
       ["correlationId"] = Guid.NewGuid().ToString(),
       ["source"] = "order-service",
       ["version"] = "1.0"
   });

   // Publish with priority via headers
   await client.PublishAsync("alerts", alertData, new Dictionary<string, string> {
       ["priority"] = MessagePriority.Critical.ToString()
   });

   // Publish with both priority and custom headers
   await client.PublishAsync("orders", orderData, new Dictionary<string, string> {
       ["priority"] = MessagePriority.High.ToString(),
       ["correlationId"] = Guid.NewGuid().ToString(),
       ["source"] = "order-service"
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

Class-based Subscriptions
--------------------------

Instead of using lambda handlers, you can create handler classes implementing ``IMessageHandler<T>``:

.. code-block:: csharp

   using VibeMQ.Interfaces;

   // Define message handler
   public class OrderHandler : IMessageHandler<OrderCreated> {
       private readonly ILogger<OrderHandler> _logger;

       public OrderHandler(ILogger<OrderHandler> logger) {
           _logger = logger;
       }

       public async Task HandleAsync(OrderCreated message, CancellationToken cancellationToken) {
           _logger.LogInformation("Processing order {OrderId}", message.OrderId);
           await ProcessOrderAsync(message, cancellationToken);
       }

       private Task ProcessOrderAsync(OrderCreated order, CancellationToken ct) {
           // Process order
           return Task.CompletedTask;
       }
   }

   // Subscribe using handler class
   await using var subscription = await client.SubscribeAsync<OrderCreated, OrderHandler>("orders.created");

This approach provides better testability, dependency injection support, and cleaner code organization.

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

.. _queue-declarations:

Queue Declarations
==================

Queue declarations let you describe the queues your application needs directly in ``ClientOptions``.
On each ``ConnectAsync``, the client automatically creates any missing queues and — when the queue
already exists — compares the declared settings against the live configuration and reacts according
to your chosen conflict strategy.

This is the recommended way to manage queues in production: your code is the source of truth and the
broker always matches it.

Declaring Queues
----------------

Use the fluent ``DeclareQueue`` helper on ``ClientOptions``:

.. code-block:: csharp

   using VibeMQ.Client;
   using VibeMQ.Configuration;
   using VibeMQ.Enums;

   await using var client = await VibeMQClient.ConnectAsync(
       "localhost",
       2925,
       new ClientOptions {
           AuthToken = "my-token",
       }
       .DeclareQueue("orders", q => {
           q.Mode            = DeliveryMode.FanOutWithAck;
           q.MaxQueueSize    = 50_000;
           q.EnableDeadLetterQueue = true;
           q.MessageTtl      = TimeSpan.FromHours(24);
       }, onConflict: QueueConflictResolution.Fail)

       .DeclareQueue("analytics-events", q => {
           q.MaxQueueSize    = 200_000;
           q.OverflowStrategy = OverflowStrategy.DropOldest;
       })

       .DeclareQueue("transient-tasks",
           onConflict: QueueConflictResolution.Override)
   );

Declarations are processed **sequentially** in the order they appear, which ensures a DLQ queue
exists before the main queue that references it.

Conflict Resolution
-------------------

When a queue already exists, the client computes a diff between the declared and the live
configuration. Each setting difference is classified by severity:

+-------------------+-----------------------------------------------------------------------+
| Severity          | Description                                                           |
+===================+=======================================================================+
| ``Info``          | Additive or neutral change (e.g. increasing ``MaxQueueSize``).        |
|                   | Never a conflict. Logged at Debug. ``OnConflict`` is not triggered.   |
+-------------------+-----------------------------------------------------------------------+
| ``Soft``          | Behavioral change that may affect in-flight messages                  |
|                   | (e.g. enabling TTL). Logged at Warning. ``OnConflict`` is applied.    |
+-------------------+-----------------------------------------------------------------------+
| ``Hard``          | Breaking semantic change (e.g. changing ``Mode``). Logged at Error.   |
|                   | ``OnConflict`` is applied.                                            |
+-------------------+-----------------------------------------------------------------------+

The ``OnConflict`` strategy determines what happens when at least one ``Soft`` or ``Hard``
difference is detected:

+---------------+-------------------------------------------------------------------+
| Strategy      | Behavior                                                          |
+===============+===================================================================+
| ``Ignore``    | Log the diff and continue. Default.                               |
+---------------+-------------------------------------------------------------------+
| ``Fail``      | Throw ``QueueConflictException`` — treat drift as a deploy error. |
+---------------+-------------------------------------------------------------------+
| ``Override``  | Delete and recreate the queue. **All messages are lost.**         |
+---------------+-------------------------------------------------------------------+

**Severity classification per setting:**

+-------------------------------+-------------------------+------------+
| Setting                       | Direction               | Severity   |
+===============================+=========================+============+
| ``Mode``                      | any                     | Hard       |
+-------------------------------+-------------------------+------------+
| ``MaxQueueSize``              | any                     | Info       |
+-------------------------------+-------------------------+------------+
| ``MessageTtl``                | ``null`` → value        | Soft       |
+-------------------------------+-------------------------+------------+
| ``MessageTtl``                | value → ``null``        | Info       |
+-------------------------------+-------------------------+------------+
| ``MessageTtl``                | decrease                | Soft       |
+-------------------------------+-------------------------+------------+
| ``MessageTtl``                | increase                | Info       |
+-------------------------------+-------------------------+------------+
| ``EnableDeadLetterQueue``     | ``false`` → ``true``    | Info       |
+-------------------------------+-------------------------+------------+
| ``EnableDeadLetterQueue``     | ``true`` → ``false``    | Soft       |
+-------------------------------+-------------------------+------------+
| ``DeadLetterQueueName``       | any (when DLQ active)   | Hard       |
+-------------------------------+-------------------------+------------+
| ``OverflowStrategy``          | → non-``RedirectToDlq`` | Info       |
+-------------------------------+-------------------------+------------+
| ``OverflowStrategy``          | → ``RedirectToDlq``,    | Info       |
|                               | DLQ enabled             |            |
+-------------------------------+-------------------------+------------+
| ``OverflowStrategy``          | → ``RedirectToDlq``,    | Hard       |
|                               | DLQ **not** enabled     |            |
+-------------------------------+-------------------------+------------+
| ``MaxRetryAttempts``          | any                     | Info       |
+-------------------------------+-------------------------+------------+

Handling QueueConflictException
--------------------------------

When ``OnConflict = Fail`` and a conflict is detected, ``ConnectAsync`` throws
``QueueConflictException``. The exception carries the full diff for diagnostics:

.. code-block:: csharp

   using VibeMQ.Client.Exceptions;

   try {
       await using var client = await VibeMQClient.ConnectAsync("localhost", 2925, options);
   } catch (QueueConflictException ex) {
       Console.WriteLine($"Queue '{ex.QueueName}' has conflicting settings:");
       foreach (var diff in ex.Conflicts) {
           Console.WriteLine($"  [{diff.Severity}] {diff.SettingName}: " +
                             $"{diff.ExistingValue} → {diff.DeclaredValue}");
       }
       Console.WriteLine($"Highest severity: {ex.HighestSeverity}");
   }

The ``Conflicts`` list contains only ``Soft`` and ``Hard`` diffs. ``Info`` differences are never
included.

Provisioning Errors vs. Conflicts
----------------------------------

``FailOnProvisioningError`` (default ``true``) controls what happens when a provisioning
operation fails for a technical reason (e.g. network timeout, broker error) — not for a conflict.
Set it to ``false`` to let the client skip that queue and continue connecting:

.. code-block:: csharp

   options.DeclareQueue("non-critical-cache",
       q => q.MaxQueueSize = 10_000,
       onConflict: QueueConflictResolution.Ignore,
       failOnError: false   // skip on error, do not abort connection
   );

.. note::

   ``FailOnProvisioningError`` never suppresses ``QueueConflictException``. Conflicts always
   propagate regardless of this flag.

Pre-flight Validation
---------------------

The client validates declarations **before** establishing a TCP connection. An
``InvalidOperationException`` is thrown immediately if a declaration contains an incompatible
combination of options, such as ``OverflowStrategy = RedirectToDlq`` without
``EnableDeadLetterQueue = true``:

.. code-block:: csharp

   // This throws before any network call is made:
   options.DeclareQueue("bad-queue", q => {
       q.OverflowStrategy     = OverflowStrategy.RedirectToDlq;
       q.EnableDeadLetterQueue = false; // ← invalid
   });

Reconnect Behavior
------------------

On automatic reconnect, the client re-runs provisioning with a forced ``Ignore`` strategy for
all declarations. This ensures missing queues are recreated (e.g. after a server restart that
lost in-memory data) without aborting the reconnect due to a conflict detected on a previous
connection.

Queue Declarations with Dependency Injection
---------------------------------------------

Call ``DeclareQueue`` directly on ``settings.ClientOptions``:

.. code-block:: csharp

   services.AddVibeMQClient(settings => {
       settings.Host = "localhost";
       settings.Port = 2925;

       settings.ClientOptions
           .DeclareQueue("orders", q => {
               q.Mode                  = DeliveryMode.FanOutWithAck;
               q.EnableDeadLetterQueue = true;
               q.MessageTtl            = TimeSpan.FromHours(24);
           }, onConflict: QueueConflictResolution.Fail)

           .DeclareQueue("notifications",
               onConflict: QueueConflictResolution.Ignore,
               failOnError: false);
   });

Creating Queues Manually
========================

You can also create individual queues at any point after connecting:

.. code-block:: csharp

   using VibeMQ.Configuration;
   using VibeMQ.Enums;

   // Create queue with default options
   await client.CreateQueueAsync("my-queue");

   // Create queue with custom options
   var options = new QueueOptions {
       Mode = DeliveryMode.FanOutWithAck,
       MaxQueueSize = 5000,
       EnableDeadLetterQueue = true,
       MaxRetryAttempts = 5
   };
   await client.CreateQueueAsync("my-queue", options);

.. note::

   Prefer :ref:`queue declarations <queue-declarations>` for queues that must exist at startup.
   Use ``CreateQueueAsync`` for queues created dynamically at runtime.

   ``GetQueueInfoAsync``, ``DeleteQueueAsync``, and ``ListQueuesAsync`` are also available on
   ``IVibeMQClient``. When ``EnableAutoCreate`` is enabled on the server, queues are created
   automatically on the first publish even if not declared.

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
       2925,
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
       2925,
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

   await using var client = await VibeMQClient.ConnectAsync("localhost", 2925);

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

   await using var publisher = await VibeMQClient.ConnectAsync("localhost", 2925, new ClientOptions {
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

   await using var subscriber = await VibeMQClient.ConnectAsync("localhost", 2925, new ClientOptions {
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
           await _client.PublishAsync($"events.{eventType}", eventData, new Dictionary<string, string> {
               ["event_type"] = eventType,
               ["timestamp"] = DateTime.UtcNow.ToString("O")
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
       2925,         // Or correct port
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
