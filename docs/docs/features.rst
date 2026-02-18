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

**Publish with priority:**

.. code-block:: csharp

   await client.PublishAsync("alerts", message, options => {
       options.Priority = MessagePriority.Critical;
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
       DeliveryMode = DeliveryMode.RoundRobin,
       MaxQueueSize = 10_000,
       MessageTtl = TimeSpan.FromHours(1),
   });

Deleting a Queue
----------------

.. code-block:: csharp

   await queueManager.DeleteQueueAsync("my-queue");

Getting Information
-------------------

.. code-block:: csharp

   var info = await queueManager.GetQueueInfoAsync("my-queue");

   Console.WriteLine($"Queue: {info.Name}");
   Console.WriteLine($"Messages: {info.MessageCount}");
   Console.WriteLine($"Subscribers: {info.SubscriberCount}");
   Console.WriteLine($"Mode: {info.DeliveryMode}");

List Queues
-----------

.. code-block:: csharp

   var queues = await queueManager.ListQueuesAsync();

   foreach (var queueName in queues) {
       Console.WriteLine(queueName);
   }

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

**Publish with priority:**

.. code-block:: csharp

   // Critical message
   await client.PublishAsync("alerts", alertData, options => {
       options.Priority = MessagePriority.Critical;
   });

   // High priority
   await client.PublishAsync("notifications", data, options => {
       options.Priority = MessagePriority.High;
   });

   // Normal priority (default)
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

+------------------------+------------------+------------------------------------------+
| Parameter              | Default          | Description                              |
+========================+==================+==========================================+
| ``MaxAttempts``        | int.MaxValue     | Maximum number of attempts               |
+------------------------+------------------+------------------------------------------+
| ``InitialDelay``       | 1s               | Initial delay                            |
+------------------------+------------------+------------------------------------------+
| ``MaxDelay``           | 5min             | Maximum delay                            |
+------------------------+------------------+------------------------------------------+
| ``UseExponentialBackoff`` | true          | Exponential increase                     |
+------------------------+------------------+------------------------------------------+

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
       options.MaxConnectionsPerIpPerWindow = 100;
       options.ConnectionWindowSeconds = 60;
       options.MaxMessagesPerClientPerSecond = 1000;
   });

**Parameters:**

+------------------------+------------------+------------------------------------------+
| Parameter              | Default          | Description                              |
+========================+==================+==========================================+
| ``Enabled``            | false            | Enable rate limiting                     |
+------------------------+------------------+------------------------------------------+
| ``MaxConnectionsPerIpPerWindow`` | 100    | Max connections per IP per window        |
+------------------------+------------------+------------------------------------------+
| ``ConnectionWindowSeconds`` | 60          | Time window (seconds)                    |
+------------------------+------------------+------------------------------------------+
| ``MaxMessagesPerClientPerSecond`` | 1000  | Max messages per second per client       |
+------------------------+------------------+------------------------------------------+

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
     "total_acknowledged": 124800,
     "active_connections": 15,
     "active_queues": 5,
     "memory_usage_bytes": 268435456,
     "average_delivery_latency_ms": 2.5
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

Next Steps
==========

- :doc:`server-setup` — server configuration
- :doc:`client-usage` — client usage
- :doc:`monitoring` — monitoring
