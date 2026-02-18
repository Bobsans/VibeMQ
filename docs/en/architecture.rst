=============
Architecture
=============

This guide describes the internal architecture of VibeMQ and its operating principles.

.. contents:: Contents
   :local:
   :depth: 2

Architecture Overview
=====================

VibeMQ is built on a modular principle and consists of several independent components that interact through clearly defined interfaces.

.. code-block:: text

   ┌─────────────────────────────────────────────────────────────┐
   │                      VibeMQ.Server                          │
   ├─────────────────────────────────────────────────────────────┤
   │  ┌──────────┐  ┌──────────────┐  ┌────────────────────┐     │
   │  │TCP Server│◄─┤Connection    │◄─┤Queue Manager       │     │
   │  │          │  │Manager       │  │  - Queues          │     │
   │  └──────────┘  │  - Clients   │  │  - Subscriptions   │     │
   │                │  - Health    │  │  - Delivery modes  │     │
   │                └──────────────┘  └────────────────────┘     │
   │                            │                │               │
   │                ┌───────────▼────────────────▼───────────┐   │
   │                │         Command Dispatcher             │   │
   │                │  - ConnectHandler                      │   │
   │                │  - PublishHandler                      │   │
   │                │  - SubscribeHandler                    │   │
   │                │  - AckHandler                          │   │
   │                └────────────────────────────────────────┘   │
   │                                   │                         │
   │                ┌──────────────────▼────────────────────┐    │
   │                │         Delivery Infrastructure       │    │
   │                │  - AckTracker (retry logic)           │    │
   │                │  - DeadLetterQueue                    │    │
   │                └───────────────────────────────────────┘    │
   └─────────────────────────────────────────────────────────────┘
                                 │
          ┌──────────────────────┼──────────────────────┐
          │                      │                      │
    ┌─────▼─────┐          ┌─────▼─────┐          ┌─────▼─────┐
    │  Client 1 │          │  Client 2 │          │  Client 3 │
    │(Publisher)│          │(Subscriber)│          │(Subscriber)│
    └───────────┘          └───────────┘          └───────────┘

System Components
=================

VibeMQ.Core
-----------

**Purpose:** Basic models, interfaces, and configuration.

**Main types:**

+------------------------+--------------------------------------------------+
| Type                   | Description                                      |
+========================+==================================================+
| ``BrokerMessage``      | Broker message model                             |
+------------------------+--------------------------------------------------+
| ``QueueInfo``          | Queue state information                          |
+------------------------+--------------------------------------------------+
| ``BrokerOptions``      | Server configuration                             |
+------------------------+--------------------------------------------------+
| ``QueueOptions``       | Queue settings                                   |
+------------------------+--------------------------------------------------+
| ``ClientOptions``      | Client settings                                  |
+------------------------+--------------------------------------------------+
| ``IQueueManager``      | Queue management interface                       |
+------------------------+--------------------------------------------------+
| ``IMessageStore``      | Message store interface                          |
+------------------------+--------------------------------------------------+
| ``IAuthenticationService`` | Authentication interface                     |
+------------------------+--------------------------------------------------+
| ``IBrokerMetrics``     | Metrics collection interface                     |
+------------------------+--------------------------------------------------+

**Enumerations:**

- ``DeliveryMode`` — delivery mode (RoundRobin, FanOut, Priority)
- ``MessagePriority`` — message priority (Low, Normal, High, Critical)
- ``OverflowStrategy`` — overflow strategy
- ``FailureReason`` — failure reason
- ``CommandType`` — protocol command type

VibeMQ.Protocol
---------------

**Purpose:** Message serialization and TCP transmission.

**Framing:**

Uses length-prefix approach for message separation in TCP stream:

.. code-block:: text

   [4 bytes: length in Big Endian][N bytes: UTF-8 JSON body]

**Components:**

- ``FrameReader`` — read frames from stream
- ``FrameWriter`` — write frames to stream
- ``WriteBatcher`` — message batching for performance
- ``ProtocolMessage`` — base class for protocol message

**Message format:**

