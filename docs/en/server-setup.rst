============
Server Setup
============

This guide describes various ways to configure and run the VibeMQ server.

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
       .UsePort(8080)
       .Build();

   await broker.RunAsync(CancellationToken.None);

This code will start server on port 8080 without authentication and with default settings.

Advanced Configuration
------------------------

.. code-block:: csharp

   using Microsoft.Extensions.Logging;
   using VibeMQ.Server;
   using VibeMQ.Core.Enums;

   using var loggerFactory = LoggerFactory.Create(builder => {
       builder.SetMinimumLevel(LogLevel.Information).AddConsole();
   });

   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .UseAuthentication("my-secret-token")
       .UseMaxConnections(1000)
       .UseMaxMessageSize(1_048_576)  // 1 MB
       .ConfigureQueues(options => {
           options.DefaultDeliveryMode = DeliveryMode.RoundRobin;
           options.MaxQueueSize = 10_000;
           options.EnableAutoCreate = true;
           options.MessageTtl = TimeSpan.FromHours(24);
           options.EnableDeadLetterQueue = true;
           options.MaxRetryAttempts = 3;
       })
       .ConfigureRateLimiting(options => {
           options.Enabled = true;
           options.MaxConnectionsPerIpPerWindow = 50;
           options.MaxMessagesPerClientPerSecond = 1000;
       })
       .ConfigureHealthChecks(options => {
           options.Enabled = true;
           options.Port = 8081;
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

+------------------------+------------------+----------------------------------+
| Parameter               | Default          | Description                       |
+========================+==================+==================================+
| ``Port``               | 8080             | TCP port for client connections  |
+------------------------+------------------+----------------------------------+
| ``MaxConnections``     | 1000             | Maximum number of connections    |
+------------------------+------------------+----------------------------------+
| ``MaxMessageSize``     | 1 MB             | Maximum message size              |
+------------------------+------------------+----------------------------------+
| ``EnableAuthentication`` | false          | Enable authentication             |
+------------------------+------------------+----------------------------------+
| ``AuthToken``          | null             | Token for authentication          |
+------------------------+------------------+----------------------------------+

Default Queue Settings
-------------------------------

+------------------------+------------------+----------------------------------+
| Parameter               | Default          | Description                       |
+========================+==================+==================================+
| ``DefaultDeliveryMode``| RoundRobin       | Default delivery mode             |
+------------------------+------------------+----------------------------------+
| ``MaxQueueSize``       | 10,000           | Maximum queue size                |
+------------------------+------------------+----------------------------------+
| ``EnableAutoCreate``   | true             | Automatic queue creation          |
+------------------------+------------------+----------------------------------+
| ``MessageTtl``         | null             | Message time-to-live (TTL)        |
+------------------------+------------------+----------------------------------+
| ``EnableDeadLetterQueue`` | false         | Enable DLQ                        |
+------------------------+------------------+----------------------------------+
| ``MaxRetryAttempts``   | 3                | Max delivery attempts              |
+------------------------+------------------+----------------------------------+
| ``OverflowStrategy``   | DropOldest       | Overflow strategy                 |
+------------------------+------------------+----------------------------------+

Rate Limiting
-------------

+------------------------+------------------+----------------------------------+
| Parameter               | Default          | Description                       |
+========================+==================+==================================+
| ``Enabled``            | false            | Enable rate limiting              |
+------------------------+------------------+----------------------------------+
| ``MaxConnectionsPerIpPerWindow`` | 100    | Max connections from IP per window |
+------------------------+------------------+----------------------------------+
| ``ConnectionWindowSeconds`` | 60          | Time window for connections (sec) |
+------------------------+------------------+----------------------------------+
| ``MaxMessagesPerClientPerSecond`` | 1000  | Max messages from client per sec  |
+------------------------+------------------+----------------------------------+

TLS/SSL Settings
-----------------

+------------------------+------------------+----------------------------------+
| Parameter               | Default          | Description                       |
+========================+==================+==================================+
| ``Enabled``            | false            | Enable TLS                        |
+------------------------+------------------+----------------------------------+
| ``CertificatePath``    | null             | Path to PFX certificate           |
+------------------------+------------------+----------------------------------+
| ``CertificatePassword``| null             | Certificate password               |
+------------------------+------------------+----------------------------------+

Health Check Settings
----------------------

+------------------------+------------------+----------------------------------+
| Parameter               | Default          | Description                       |
+========================+==================+==================================+
| ``Enabled``            | true             | Enable health check server        |
+------------------------+------------------+----------------------------------+
| ``Port``               | 8081             | HTTP port for health checks       |
+------------------------+------------------+----------------------------------+

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
       options.MaxRetryAttempts = 3;
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

   await client.PublishAsync("alerts", message, options => {
       options.Priority = MessagePriority.Critical;
   });

Overflow Strategies
======================

DropOldest
----------

Remove oldest message on overflow:

.. code-block:: csharp

   .ConfigureQueues(options => {
       options.OverflowStrategy = OverflowStrategy.DropOldest;
   });

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

   .ConfigureQueues(options => {
       options.OverflowStrategy = OverflowStrategy.BlockPublisher;
   });

**When to use:**

- Message loss is unacceptable
- Publisher can wait

RedirectToDlq
-------------

Redirect to Dead Letter Queue:

.. code-block:: csharp

   .ConfigureQueues(options => {
       options.OverflowStrategy = OverflowStrategy.RedirectToDlq;
       options.EnableDeadLetterQueue = true;
       options.DeadLetterQueueName = "dlq";
   });

**When to use:**

- All messages must be preserved
- Subsequent processing planned

Dead Letter Queue
=================

DLQ Configuration:

.. code-block:: csharp

   .ConfigureQueues(options => {
       options.EnableDeadLetterQueue = true;
       options.DeadLetterQueueName = "dead-letters";
       options.MaxRetryAttempts = 3;
   });

**Reasons for DLQ:**

- Exceeded maximum delivery attempts
- Message TTL expired
- Deserialization error
- Exception in handler

**Getting messages from DLQ:**

.. code-block:: csharp

   var dlqMessages = await queueManager.GetDeadLetterMessagesAsync(100);
   
   foreach (var message in dlqMessages) {
       // Process failed message
   }

Authentication
==============

Enabling Authentication:

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
       8080,
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
       .UsePort(8080)
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
       .UsePort(8080)
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
       .UsePort(8080)
       .Build();

   await broker.RunAsync(CancellationToken.None);

Production Server
---------------------

.. code-block:: csharp

   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .UseAuthentication(Environment.GetEnvironmentVariable("VIBEMQ_TOKEN"))
       .UseMaxConnections(5000)
       .ConfigureQueues(options => {
           options.DefaultDeliveryMode = DeliveryMode.RoundRobin;
           options.MaxQueueSize = 100_000;
           options.EnableDeadLetterQueue = true;
           options.MaxRetryAttempts = 5;
       })
       .ConfigureRateLimiting(options => {
           options.Enabled = true;
           options.MaxConnectionsPerIpPerWindow = 100;
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
       .UsePort(8080)
       .UseAuthentication("microservice-token")
       .ConfigureQueues(options => {
           options.DefaultDeliveryMode = DeliveryMode.FanOutWithAck;
           options.EnableAutoCreate = true;
           options.MessageTtl = TimeSpan.FromMinutes(30);
       })
       .ConfigureHealthChecks(options => {
           options.Enabled = true;
           options.Port = 8081;
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
           options.MaxQueueSize = 1000;  // Small size to save memory
           options.MessageTtl = TimeSpan.FromSeconds(60);  // Short TTL
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
