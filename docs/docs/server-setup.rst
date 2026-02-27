============
Server Setup
============

This guide describes various ways to configure and run the VibeMQ server.

For running the broker in Docker with environment-based configuration, see :doc:`docker`. For an optional Web dashboard (health, metrics, queues), see :doc:`web-ui`.

.. contents:: Contents
   :local:
   :depth: 2

Basic Configuration
=================

Minimal Configuration
------------------------

Simplest way to start server:

.. code-block:: csharp

   using VibeMQ.Server;

   var broker = BrokerBuilder.Create()
       .UsePort(2925)
       .Build();

   await broker.RunAsync(CancellationToken.None);

This code will start server on port 2925 without authentication and with default settings.

Advanced Configuration
------------------------

.. code-block:: csharp

   using Microsoft.Extensions.Logging;
   using VibeMQ.Server;
   using VibeMQ.Enums;

   using var loggerFactory = LoggerFactory.Create(builder => {
       builder.SetMinimumLevel(LogLevel.Information).AddConsole();
   });

   var broker = BrokerBuilder.Create()
       .UsePort(2925)
       .UseAuthentication("my-secret-token")
       .UseMaxConnections(1000)
       .UseMaxMessageSize(1_048_576)  // 1 MB
       .ConfigureQueues(options => {
           options.DefaultDeliveryMode = DeliveryMode.RoundRobin;
           options.MaxQueueSize = 10_000;
           options.EnableAutoCreate = true;
       })
       .ConfigureRateLimiting(options => {
           options.Enabled = true;
           options.MaxConnectionsPerIpPerWindow = 50;
           options.MaxMessagesPerClientPerSecond = 1000;
       })
       .ConfigureHealthChecks(options => {
           options.Enabled = true;
           options.Port = 2926;
       })
       .UseTls(options => {
           options.Enabled = false;  // Enable for production
           options.CertificatePath = "/path/to/cert.pfx";
           options.CertificatePassword = "cert-password";
       })
       .UseLoggerFactory(loggerFactory)
       .Build();

   await broker.RunAsync(CancellationToken.None);

Configuration Parameters
======================

Basic Parameters
------------------

.. list-table::
   :header-rows: 1
   :widths: 34 18 38

   * - Parameter
     - Default
     - Description
   * - ``Port``
     - 2925
     - TCP port for client connections
   * - ``MaxConnections``
     - 1000
     - Maximum number of connections
   * - ``MaxMessageSize``
     - 1 MB
     - Maximum message size
   * - ``EnableAuthentication``
     - false
     - Enable legacy token authentication
   * - ``AuthToken``
     - null
     - Token for legacy authentication (deprecated)
   * - ``Authorization``
     - null
     - Enable username/password auth with per-queue ACL (see :doc:`authorization`)

Default Queue Settings
-------------------------------

.. list-table::
   :header-rows: 1
   :widths: 34 18 38

   * - Parameter
     - Default
     - Description
   * - ``DefaultDeliveryMode``
     - RoundRobin
     - Default delivery mode
   * - ``MaxQueueSize``
     - 10,000
     - Maximum queue size
   * - ``EnableAutoCreate``
     - true
     - Automatic queue creation

Per-queue options (MessageTtl, EnableDeadLetterQueue, MaxRetryAttempts, OverflowStrategy) are set via ``QueueOptions`` when creating a queue with ``client.CreateQueueAsync(name, options)``; see :doc:`client-usage`.

Rate Limiting
-------------

.. list-table::
   :header-rows: 1
   :widths: 34 18 38

   * - Parameter
     - Default
     - Description
   * - ``Enabled``
     - true
     - Enable rate limiting
   * - ``MaxConnectionsPerIpPerWindow``
     - 20
     - Max connections from IP per window
   * - ``ConnectionWindow``
     - 60 sec
     - Time window (TimeSpan)
   * - ``MaxMessagesPerClientPerSecond``
     - 1000
     - Max messages from client per sec

TLS/SSL Settings
-----------------

.. list-table::
   :header-rows: 1
   :widths: 34 18 38

   * - Parameter
     - Default
     - Description
   * - ``Enabled``
     - false
     - Enable TLS
   * - ``CertificatePath``
     - null
     - Path to PFX certificate
   * - ``CertificatePassword``
     - null
     - Certificate password

Health Check Settings
----------------------

.. list-table::
   :header-rows: 1
   :widths: 34 18 38

   * - Parameter
     - Default
     - Description
   * - ``Enabled``
     - true
     - Enable health check server
   * - ``Port``
     - 2926
     - HTTP port for health checks

Delivery Modes
===============

Round-robin
-----------

Each message is delivered to one subscriber cyclically:

.. code-block:: csharp

   .ConfigureQueues(options => {
       options.DefaultDeliveryMode = DeliveryMode.RoundRobin;
   });

**Usage:**

