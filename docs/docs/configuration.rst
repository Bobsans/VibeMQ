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
   }

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

Default queue settings:

.. code-block:: csharp

   public sealed class QueueDefaults {
       public DeliveryMode DefaultDeliveryMode { get; set; } = DeliveryMode.RoundRobin;
       public int MaxQueueSize { get; set; } = 10_000;
       public bool EnableAutoCreate { get; set; } = true;
       public TimeSpan? MessageTtl { get; set; }
       public bool EnableDeadLetterQueue { get; set; } = false;
       public string? DeadLetterQueueName { get; set; }
       public OverflowStrategy OverflowStrategy { get; set; } = OverflowStrategy.DropOldest;
       public int MaxRetryAttempts { get; set; } = 3;
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

   Disable for strict queue control.

MessageTtl
~~~~~~~~~~

**Type:** ``TimeSpan?``

**Default:** ``null`` (no limit)

Message time-to-live:

.. code-block:: csharp

   options.MessageTtl = TimeSpan.FromHours(24);

.. note::

   Expired messages are automatically removed.

EnableDeadLetterQueue
~~~~~~~~~~~~~~~~~~~~~

**Type:** ``bool``

**Default:** ``false``

Enable Dead Letter Queue:

.. code-block:: csharp

   options.EnableDeadLetterQueue = true;

DeadLetterQueueName
~~~~~~~~~~~~~~~~~~~

**Type:** ``string?``

**Default:** ``null`` (auto-name)

Dead Letter Queue name:

.. code-block:: csharp

   options.DeadLetterQueueName = "dead-letters";

OverflowStrategy
~~~~~~~~~~~~~~~~

**Type:** ``OverflowStrategy``

**Default:** ``DropOldest``

Queue overflow strategy:

.. code-block:: csharp

   options.OverflowStrategy = OverflowStrategy.DropOldest;

**Possible values:**

- ``DropOldest`` — remove oldest message
- ``DropNewest`` — reject new message
- ``BlockPublisher`` — block publisher
- ``RedirectToDlq`` — redirect to DLQ

MaxRetryAttempts
~~~~~~~~~~~~~~~~

**Type:** ``int``

**Default:** ``3``

Maximum number of delivery attempts:

.. code-block:: csharp

   options.MaxRetryAttempts = 5;

TlsOptions
----------

TLS/SSL settings:

.. code-block:: csharp

   public sealed class TlsOptions {
       public bool Enabled { get; set; }
       public string? CertificatePath { get; set; }
       public string? CertificatePassword { get; set; }
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
       public bool Enabled { get; set; }
       public int MaxConnectionsPerIpPerWindow { get; set; } = 100;
       public int ConnectionWindowSeconds { get; set; } = 60;
       public int MaxMessagesPerClientPerSecond { get; set; } = 1000;
   }

Enabled
~~~~~~~

**Type:** ``bool``

**Default:** ``false``

Enable rate limiting:

.. code-block:: csharp

   .ConfigureRateLimiting(options => {
       options.Enabled = true;
   })

MaxConnectionsPerIpPerWindow
~~~~~~~~~~~~~~~~~~~~~~~~~~~~

**Type:** ``int``

**Default:** ``100``

Maximum number of connections from one IP per time window:

.. code-block:: csharp

   options.MaxConnectionsPerIpPerWindow = 50;

ConnectionWindowSeconds
~~~~~~~~~~~~~~~~~~~~~~~

**Type:** ``int``

**Default:** ``60``

Time window for connection limiting (seconds):

.. code-block:: csharp

   options.ConnectionWindowSeconds = 120;

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
           options.EnableDeadLetterQueue = true;
           options.MaxRetryAttempts = 5;
           options.MessageTtl = TimeSpan.FromDays(1);
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
           options.MessageTtl = TimeSpan.FromSeconds(60);
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
           options.MessageTtl = TimeSpan.FromMinutes(30);
           options.EnableDeadLetterQueue = true;
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
