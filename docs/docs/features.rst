============
Features
============

This guide describes all capabilities and features of VibeMQ.

.. contents:: Contents
   :local:
   :depth: 2

Publish/Subscribe (Pub/Sub)
============================

VibeMQ implements the publish/subscribe pattern through message queues.

**Basic concepts:**

- **Publisher** — client sending messages
- **Subscriber** — client receiving messages
- **Queue** — buffer for storing messages
- **Broker** — server managing queues

**Basic example:**

.. code-block:: csharp

   // Publisher
   await client.PublishAsync("notifications", new {
       Title = "Hello",
       Body = "World"
   });

   // Subscriber
   await using var subscription = await client.SubscribeAsync<dynamic>(
       "notifications",
       async msg => {
           Console.WriteLine($"{msg.Title}: {msg.Body}");
       }
   );

Delivery Modes
==============

VibeMQ supports four message delivery modes:

Round-robin
-----------

**Description:** Each message is delivered to one subscriber cyclically.

.. code-block:: text

   Publisher → [Queue] → Subscriber 1 (message 1)
                       → Subscriber 2 (message 2)
                       → Subscriber 1 (message 3)

**Configuration:**

.. code-block:: csharp

   options.DefaultDeliveryMode = DeliveryMode.RoundRobin;

**Use cases:**

- Task processing by multiple workers
- Load balancing
- Task queues

Fan-out with Acknowledgment
---------------------------

**Description:** Message is delivered to all subscribers, each must acknowledge receipt.

.. code-block:: text

   Publisher → [Queue] → Subscriber 1 (copy + ACK)
                       → Subscriber 2 (copy + ACK)
                       → Subscriber 3 (copy + ACK)

**Configuration:**

.. code-block:: csharp

   options.DefaultDeliveryMode = DeliveryMode.FanOutWithAck;
   options.MaxRetryAttempts = 3;

**Use cases:**

- Notification broadcasting
- Data replication
- Audit and logging

Fan-out without Acknowledgment
------------------------------

**Description:** Message is delivered to all subscribers without confirmation.

.. code-block:: text

   Publisher → [Queue] → Subscriber 1 (copy)
                       → Subscriber 2 (copy)
                       → Subscriber 3 (copy)

**Configuration:**

.. code-block:: csharp

   options.DefaultDeliveryMode = DeliveryMode.FanOutWithoutAck;

**Use cases:**

- Broadcast messages
- Real-time updates
- Data streaming

Priority-based
--------------

**Description:** Messages are delivered by priority.

.. code-block:: text

   [Critical] → [High] → [Normal] → [Low]

**Priorities:**

+----------------+----------+------------------------------------------+
| Priority       | Value    | Description                              |
+================+==========+==========================================+
| Critical       | 3        | Critical, delivered first                |
+----------------+----------+------------------------------------------+
| High           | 2        | High priority                            |
+----------------+----------+------------------------------------------+
| Normal         | 1        | Normal (default)                         |
+----------------+----------+------------------------------------------+
| Low            | 0        | Low, delivered last                      |
+----------------+----------+------------------------------------------+

**Configuration:**

.. code-block:: csharp

   options.DefaultDeliveryMode = DeliveryMode.PriorityBased;

**Publish message:**

.. code-block:: csharp

   await client.PublishAsync("alerts", message);

**Publish with priority:**

.. code-block:: csharp

   using VibeMQ.Core.Enums;

   await client.PublishAsync("alerts", message, new Dictionary<string, string> {
       ["priority"] = MessagePriority.Critical.ToString()
   });

Delivery Guarantees
===================

Acknowledgments (ACK)
---------------------

VibeMQ uses an acknowledgment mechanism for delivery guarantees:

.. code-block:: text

   Broker → Deliver message → Client
                               │
                               │ Processing...
                               │
          ◀────── ACK ─────────┘

**How it works:**

1. Broker sends message to client
2. ACK wait timer starts
3. Client processes message and sends ACK
4. Broker receives ACK and marks message as delivered

**Automatic ACKs:**

By default, the client automatically sends ACK after successful processing:

.. code-block:: csharp

   await using var subscription = await client.SubscribeAsync<dynamic>(
       "notifications",
       async msg => {
           await ProcessMessageAsync(msg);
           // ACK is sent automatically
       }
   );

Retry Attempts
--------------

If ACK is not received within the timeout, the message is resent:

.. code-block:: text

   Attempt 1 → Timeout → Attempt 2 → Timeout → Attempt 3 → DLQ