.. code-block:: json

   {
     "id": "msg_123",
     "type": "publish",
     "queue": "notifications",
     "payload": {"title": "Hello", "body": "World"},
     "headers": {
       "priority": "high",
       "correlationId": "corr_456"
     },
     "schemaVersion": "1.0"
   }

VibeMQ.Server
-------------

**Purpose:** Broker server implementation.

**Key components:**

**BrokerServer** — main server class:

.. code-block:: csharp

   public sealed partial class BrokerServer : IAsyncDisposable {
       public IBrokerMetrics Metrics { get; }
       public int ActiveConnections { get; }
       public int InFlightMessages { get; }

       public Task RunAsync(CancellationToken cancellationToken = default);
       public Task StopAsync(CancellationToken cancellationToken = default);
       public ValueTask DisposeAsync();
   }

**BrokerBuilder** — Fluent API for configuration:

.. code-block:: csharp

   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .UseAuthentication("token")
       .ConfigureQueues(options => { ... })
       .Build();

**QueueManager** — queue management:

- Queue creation and deletion
- Message publishing
- Client subscription and unsubscription
- Message acknowledgment

**ConnectionManager** — connection management:

- Active connection tracking
- Message routing to subscribers
- Connection lifecycle management

**CommandDispatcher** — command handling:

- ``ConnectHandler`` — connection establishment
- ``PublishHandler`` — message publishing
- ``SubscribeHandler`` — queue subscription
- ``UnsubscribeHandler`` — queue unsubscription
- ``AckHandler`` — acknowledgment handling
- ``PingHandler`` — keep-alive

**AckTracker** — acknowledgment tracking:

- Unacknowledged message tracking
- Timeouts and retry attempts
- Exponential backoff between retries

**DeadLetterQueue** — failed message queue:

- Store messages with failed delivery
- Re-processing mechanism

**RateLimiter** — rate limiting:

- IP-based rate limiting for connections
- Client-based rate limiting for messages

VibeMQ.Client
-------------

**Purpose:** Client for broker connection.

**VibeMQClient** — main client class:

.. code-block:: csharp

   public sealed partial class VibeMQClient : IAsyncDisposable {
       public bool IsConnected { get; }

       public static Task<VibeMQClient> ConnectAsync(...);
       public Task PublishAsync<T>(string queueName, T payload, ...);
       public Task<IAsyncDisposable> SubscribeAsync<T>(...);
       public Task UnsubscribeAsync(string queueName, ...);
       public Task DisconnectAsync(...);
       public ValueTask DisposeAsync();
   }

**ReconnectPolicy** — reconnection policy:

.. code-block:: csharp

   public sealed class ReconnectPolicy {
       public int MaxAttempts { get; set; } = int.MaxValue;
       public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);
       public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(5);
       public bool UseExponentialBackoff { get; set; } = true;

       public TimeSpan GetDelay(int attempt);
   }

VibeMQ.Health
-------------

**Purpose:** HTTP server for health checks.

**HealthCheckServer** — HTTP server:

.. code-block:: csharp

   public sealed partial class HealthCheckServer : IAsyncDisposable {
       public void Start();
       public ValueTask DisposeAsync();
   }

**Endpoints:**

- ``GET /health/`` — health status (200 OK or 503)
- ``GET /metrics/`` — broker metrics (JSON)

**HealthStatus** — health status:

.. code-block:: json

   {
     "isHealthy": true,
     "status": "healthy",
     "activeConnections": 15,
     "queueCount": 5,
     "inFlightMessages": 42,
     "totalMessagesPublished": 125000,
     "totalMessagesDelivered": 124850,
     "memoryUsageMb": 256,
     "timestamp": "2026-02-18T10:30:00Z"
   }

Operating Principles
====================

Message Lifecycle
-----------------

1. **Publishing:**

   - Client sends ``Publish`` command
   - Server validates message
   - Message is stored in queue
   - ``PublishAck`` confirmation is sent

2. **Routing:**

   - ``QueueManager`` determines delivery mode
   - For Round-robin, next subscriber is selected
   - For Fan-out, message is copied to all subscribers
   - For Priority-based, sorted by priority

3. **Delivery:**

   - Message is sent to subscriber via ``Deliver`` command
   - ACK wait timer is started
   - Message is marked as "in-flight"

