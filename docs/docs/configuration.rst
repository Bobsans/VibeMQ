===============
Configuration
===============

This guide describes all configuration parameters for VibeMQ.

.. contents:: Contents
   :local:
   :depth: 2

Server Configuration
====================

BrokerOptions
-------------

Main server configuration class:

.. code-block:: csharp

   public sealed class BrokerOptions {
       public int Port { get; set; } = 8080;
       public int MaxConnections { get; set; } = 1000;
       public int MaxMessageSize { get; set; } = 1_048_576;
       public bool EnableAuthentication { get; set; }
       public string? AuthToken { get; set; }
       public QueueDefaults QueueDefaults { get; set; } = new();
       public TlsOptions Tls { get; set; } = new();
       public RateLimitOptions RateLimit { get; set; } = new();
       public StorageType StorageType { get; set; } = StorageType.InMemory;
   }

**StorageType** (default: ``InMemory``) selects the persistence backend: ``InMemory`` (no durability) or ``Sqlite`` (durable, single-node). See :doc:`storage` for details.

Basic Parameters
----------------

Port
~~~~

**Type:** ``int``

**Default:** ``8080``

TCP port for client connections:

.. code-block:: csharp

   .UsePort(8080)

.. note::

   Make sure the port is not occupied by other applications.

MaxConnections
~~~~~~~~~~~~~~

**Type:** ``int``

**Default:** ``1000``

Maximum number of simultaneous connections:

.. code-block:: csharp

   .UseMaxConnections(5000)

.. note::

   Increase for high-load scenarios.

MaxMessageSize
~~~~~~~~~~~~~~

**Type:** ``int``

**Default:** ``1_048_576`` (1 MB)

Maximum message size in bytes:

.. code-block:: csharp

   .UseMaxMessageSize(2_097_152)  // 2 MB

.. warning::

   Large messages increase memory usage.

EnableAuthentication
~~~~~~~~~~~~~~~~~~~~

**Type:** ``bool``

**Default:** ``false``

Enable authentication:

.. code-block:: csharp

   .UseAuthentication("my-secret-token")

AuthToken
~~~~~~~~~

**Type:** ``string?``

**Default:** ``null``

Token for client authentication:

.. code-block:: csharp

   .UseAuthentication("complex-token-with-32-chars-min")

.. warning::

   Use complex tokens (32+ characters) in production.

QueueDefaults
-------------

Server-side default settings for auto-created queues (used by ``ConfigureQueues``). Only these three properties are configurable as defaults; per-queue options (MessageTtl, DeadLetterQueue, OverflowStrategy, MaxRetryAttempts) are set via ``QueueOptions`` when creating a queue explicitly with ``CreateQueueAsync`` on the client or when the queue is created with custom options.

.. code-block:: csharp

   public sealed class QueueDefaults {
       public DeliveryMode DefaultDeliveryMode { get; set; } = DeliveryMode.RoundRobin;
       public int MaxQueueSize { get; set; } = 10_000;
       public bool EnableAutoCreate { get; set; } = true;
   }

DefaultDeliveryMode
~~~~~~~~~~~~~~~~~~~

**Type:** ``DeliveryMode``

**Default:** ``RoundRobin``

Default delivery mode:

.. code-block:: csharp

   options.DefaultDeliveryMode = DeliveryMode.RoundRobin;

**Possible values:**

- ``RoundRobin`` — round-robin delivery to one subscriber
- ``FanOutWithAck`` — to all with acknowledgment
- ``FanOutWithoutAck`` — to all without acknowledgment
- ``PriorityBased`` — by priority

MaxQueueSize
~~~~~~~~~~~~

**Type:** ``int``

**Default:** ``10_000``

Maximum number of messages in queue:

.. code-block:: csharp

   options.MaxQueueSize = 100_000;

EnableAutoCreate
~~~~~~~~~~~~~~~~

**Type:** ``bool``

**Default:** ``true``

Automatically create queues on first publish:

.. code-block:: csharp

   options.EnableAutoCreate = true;

.. note::

   Disable for strict queue control. Per-queue options (MessageTtl, EnableDeadLetterQueue, OverflowStrategy, MaxRetryAttempts) are available in ``QueueOptions`` when creating a queue via ``client.CreateQueueAsync(name, options)``.