**Configuration:**

.. code-block:: csharp

   options.MaxRetryAttempts = 3;

**Exponential backoff:**

Exponential delay is used between attempts:

- Attempt 1: immediately
- Attempt 2: after 1s
- Attempt 3: after 2s
- Attempt 4: after 4s
- ...

Dead Letter Queue (DLQ)
-----------------------

Messages that fail to deliver after all attempts are moved to Dead Letter Queue:

.. code-block:: csharp

   options.EnableDeadLetterQueue = true;
   options.DeadLetterQueueName = "dead-letters";
   options.MaxRetryAttempts = 3;

**Reasons for DLQ:**

- Maximum delivery attempts exceeded
- Message TTL expired
- Deserialization error
- Exception in handler

**DLQ processing:**

.. code-block:: csharp

   var dlqMessages = await queueManager.GetDeadLetterMessagesAsync(100);

   foreach (var message in dlqMessages) {
       // Retry or log
       await RetryOrLogAsync(message);
   }

Queue Management
================

Creating a Queue
----------------

**Automatic creation:**

When publishing to a non-existent queue, it's created automatically:

.. code-block:: csharp

   options.EnableAutoCreate = true;

**Manual creation:**

.. code-block:: csharp

   await queueManager.CreateQueueAsync("my-queue", new QueueOptions {
       Mode = DeliveryMode.RoundRobin,
       MaxQueueSize = 10_000,
       MessageTtl = TimeSpan.FromHours(1),
   });

Deleting a Queue
----------------

.. code-block:: csharp

   await queueManager.DeleteQueueAsync("my-queue");

Getting Information
-------------------

``GetQueueInfoAsync`` returns a complete snapshot of a queue's current state, including all
configuration settings:

.. code-block:: csharp

   var info = await client.GetQueueInfoAsync("orders");

   if (info is null) {
       Console.WriteLine("Queue does not exist.");
       return;
   }

   // Runtime state
   Console.WriteLine($"Name:             {info.Name}");
   Console.WriteLine($"Messages:         {info.MessageCount}");
   Console.WriteLine($"Subscribers:      {info.SubscriberCount}");
   Console.WriteLine($"Created:          {info.CreatedAt:u}");

   // Configuration
   Console.WriteLine($"Mode:             {info.DeliveryMode}");
   Console.WriteLine($"Max size:         {info.MaxSize}");
   Console.WriteLine($"Message TTL:      {info.MessageTtl?.ToString() ?? "none"}");
   Console.WriteLine($"DLQ enabled:      {info.EnableDeadLetterQueue}");
   Console.WriteLine($"DLQ name:         {info.DeadLetterQueueName ?? "auto"}");
   Console.WriteLine($"Overflow:         {info.OverflowStrategy}");
   Console.WriteLine($"Max retries:      {info.MaxRetryAttempts}");

**QueueInfo fields:**

.. list-table::
   :header-rows: 1
   :widths: 26 22 44

   * - Field
     - Type
     - Description
   * - ``Name``
     - ``string``
     - Queue name.
   * - ``MessageCount``
     - ``int``
     - Messages currently waiting in the queue.
   * - ``SubscriberCount``
     - ``int``
     - Number of active subscribers.
   * - ``CreatedAt``
     - ``DateTime``
     - UTC timestamp of queue creation.
   * - ``DeliveryMode``
     - ``DeliveryMode``
     - Delivery mode configured for the queue.
   * - ``MaxSize``
     - ``int``
     - Maximum message capacity.
   * - ``MessageTtl``
     - ``TimeSpan?``
     - Per-message time-to-live. ``null`` = no TTL.
   * - ``EnableDeadLetterQueue``
     - ``bool``
     - Whether a DLQ is enabled.
   * - ``DeadLetterQueueName``
     - ``string?``
     - DLQ name. ``null`` = auto-generated.
   * - ``OverflowStrategy``
     - ``OverflowStrategy``
     - Action taken when the queue is full.
   * - ``MaxRetryAttempts``
     - ``int``
     - Delivery attempts before DLQ routing.

List Queues
-----------

.. code-block:: csharp

   var queues = await client.ListQueuesAsync();

   foreach (var queueName in queues) {
       Console.WriteLine(queueName);
   }

Queue Declarations
------------------

Instead of calling ``CreateQueueAsync`` manually, you can declare all queues your application
needs as part of ``ClientOptions``. On every ``ConnectAsync`` the client:

1. Creates any queue that does not yet exist.
2. Compares the declared settings against the live queue and classifies each difference as
   ``Info``, ``Soft``, or ``Hard``.
