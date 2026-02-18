==========================
Frequently Asked Questions
==========================

This guide answers common questions about VibeMQ.

.. contents:: Contents
   :local:
   :depth: 2

General Questions
=============

What is VibeMQ?
-----------------

**VibeMQ** is a simple but reliable message broker for .NET applications. It supports:

- Publish/subscribe (pub/sub)
- Queues with delivery guarantees
- Automatic reconnections
- Token-based authentication
- TLS/SSL encryption
- Health checks for orchestrators

How is VibeMQ different from RabbitMQ/Kafka?
-----------------------------------------

**VibeMQ vs RabbitMQ:**

- VibeMQ is simpler to configure and use
- Does not require external dependencies (databases, etc.)
- Written in C# for .NET ecosystem
- Less functionality, but sufficient for most scenarios

**VibeMQ vs Kafka:**

- VibeMQ is not a distributed event log
- Does not support persistent storage (in current version)
- Simpler to deploy and use
- Better suited for task queues and real-time notifications

When to use VibeMQ?
--------------------------

**Good scenarios:**

- Task queues for background processing
- Notifications between microservices
- Real-time updates
- Event-driven architecture
- Distributed systems on .NET

**Not suitable for:**

- Storing large volumes of data
- Real-time streaming (use Kafka)
- Complex message routing (use RabbitMQ)
- Transactional messages

What .NET versions are supported?
---------------------------------

- **.NET 8.0** (main target platform)
- **.NET 10.0** (support planned)

Technical Questions
===================

How does delivery guarantee work?
-------------------------------

VibeMQ uses acknowledgment (ACK) mechanism:

1. Broker sends message to subscriber
2. ACK wait timer starts
3. Subscriber processes message and sends ACK
4. If ACK not received — retry delivery
5. After exhausting attempts — Dead Letter Queue

What happens when server restarts?
---------------------------------------

**Current version:**

- Messages in queues are lost (in-memory storage)
- Connections must be restored

**Planned:**

- Persistence layer for message storage
- State recovery after restart

How to scale VibeMQ?
--------------------------

**Horizontal scaling:**

1. Run multiple server instances
2. Use load balancer
3. Clients connect to nearest server

**Vertical scaling:**

- Increase limits (MaxConnections, MaxQueueSize)
- Add resources (CPU, RAM)

**Planned:**

- Clustering for automatic scaling

How to ensure security?
----------------------------

**Authentication:**

.. code-block:: csharp

   .UseAuthentication("my-secret-token")

**TLS encryption:**

.. code-block:: csharp

   .UseTls(options => {
       options.Enabled = true;
       options.CertificatePath = "/path/to/cert.pfx";
   })

**Rate limiting:**

.. code-block:: csharp

   .ConfigureRateLimiting(options => {
       options.Enabled = true;
       options.MaxConnectionsPerIpPerWindow = 100;
   })

What delivery modes are supported?
-------------------------------------

- **Round-robin** — to each subscriber in turn
- **Fan-out with ACK** — to all with acknowledgment
- **Fan-out without ACK** — to all without acknowledgment
- **Priority-based** — by priority

How does Dead Letter Queue work?
-------------------------------

DLQ stores messages that could not be delivered:

.. code-block:: csharp

   .ConfigureQueues(options => {
       options.EnableDeadLetterQueue = true;
       options.MaxRetryAttempts = 3;
   });

**Reasons for DLQ:**

- Exceeded delivery attempt count
- Message TTL expired
- Deserialization error
- Exception in handler

Deployment
=============

How to run in Docker?
-----------------------

**Dockerfile:**

.. code-block:: dockerfile

   FROM mcr.microsoft.com/dotnet/runtime:8.0
   WORKDIR /app
   EXPOSE 8080 8081
   COPY . .
   ENTRYPOINT ["dotnet", "VibeMQ.Server.dll"]

**Run:**

.. code-block:: bash

   docker build -t vibemq-server .
   docker run -p 8080:8080 -p 8081:8081 vibemq-server

How to use with Kubernetes?
------------------------------

**Deployment:**

.. code-block:: yaml

   apiVersion: apps/v1
   kind: Deployment
   metadata:
     name: vibemq
   spec:
     replicas: 3
     selector:
       matchLabels:
         app: vibemq
     template:
       spec:
         containers:
         - name: vibemq
           image: vibemq-server:latest
           ports:
           - containerPort: 8080
           - containerPort: 8081
           livenessProbe:
             httpGet:
               path: /health/
               port: 8081
             initialDelaySeconds: 10
             periodSeconds: 10

How to configure for production?
-----------------------------

**Recommendations:**