TlsOptions
----------

TLS/SSL settings:

.. code-block:: csharp

   public sealed class TlsOptions {
       public bool Enabled { get; set; }
       public string? CertificatePath { get; set; }
       public string? CertificatePassword { get; set; }
       public SslProtocols SslProtocols { get; set; } = SslProtocols.Tls12 | SslProtocols.Tls13;
       public bool RequireClientCertificate { get; set; }
   }

Enabled
~~~~~~~

**Type:** ``bool``

**Default:** ``false``

Enable TLS:

.. code-block:: csharp

   .UseTls(options => {
       options.Enabled = true;
       options.CertificatePath = "/path/to/cert.pfx";
       options.CertificatePassword = "cert-password";
   })

CertificatePath
~~~~~~~~~~~~~~~

**Type:** ``string?``

**Default:** ``null``

Path to PFX certificate:

.. code-block:: csharp

   options.CertificatePath = "/etc/ssl/vibemq.pfx";

CertificatePassword
~~~~~~~~~~~~~~~~~~~

**Type:** ``string?``

**Default:** ``null``

Certificate password:

.. code-block:: csharp

   options.CertificatePassword = "secure-password";

RateLimitOptions
----------------

Rate limiting settings:

.. code-block:: csharp

   public sealed class RateLimitOptions {
       public bool Enabled { get; set; } = true;
       public int MaxConnectionsPerIpPerWindow { get; set; } = 20;
       public TimeSpan ConnectionWindow { get; set; } = TimeSpan.FromSeconds(60);
       public int MaxMessagesPerClientPerSecond { get; set; } = 1000;
   }

Enabled
~~~~~~~

**Type:** ``bool``

**Default:** ``true``

Enable rate limiting:

.. code-block:: csharp

   .ConfigureRateLimiting(options => {
       options.Enabled = true;
   })

MaxConnectionsPerIpPerWindow
~~~~~~~~~~~~~~~~~~~~~~~~~~~~

**Type:** ``int``

**Default:** ``20``

Maximum number of connections from one IP per time window:

.. code-block:: csharp

   options.MaxConnectionsPerIpPerWindow = 50;

ConnectionWindow
~~~~~~~~~~~~~~~~

**Type:** ``TimeSpan``

**Default:** ``60 seconds``

Time window for connection limiting:

.. code-block:: csharp

   options.ConnectionWindow = TimeSpan.FromSeconds(120);

MaxMessagesPerClientPerSecond
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

**Type:** ``int``

**Default:** ``1000``

Maximum number of messages from client per second:

.. code-block:: csharp

   options.MaxMessagesPerClientPerSecond = 500;

Client Configuration
====================

ClientOptions
-------------

.. code-block:: csharp

   public sealed class ClientOptions {
       public string? AuthToken { get; set; }
       public ReconnectPolicy ReconnectPolicy { get; set; } = new();
       public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(30);
       public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(10);
       public bool UseTls { get; set; }
       public bool SkipCertificateValidation { get; set; }
   }

AuthToken
~~~~~~~~~

**Type:** ``string?``

**Default:** ``null``

Token for authentication:

.. code-block:: csharp

   AuthToken = "my-secret-token"

ReconnectPolicy
~~~~~~~~~~~~~~~

**Type:** ``ReconnectPolicy``

Reconnection policy:

.. code-block:: csharp

   ReconnectPolicy = new ReconnectPolicy {
       MaxAttempts = 10,
       InitialDelay = TimeSpan.FromSeconds(1),
       MaxDelay = TimeSpan.FromMinutes(5),
       UseExponentialBackoff = true
   }

KeepAliveInterval
~~~~~~~~~~~~~~~~~

**Type:** ``TimeSpan``

**Default:** ``30 seconds``

PING send interval:

.. code-block:: csharp

   KeepAliveInterval = TimeSpan.FromSeconds(60)

CommandTimeout
~~~~~~~~~~~~~~

**Type:** ``TimeSpan``

**Default:** ``10 seconds``

Command timeout:

.. code-block:: csharp

   CommandTimeout = TimeSpan.FromSeconds(30)

UseTls
~~~~~~

**Type:** ``bool``

**Default:** ``false``

Use TLS:

.. code-block:: csharp

   UseTls = true