3. Applies the ``OnConflict`` strategy (``Ignore``, ``Fail``, or ``Override``) for any
   ``Soft`` or ``Hard`` differences found.

.. code-block:: csharp

   await using var client = await VibeMQClient.ConnectAsync(
       "localhost",
       8080,
       new ClientOptions()
           // Production queue — any drift is a deploy error
           .DeclareQueue("orders", q => {
               q.Mode                  = DeliveryMode.FanOutWithAck;
               q.MaxQueueSize          = 50_000;
               q.EnableDeadLetterQueue = true;
               q.MessageTtl            = TimeSpan.FromHours(24);
           }, onConflict: QueueConflictResolution.Fail)

           // Analytics — drift is acceptable
           .DeclareQueue("analytics-events", q => {
               q.MaxQueueSize     = 200_000;
               q.OverflowStrategy = OverflowStrategy.DropOldest;
           })
   );

See :doc:`client-usage` for a full description of conflict resolution, the ``QueueConflictException``
type, and DI integration.

Message Priorities
==================

VibeMQ supports message priorities for important deliveries.

**Priority levels:**

.. code-block:: csharp

   public enum MessagePriority {
       Low = 0,      // Low
       Normal = 1,   // Normal (default)
       High = 2,     // High
       Critical = 3  // Critical
   }

**Publish messages:**

.. code-block:: csharp

   using VibeMQ.Core.Enums;

   // Critical message with priority
   await client.PublishAsync("alerts", alertData, new Dictionary<string, string> {
       ["priority"] = MessagePriority.Critical.ToString()
   });

   // High priority notification
   await client.PublishAsync("notifications", data, new Dictionary<string, string> {
       ["priority"] = MessagePriority.High.ToString()
   });

   // Normal priority (default) - no headers needed
   await client.PublishAsync("logs", logData);

Keep-alive (PING/PONG)
======================

Active connections are maintained using the keep-alive mechanism:

.. code-block:: text

   Client                    Server
      │                         │
      │─── PING (30s) ─────────▶│
      │                         │
      │◄── PONG (immediate) ────│

**Client configuration:**

.. code-block:: csharp

   var client = await VibeMQClient.ConnectAsync(
       "localhost",
       8080,
       new ClientOptions {
           KeepAliveInterval = TimeSpan.FromSeconds(30)
       }
   );

Automatic Reconnections
=======================

The client automatically reconnects when the connection is lost.

**Reconnect policy configuration:**

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

**Parameters:**

+---------------------------+------------------+------------------------------------------+
| Parameter                 | Default          | Description                              |
+===========================+==================+==========================================+
| ``MaxAttempts``           | int.MaxValue     | Maximum number of attempts               |
+---------------------------+------------------+------------------------------------------+
| ``InitialDelay``          | 1s               | Initial delay                            |
+---------------------------+------------------+------------------------------------------+
| ``MaxDelay``              | 5min             | Maximum delay                            |
+---------------------------+------------------+------------------------------------------+
| ``UseExponentialBackoff`` | true             | Exponential increase                     |
+---------------------------+------------------+------------------------------------------+

Authentication
==============

Token-based authentication:

**On server:**

.. code-block:: csharp

   var broker = BrokerBuilder.Create()
       .UseAuthentication("my-secret-token")
       .Build();

**On client:**

.. code-block:: csharp

   var client = await VibeMQClient.ConnectAsync(
       "localhost",
       8080,
       new ClientOptions {
           AuthToken = "my-secret-token"
       }
   );

.. warning::

   Use complex tokens (32+ characters) and store them in a secure location.

TLS/SSL Encryption
==================

Transport encryption support:

**On server:**

.. code-block:: csharp

   .UseTls(options => {
       options.Enabled = true;
       options.CertificatePath = "/path/to/cert.pfx";
       options.CertificatePassword = "cert-password";
   })

**On client:**

.. code-block:: csharp

   var client = await VibeMQClient.ConnectAsync(
       "localhost",
       8080,
       new ClientOptions {
           UseTls = true,
           SkipCertificateValidation = false  // Only for tests!
       }
   );

Rate Limiting
=============

Overload protection:

**Configuration:**

.. code-block:: csharp

   .ConfigureRateLimiting(options => {
       options.Enabled = true;
       options.MaxConnectionsPerIpPerWindow = 20;
       options.ConnectionWindow = TimeSpan.FromSeconds(60);
       options.MaxMessagesPerClientPerSecond = 1000;
   });