- Task processing by multiple workers
- Load balancing
- Task queues

Fan-out with Acknowledgment
------------------------

Message delivered to all subscribers with delivery guarantee:

.. code-block:: csharp

   .ConfigureQueues(options => {
       options.DefaultDeliveryMode = DeliveryMode.FanOutWithAck;
   });

**Usage:**

- Notification broadcasting
- Data replication
- Audit and logging

Fan-out without Acknowledgment
-------------------------

Message delivered to all subscribers without acknowledgment:

.. code-block:: csharp

   .ConfigureQueues(options => {
       options.DefaultDeliveryMode = DeliveryMode.FanOutWithoutAck;
   });

**Usage:**

- Broadcast messages
- Real-time updates
- Data streaming

Priority-based
--------------

Delivery by message priority:

.. code-block:: csharp

   .ConfigureQueues(options => {
       options.DefaultDeliveryMode = DeliveryMode.PriorityBased;
   });

**Priorities:**

- ``Critical`` — critical (delivered first)
- ``High`` — high
- ``Normal`` — normal (default)
- ``Low`` — low (delivered last)

**Publishing example:**

.. code-block:: csharp

   using VibeMQ.Enums;

   await client.PublishAsync("alerts", message, new Dictionary<string, string> {
       ["priority"] = MessagePriority.Critical.ToString()
   });

Overflow Strategies
======================

DropOldest
----------

Remove oldest message on overflow:

.. code-block:: csharp

   // Set via QueueOptions when creating queue: client.CreateQueueAsync("q", new QueueOptions { OverflowStrategy = OverflowStrategy.DropOldest });

**When to use:**

- Fresh data is important
- Old messages lose relevance

DropNewest
----------

Reject new message:

.. code-block:: csharp

   .ConfigureQueues(options => {
       options.OverflowStrategy = OverflowStrategy.DropNewest;
   });

**When to use:**

- All existing messages are important
- New messages can wait

BlockPublisher
--------------

Block publisher until space is available:

.. code-block:: csharp

   // Set via QueueOptions when creating queue: client.CreateQueueAsync("q", new QueueOptions { OverflowStrategy = OverflowStrategy.BlockPublisher });

**When to use:**

- Message loss is unacceptable
- Publisher can wait

RedirectToDlq
-------------

Redirect to Dead Letter Queue:

.. code-block:: csharp

   // Set via QueueOptions when creating queue: client.CreateQueueAsync("q", new QueueOptions { OverflowStrategy = OverflowStrategy.RedirectToDlq, EnableDeadLetterQueue = true, DeadLetterQueueName = "dlq" });

**When to use:**

- All messages must be preserved
- Subsequent processing planned

Dead Letter Queue
=================

DLQ Configuration:

.. code-block:: csharp

   // Set via QueueOptions when creating queue: client.CreateQueueAsync("q", new QueueOptions { EnableDeadLetterQueue = true, DeadLetterQueueName = "dead-letters", MaxRetryAttempts = 3 });

**Reasons for DLQ:**

- Exceeded maximum delivery attempts
- Message TTL expired
- Deserialization error
- Exception in handler

**Getting messages from DLQ:** DLQ messages are persisted by the broker's storage provider. To consume them you can subscribe to the Dead Letter Queue by name (if the broker exposes it as a queue) or use a custom component that has access to the storage provider's ``GetDeadLetteredMessagesAsync`` API. See :doc:`storage` for storage provider details.

Authorization (Username/Password + ACL)
=======================================

The recommended authentication mode for production. Uses BCrypt-hashed passwords
and per-queue ACL stored in a dedicated SQLite database:

.. code-block:: csharp

   var broker = BrokerBuilder.Create()
       .UsePort(2925)
       .UseAuthorization(o => {
           o.SuperuserUsername = "admin";
           o.SuperuserPassword = Environment.GetEnvironmentVariable("VIBEMQ_ADMIN_PASS");
           o.DatabasePath = "/var/lib/vibemq/auth.db";
       })
       .Build();

**On client:**

.. code-block:: csharp

   var client = await VibeMQClient.ConnectAsync(
       "localhost",
       2925,
       new ClientOptions {
           Username = "alice",
           Password = "alice-secret"
       }
   );

See :doc:`authorization` for full details (users, ACL patterns, admin commands).

Legacy Token Authentication
============================

.. deprecated::

   Use ``UseAuthorization()`` for new deployments.

Enabling Authentication:

**On client:**

.. code-block:: csharp

   var client = await VibeMQClient.ConnectAsync(
       "localhost",
       2925,
       new ClientOptions {
           AuthToken = "my-secret-token"
       }
   );

.. warning::

   Use complex tokens in production (minimum 32 characters).
   Store tokens securely (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault).

TLS/SSL Encryption
==================

TLS Configuration:

.. code-block:: csharp

   .UseTls(options => {
       options.Enabled = true;
       options.CertificatePath = "/path/to/cert.pfx";
       options.CertificatePassword = "cert-password";
   })

**Creating self-signed certificate (for tests):**

.. code-block:: bash

   dotnet dev-certs https -ep vibemq.pfx -p cert-password
   dotnet dev-certs https --trust

**On client:**

.. code-block:: csharp

   var client = await VibeMQClient.ConnectAsync(
       "localhost",
       2925,
       new ClientOptions {
           UseTls = true,
           // Tests only!
           SkipCertificateValidation = true
       }
   );

.. warning::

   Do not use ``SkipCertificateValidation = true`` in production!

Starting and Stopping
==================

Starting Server
--------------

**Async start:**

.. code-block:: csharp

   await broker.RunAsync(cancellationToken);

**Start with signal handling:**

.. code-block:: csharp

   var cts = new CancellationTokenSource();
   Console.CancelKeyPress += (_, e) => {
       e.Cancel = true;
       cts.Cancel();
   };

   await broker.RunAsync(cts.Token);

Stopping Server
-----------------

**Graceful stop:**

.. code-block:: csharp

   await broker.StopAsync(cancellationToken);

**Resource cleanup:**

.. code-block:: csharp

   await broker.DisposeAsync();

**Using statement:**

.. code-block:: csharp

   await using var broker = BrokerBuilder.Create()
       .UsePort(2925)
       .Build();

   await broker.RunAsync(cancellationToken);
   // DisposeAsync called automatically

Logging
===========

Logging Configuration:

.. code-block:: csharp

   using Microsoft.Extensions.Logging;

   using var loggerFactory = LoggerFactory.Create(builder => {
       builder
           .SetMinimumLevel(LogLevel.Debug)
           .AddConsole()
           .AddDebug()
           .AddFile("logs/vibemq-.log");  // Requires Microsoft.Extensions.Logging.File package
   });

   var broker = BrokerBuilder.Create()
       .UsePort(2925)
       .UseLoggerFactory(loggerFactory)
       .Build();

**Log levels:**

- ``Trace`` — detailed debugging
- ``Debug`` — debug information
- ``Information`` — informational messages
- ``Warning`` — warnings
- ``Error`` — errors
- ``Critical`` — critical errors

Configuration Examples
====================

Minimal Server for Development
---------------------------------

.. code-block:: csharp

   var broker = BrokerBuilder.Create()
       .UsePort(2925)
       .Build();

   await broker.RunAsync(CancellationToken.None);

Production Server
---------------------

.. code-block:: csharp

   var broker = BrokerBuilder.Create()
       .UsePort(2925)
       .UseAuthentication(Environment.GetEnvironmentVariable("VIBEMQ_TOKEN"))
       .UseMaxConnections(5000)
       .ConfigureQueues(options => {
           options.DefaultDeliveryMode = DeliveryMode.RoundRobin;
           options.MaxQueueSize = 100_000;
           options.EnableAutoCreate = true;
       })
       .ConfigureRateLimiting(options => {
           options.Enabled = true;
           options.MaxConnectionsPerIpPerWindow = 50;
           options.MaxMessagesPerClientPerSecond = 5000;
       })
       .UseTls(options => {
           options.Enabled = true;
           options.CertificatePath = "/etc/ssl/vibemq.pfx";
           options.CertificatePassword = Environment.GetEnvironmentVariable("CERT_PASSWORD");
       })
       .UseLoggerFactory(loggerFactory)
       .Build();

Microservices Server
------------------------

.. code-block:: csharp

   var broker = BrokerBuilder.Create()
       .UsePort(2925)
       .UseAuthentication("microservice-token")
       .ConfigureQueues(options => {
           options.DefaultDeliveryMode = DeliveryMode.FanOutWithAck;
           options.EnableAutoCreate = true;
           options.MaxQueueSize = 10_000;
       })
       .ConfigureHealthChecks(options => {
           options.Enabled = true;
           options.Port = 2926;
       })
       .Build();

IoT Server
--------------

.. code-block:: csharp

   var broker = BrokerBuilder.Create()
       .UsePort(8883)  // Standard MQTT port
       .UseAuthentication("iot-token")
       .UseMaxConnections(10000)
       .ConfigureQueues(options => {
           options.DefaultDeliveryMode = DeliveryMode.RoundRobin;
           options.MaxQueueSize = 1000;
       })
       .ConfigureRateLimiting(options => {
           options.Enabled = true;
           options.MaxConnectionsPerIpPerWindow = 500;
           options.MaxMessagesPerClientPerSecond = 100;  // Limit for devices
       })
       .Build();

Next Steps
==========

- :doc:`client-usage` — client usage
- :doc:`configuration` — detailed configuration
- :doc:`monitoring` — monitoring and health checks
- :doc:`di-integration` — DI integration