4. **Acknowledgment:**

   - Subscriber sends ``Ack`` command
   - ``AckTracker`` marks message as delivered
   - Message is removed from in-flight
   - Metrics are updated

5. **Retry (if no ACK):**

   - Timer expires
   - Attempt counter is incremented
   - If attempts not exhausted — resend
   - If exhausted — move to Dead Letter Queue

Delivery Modes
--------------

**Round-robin:**

.. code-block:: text

   Publisher → Queue → Subscriber 1 (message 1)
                     → Subscriber 2 (message 2)
                     → Subscriber 1 (message 3)
                     → Subscriber 2 (message 4)

Each message is delivered to one subscriber cyclically.

**Fan-out with acknowledgment:**

.. code-block:: text

   Publisher → Queue → Subscriber 1 (copy 1, ACK required)
                     → Subscriber 2 (copy 1, ACK required)
                     → Subscriber 3 (copy 1, ACK required)

Message is delivered to all subscribers, each must acknowledge.

**Fan-out without acknowledgment:**

.. code-block:: text

   Publisher → Queue → Subscriber 1 (copy 1)
                     → Subscriber 2 (copy 1)
                     → Subscriber 3 (copy 1)

Message is delivered to all without acknowledgment.

**Priority-based:**

.. code-block:: text

   Queue: [Critical:1] [High:2] [High:3] [Normal:4] [Low:5]

   Delivery: Critical → High → High → Normal → Low

Messages are delivered by priority.

Keep-alive Mechanism
--------------------

PING/PONG mechanism is used to maintain active connections:

.. code-block:: text

   Client                          Server
      │                              │
      │────── PING (every 30s) ─────▶│
      │                              │
      │◀───── PONG (immediate) ─────│
      │                              │

If server doesn't receive PING within timeout, connection is closed.

Automatic Reconnections
-----------------------

Client automatically reconnects on connection loss:

.. code-block:: text

   Attempt 1: wait 1s
   Attempt 2: wait 2s
   Attempt 3: wait 4s
   Attempt 4: wait 8s
   Attempt 5: wait 16s
   ...
   Attempt N: wait 5min (maximum)

Exponential backoff is used with 5 minute maximum.

Graceful Shutdown
-----------------

On server stop, graceful shutdown is performed:

1. Stop accepting new connections
2. Send shutdown notification to clients
3. Wait for in-flight message processing (up to 30s)
4. Close all connections
5. Clean up resources

Memory Management
=================

Backpressure
------------

When memory usage reaches high level:

1. **Watermark 80%:** Backpressure enabled
2. **Watermark 90%:** New publications blocked
3. **Watermark 95%:** Overflow strategy applied

Overflow strategies:

- **DropOldest** — drop oldest message
- **DropNewest** — reject new message
- **BlockPublisher** — block publisher
- **RedirectToDlq** — redirect to DLQ

Object Pool
-----------

Message object pool is used to reduce allocations:

.. code-block:: csharp

   public class MessageObjectPool {
       private readonly ConcurrentBag<BrokerMessage> _pool = new();

       public BrokerMessage Rent() { ... }
       public void Return(BrokerMessage message) { ... }
   }

Metrics and Monitoring
======================

Collected metrics:

**Counters:**

- ``TotalMessagesPublished`` — total published
- ``TotalMessagesDelivered`` — total delivered
- ``TotalMessagesAcknowledged`` — total acknowledged
- ``TotalRetries`` — total retries
- ``TotalDeadLettered`` — total in DLQ
- ``TotalErrors`` — total errors
- ``TotalConnectionsAccepted`` — total connections
- ``TotalConnectionsRejected`` — total rejections

**Gauge metrics:**

- ``ActiveConnections`` — active connections
- ``ActiveQueues`` — active queues
- ``InFlightMessages`` — in-flight messages
- ``MemoryUsageBytes`` — memory usage

**Latency:**

- ``AverageDeliveryLatencyMs`` — average delivery latency

Next Steps
==========

- :doc:`features` — detailed feature overview
- :doc:`protocol` — communication protocol details
- :doc:`monitoring` — monitoring and metrics