SkipCertificateValidation
~~~~~~~~~~~~~~~~~~~~~~~~~

**Type:** ``bool``

**Default:** ``false``

Skip certificate validation:

.. code-block:: csharp

   SkipCertificateValidation = true  // Tests only!

.. warning::

   Do not use in production!

ReconnectPolicy
---------------

.. code-block:: csharp

   public sealed class ReconnectPolicy {
       public int MaxAttempts { get; set; } = int.MaxValue;
       public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);
       public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(5);
       public bool UseExponentialBackoff { get; set; } = true;
   }

MaxAttempts
~~~~~~~~~~~

**Type:** ``int``

**Default:** ``int.MaxValue``

Maximum number of attempts:

.. code-block:: csharp

   MaxAttempts = 50

InitialDelay
~~~~~~~~~~~~

**Type:** ``TimeSpan``

**Default:** ``1 second``

Initial delay between attempts:

.. code-block:: csharp

   InitialDelay = TimeSpan.FromSeconds(2)

MaxDelay
~~~~~~~~

**Type:** ``TimeSpan``

**Default:** ``5 minutes``

Maximum delay:

.. code-block:: csharp

   MaxDelay = TimeSpan.FromMinutes(1)

UseExponentialBackoff
~~~~~~~~~~~~~~~~~~~~~

**Type:** ``bool``

**Default:** ``true``

Use exponential backoff:

.. code-block:: csharp

   UseExponentialBackoff = true

Health Check Configuration
=========================

HealthCheckOptions
------------------

.. code-block:: csharp

   public sealed class HealthCheckOptions {
       public bool Enabled { get; set; } = true;
       public int Port { get; set; } = 8081;
   }

Enabled
~~~~~~~

**Type:** ``bool``

**Default:** ``true``

Enable health check server:

.. code-block:: csharp

   .ConfigureHealthChecks(options => {
       options.Enabled = true;
   })

Port
~~~~

**Type:** ``int``

**Default:** ``8081``

HTTP port for health checks:

.. code-block:: csharp

   .ConfigureHealthChecks(options => {
       options.Port = 9090;
   })

Configuration Examples
=====================

Minimal
-------

.. code-block:: csharp

   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .Build();

Development
-----------

.. code-block:: csharp

   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .UseAuthentication("dev-token")
       .ConfigureQueues(options => {
           options.DefaultDeliveryMode = DeliveryMode.RoundRobin;
           options.MaxQueueSize = 10_000;
           options.EnableAutoCreate = true;
       })
       .Build();

Production
----------

.. code-block:: csharp

   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .UseAuthentication(Environment.GetEnvironmentVariable("VIBEMQ_TOKEN"))
       .UseMaxConnections(5000)
       .UseMaxMessageSize(2_097_152)
       .ConfigureQueues(options => {
           options.DefaultDeliveryMode = DeliveryMode.FanOutWithAck;
           options.MaxQueueSize = 100_000;
           options.EnableAutoCreate = true;
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
       .ConfigureHealthChecks(options => {
           options.Enabled = true;
           options.Port = 8081;
       })
       .Build();

IoT Scenario
------------

.. code-block:: csharp

   var broker = BrokerBuilder.Create()
       .UsePort(8883)
       .UseAuthentication("iot-token")
       .UseMaxConnections(10000)
       .ConfigureQueues(options => {
           options.DefaultDeliveryMode = DeliveryMode.RoundRobin;
           options.MaxQueueSize = 1000;
       })
       .ConfigureRateLimiting(options => {
           options.Enabled = true;
           options.MaxConnectionsPerIpPerWindow = 500;
           options.MaxMessagesPerClientPerSecond = 100;
       })
       .Build();

Microservices
------------

.. code-block:: csharp

   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .UseAuthentication("microservice-token")
       .ConfigureQueues(options => {
           options.DefaultDeliveryMode = DeliveryMode.FanOutWithAck;
           options.EnableAutoCreate = true;
           options.MaxQueueSize = 10_000;
       })
       .ConfigureHealthChecks(options => {
           options.Enabled = true;
           options.Port = 8081;
       })
       .Build();

Next Steps
==========

- :doc:`server-setup` — server setup
- :doc:`di-integration` — DI integration
- :doc:`monitoring` — monitoring