**Parameters:**

+-----------------------------------+------------------+------------------------------------------+
| Parameter                         | Default          | Description                              |
+===================================+==================+==========================================+
| ``Enabled``                       | true             | Enable rate limiting                     |
+-----------------------------------+------------------+------------------------------------------+
| ``MaxConnectionsPerIpPerWindow``  | 20               | Max connections per IP per window        |
+-----------------------------------+------------------+------------------------------------------+
| ``ConnectionWindow``              | 60s              | Time window (seconds)                    |
+-----------------------------------+------------------+------------------------------------------+
| ``MaxMessagesPerClientPerSecond`` | 1000             | Max messages per second per client       |
+-----------------------------------+------------------+------------------------------------------+

Graceful Shutdown
=================

Correct server shutdown without message loss:

.. code-block:: csharp

   var cts = new CancellationTokenSource();
   Console.CancelKeyPress += (_, e) => {
       e.Cancel = true;
       cts.Cancel();
   };

   await broker.RunAsync(cts.Token);
   // StopAsync() is called automatically

**Shutdown stages:**

1. Stop accepting new connections
2. Notify clients about shutdown
3. Wait for in-flight message processing (up to 30s)
4. Close all connections
5. Clean up resources

Health Checks
=============

HTTP endpoints for monitoring:

**Enable:**

.. code-block:: csharp

   .ConfigureHealthChecks(options => {
       options.Enabled = true;
       options.Port = 8081;
   })

**Endpoints:**

- ``GET /health/`` — health status (200 OK or 503)
- ``GET /metrics/`` — broker metrics (JSON)

**Example /health/ response:**

.. code-block:: json

   {
     "status": "healthy",
     "active_connections": 15,
     "queue_count": 5,
     "memory_usage_mb": 256
   }

**Example /metrics/ response:**

.. code-block:: json

   {
     "total_messages_published": 125000,
     "total_messages_delivered": 124850,
     "total_messages_acknowledged": 124800,
     "active_connections": 15,
     "active_queues": 5,
     "memory_usage_bytes": 268435456,
     "average_delivery_latency_ms": 2.5,
     "timestamp": "2026-02-18T10:30:00Z",
     "uptime": "02:15:30.5000000"
   }

Metrics
=======

**Counters:**

- ``TotalMessagesPublished`` — total published
- ``TotalMessagesDelivered`` — total delivered
- ``TotalMessagesAcknowledged`` — total acknowledged
- ``TotalRetries`` — retry attempts
- ``TotalDeadLettered`` — in DLQ
- ``TotalErrors`` — errors
- ``TotalConnectionsAccepted`` — connections accepted
- ``TotalConnectionsRejected`` — connections rejected

**Gauge metrics:**

- ``ActiveConnections`` — active connections
- ``ActiveQueues`` — active queues
- ``InFlightMessages`` — in flight
- ``MemoryUsageBytes`` — memory usage

**Latency:**

- ``AverageDeliveryLatencyMs`` — average delivery latency

Persistence & Storage
=====================

VibeMQ supports pluggable storage providers for message durability across server restarts.

**Default behavior (InMemory):**

All state is kept in memory — zero configuration, maximum performance. All data is lost on restart.

**With SQLite storage:**

.. code-block:: bash

   dotnet add package VibeMQ.Server.Storage.Sqlite

.. code-block:: csharp

   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .UseSqliteStorage(options => {
           options.DatabasePath = "/data/vibemq.db";
       })
       .Build();

**How it works:**

1. When a message is published it is saved to storage *before* entering the in-memory queue (write-ahead).
2. When the subscriber acknowledges the message it is removed from storage.
3. On server restart, all queues and unacknowledged messages are recovered automatically.

**DLQ persistence:**

Failed messages are saved to the ``dead_letters`` table before being added to the in-memory DLQ so they survive restarts.

**Storage providers:**

+--------------------------------------+--------------------------------------------+
| Provider                             | Description                                |
+======================================+============================================+
| ``InMemoryStorageProvider``          | Default. Fast, no durability.              |
+--------------------------------------+--------------------------------------------+
| ``SqliteStorageProvider``            | SQLite-based, single-file DB, zero-config. |
+--------------------------------------+--------------------------------------------+

See :doc:`storage` for full configuration reference, database schema, and custom provider guide.

Next Steps
==========

- :doc:`server-setup` — server configuration
- :doc:`client-usage` — client usage
- :doc:`storage` — persistence & storage
- :doc:`monitoring` — monitoring