.. code-block:: csharp

   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .UseAuthentication(Environment.GetEnvironmentVariable("VIBEMQ_TOKEN"))
       .UseMaxConnections(5000)
       .ConfigureQueues(options => {
           options.DefaultDeliveryMode = DeliveryMode.FanOutWithAck;
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
       })
       .ConfigureHealthChecks(options => {
           options.Enabled = true;
           options.Port = 8081;
       })
       .Build();

Performance
==================

What is VibeMQ performance?
--------------------------------

**Benchmarks:**

- 10,000+ messages/sec on single node
- Latency < 10ms for 95% of messages
- Support for 1000+ simultaneous connections

**Factors affecting performance:**

- Message size
- Number of subscribers
- Delivery mode
- Network latency

How to optimize performance?
--------------------------------------

**On server:**

.. code-block:: csharp

   .UseMaxConnections(5000)  // Increase limit
   .ConfigureQueues(options => {
       options.MaxQueueSize = 100_000;  // Larger queue
       options.OverflowStrategy = OverflowStrategy.DropOldest;  // Fast strategy
   })

**On client:**

.. code-block:: csharp

   new ClientOptions {
       KeepAliveInterval = TimeSpan.FromSeconds(60),  // Less frequent PING
       CommandTimeout = TimeSpan.FromSeconds(5)  // Lower timeout
   }

**General recommendations:**

- Use batching for publishing
- Optimize message handlers
- Monitor performance metrics

Security
============

How secure is token authentication?
---------------------------------------------

**Recommendations:**

- Use complex tokens (32+ characters)
- Store tokens in secure location (Key Vault, Secrets Manager)
- Rotate tokens periodically
- Use different tokens for different environments

.. warning::

   Token authentication is suitable for internal security.
   For public APIs use OAuth2/OIDC.

Do I need to use TLS?
--------------------------

**Yes, if:**

- Messages contain sensitive data
- Network is untrusted
- Security compliance required

**No, if:**

- All services in trusted network
- TLS terminates at load balancer level

How to protect against DDoS?
---------------------

**Rate limiting:**

.. code-block:: csharp

   .ConfigureRateLimiting(options => {
       options.Enabled = true;
       options.MaxConnectionsPerIpPerWindow = 50;
       options.MaxMessagesPerClientPerSecond = 100;
   })

**Additionally:**

- Use firewall
- Limit number of connections
- Monitor anomalies

Integration
==========

How to use with ASP.NET Core?
--------------------------------

.. code-block:: csharp

   using VibeMQ.Server.DependencyInjection;
   using VibeMQ.Client.DependencyInjection;

   var builder = WebApplication.CreateBuilder(args);

   // Server
   builder.Services.AddVibeMQBroker(options => {
       options.Port = 8080;
   });

   // Client
   builder.Services.AddVibeMQClient(settings => {
       settings.Host = "localhost";
       settings.Port = 8080;
   });

   var app = builder.Build();
   await app.RunAsync();

How to use with Worker Service?
----------------------------------

.. code-block:: csharp

   using VibeMQ.Client.DependencyInjection;

   var host = Host.CreateDefaultBuilder(args)
       .ConfigureServices(services => {
           services.AddHostedService<Worker>();
           services.AddVibeMQClient(settings => {
               settings.Host = "localhost";
               settings.Port = 8080;
           });
       })
       .Build();

   await host.RunAsync();

Can I use with other languages?
----------------------------------------

**Current version:** Only .NET clients

**Planned:**

- Clients for Java, Python, Node.js
- AMQP/MQTT protocol support

Troubleshooting
==================

Why are messages lost?
--------------------------

**Causes:**

- Server restarted (in-memory storage)
- Message TTL expired
- Queue size exceeded

**Solution:**

.. code-block:: csharp

   .ConfigureQueues(options => {
       options.EnableDeadLetterQueue = true;  // Save failed messages
       options.MessageTtl = TimeSpan.FromHours(24);  // Increase TTL
       options.MaxQueueSize = 100_000;  // Increase size
   })

Why high latency?
------------------------

**Causes:**

- Server overload
- Slow handlers
- Network issues

**Solution:**

- Optimize handlers
- Increase server resources
- Check network

Why frequent disconnections?
-------------------------

**Causes:**

- Network issues
- Keep-alive timeout
- Server restarting

**Solution:**

.. code-block:: csharp

   new ClientOptions {
       ReconnectPolicy = new ReconnectPolicy {
           MaxAttempts = 50,  // Increase attempts
           UseExponentialBackoff = true
       },
       KeepAliveInterval = TimeSpan.FromSeconds(30)  // Check keep-alive
   }

Next Steps
==========

- :doc:`getting-started` — quick start
- :doc:`troubleshooting` — troubleshooting
- :doc:`examples` — usage examples
