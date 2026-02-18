===============
Troubleshooting
===============

This guide will help solve common problems when using VibeMQ.

.. contents:: Contents
   :local:
   :depth: 2

Connection Issues
====================

"Connection refused"
--------------------

**Error:**

.. code-block:: text

   System.Net.Sockets.SocketException: Connection refused

**Causes:**

1. Server not running
2. Wrong port
3. Firewall blocking connection
4. Server listening only on localhost

**Solution:**

.. code-block:: csharp

   // Check that server is running
   var broker = BrokerBuilder.Create()
       .UsePort(8080)  // Correct port
       .Build();
   
   await broker.RunAsync(cancellationToken);

   // Check that client connects to correct port
   var client = await VibeMQClient.ConnectAsync("localhost", 8080);

**Check:**

.. code-block:: bash

   # Check that port is listening
   netstat -an | grep 8080

   # PowerShell
   Get-NetTCPConnection -LocalPort 8080

"Connection timeout"
--------------------

**Error:**

.. code-block:: text

   System.TimeoutException: Connection timeout

**Causes:**

1. Network latency
2. Server overloaded
3. Wrong timeout

**Solution:**

.. code-block:: csharp

   var client = await VibeMQClient.ConnectAsync(
       "localhost",
       8080,
       new ClientOptions {
           CommandTimeout = TimeSpan.FromSeconds(30)  // Increase timeout
       }
   );

"Host unreachable"
------------------

**Error:**

.. code-block:: text

   System.Net.Sockets.SocketException: Host unreachable

**Causes:**

1. Wrong host
2. Network issues
3. DNS not resolving

**Solution:**

.. code-block:: csharp

   // Check host
   var client = await VibeMQClient.ConnectAsync(
       "vibemq-server",  // Correct hostname
       8080
   );

**DNS check:**

.. code-block:: bash

   nslookup vibemq-server
   ping vibemq-server

Authentication Issues
=======================

"Authentication failed"
-----------------------

**Error:**

.. code-block:: text

   Authentication failed: Invalid token

**Cause:** Tokens on server and client do not match.

**Solution:**

.. code-block:: csharp

   // Server
   var broker = BrokerBuilder.Create()
       .UseAuthentication("my-secret-token")  // Same token
       .Build();

   // Client
   var client = await VibeMQClient.ConnectAsync(
       "localhost",
       8080,
       new ClientOptions {
           AuthToken = "my-secret-token"  // Same token
       }
   );

.. warning::

   Tokens are case-sensitive!

"Authentication required"
-------------------------

**Error:**

.. code-block:: text

   Authentication required: Token not provided

**Cause:** Server requires authentication, but client did not provide token.

**Solution:**

.. code-block:: csharp

   var client = await VibeMQClient.ConnectAsync(
       "localhost",
       8080,
       new ClientOptions {
           AuthToken = "my-token"  // Provide token
       }
   );

Queue Issues
====================

"Queue not found"
-----------------

**Error:**

.. code-block:: text

   Queue not found: notifications

**Causes:**

1. Queue does not exist
2. Auto-create disabled

**Solution 1:** Enable auto-create:

.. code-block:: csharp

   .ConfigureQueues(options => {
       options.EnableAutoCreate = true;
   });

**Solution 2:** Create queue manually:

.. code-block:: csharp

   await queueManager.CreateQueueAsync("notifications", new QueueOptions {
       DeliveryMode = DeliveryMode.RoundRobin,
       MaxQueueSize = 10_000
   });

"Queue already exists"
----------------------

**Error:**

.. code-block:: text

   Queue already exists: notifications

**Cause:** Attempting to create existing queue.

**Solution:**

.. code-block:: csharp

   try {
       await queueManager.CreateQueueAsync("notifications");
   } catch (QueueAlreadyExistsException) {
       // Queue already exists, this is normal
   }

Message Delivery Issues
==============================

Messages Not Delivered
-------------------------

**Problem:** Messages are published but subscribers do not receive them.

**Possible causes:**

1. Subscriber not subscribed to queue
2. Wrong queue name
3. Network issues

**Solution:**

.. code-block:: csharp

   // Check queue name
   await client.PublishAsync("notifications", message);  // Same name

   await using var subscription = await client.SubscribeAsync<dynamic>(
       "notifications",  // Same name
       async msg => { /* processing */ }
   );

**Check:**

.. code-block:: csharp

   var info = await queueManager.GetQueueInfoAsync("notifications");
   Console.WriteLine($"Subscribers: {info.SubscriberCount}");

Slow Message Delivery
-------------------------------

**Problem:** High delivery latency.

**Causes:**

1. Server overload
2. Slow handlers
3. Network delays

**Solution:**

.. code-block:: csharp

   // Optimize handler
   await using var subscription = await client.SubscribeAsync<dynamic>(
       "notifications",
       async msg => {
           // Fast processing
           await ProcessFastAsync(msg);
           
           // Or async processing in background
           _ = Task.Run(() => ProcessInBackgroundAsync(msg));
       }
   );

**Monitor latency:**

.. code-block:: bash

   curl http://localhost:8081/metrics/ | jq .average_delivery_latency_ms

"Message timeout"
-----------------

**Error:**

.. code-block:: text

   Message timeout: No ACK received

**Cause:** Subscriber did not send ACK within timeout.

**Solution:**

.. code-block:: csharp

   // Increase timeout on server
   .ConfigureQueues(options => {
       options.MessageTtl = TimeSpan.FromMinutes(1);  // Increase TTL
   });

   // Ensure handler is fast
   await using var subscription = await client.SubscribeAsync<dynamic>(
       "notifications",
       async msg => {
           try {
               await ProcessMessageAsync(msg);
               // ACK sent automatically
           } catch (Exception ex) {
               // Error handling
               throw;  // For retry
           }
       }
   );

Memory Issues
==================

"Out of memory"
---------------

**Error:**

.. code-block:: text

   System.OutOfMemoryException

**Causes:**

1. Queues too large
2. Many unacknowledged messages
3. Memory leaks

**Solution:**

.. code-block:: csharp

   // Limit queue sizes
   .ConfigureQueues(options => {
       options.MaxQueueSize = 10_000;  // Limit size
       options.MessageTtl = TimeSpan.FromHours(1);  // Set TTL
   });

   // Enable Dead Letter Queue
   options.EnableDeadLetterQueue = true;
   options.MaxRetryAttempts = 3;

**Monitor memory:**

.. code-block:: bash

   curl http://localhost:8081/health/ | jq .memory_usage_mb

Backpressure
------------

**Problem:** Publications are blocked.

**Cause:** Backpressure enabled due to high memory usage.

**Solution:**

.. code-block:: csharp

   // Increase memory limit or reduce load
   .ConfigureQueues(options => {
       options.MaxQueueSize = 5_000;  // Reduce size
       options.OverflowStrategy = OverflowStrategy.DropOldest;  // Strategy
   });

Reconnection Issues
======================

Frequent Disconnections
-----------------

**Problem:** Client frequently disconnects.

**Causes:**

1. Network issues
2. Server restarting
3. Keep-alive timeout

**Solution:**

.. code-block:: csharp

   var client = await VibeMQClient.ConnectAsync(
       "localhost",
       8080,
       new ClientOptions {
           ReconnectPolicy = new ReconnectPolicy {
               MaxAttempts = 50,  // Increase attempts
               InitialDelay = TimeSpan.FromSeconds(2),
               MaxDelay = TimeSpan.FromMinutes(1),
               UseExponentialBackoff = true
           },
           KeepAliveInterval = TimeSpan.FromSeconds(30)  // Keep-alive
       }
   );

"Max reconnect attempts exceeded"
---------------------------------

**Error:**

.. code-block:: text

   Max reconnect attempts exceeded

**Cause:** Reconnection attempts exhausted.

**Solution:**

.. code-block:: csharp

   // Increase attempts or handle error
   try {
       var client = await VibeMQClient.ConnectAsync(
           "localhost",
           8080,
           new ClientOptions {
               ReconnectPolicy = new ReconnectPolicy {
                   MaxAttempts = 100  // Increase
               }
           }
       );
   } catch (MaxReconnectAttemptsExceededException) {
       // Handle error
       Console.WriteLine("Failed to connect. Check server.");
   }

TLS/SSL Issues
==================

"Certificate validation failed"
-------------------------------

**Error:**

.. code-block:: text

   The remote certificate is invalid according to the validation procedure

**Causes:**

1. Self-signed certificate
2. Certificate expired
3. Wrong hostname

**Solution (tests only):**

.. code-block:: csharp

   var client = await VibeMQClient.ConnectAsync(
       "localhost",
       8080,
       new ClientOptions {
           UseTls = true,
           SkipCertificateValidation = true  // Tests only!
       }
   );

**Solution (production):**

.. code-block:: bash

   # Create valid certificate
   openssl req -x509 -newkey rsa:4096 -keyout key.pem -out cert.pem -days 365

.. warning::

   Do not use ``SkipCertificateValidation = true`` in production!

"TLS handshake failed"
----------------------

**Error:**

.. code-block:: text

   TLS handshake failed

**Causes:**

1. Server not configured for TLS
2. Wrong certificate

**Solution:**

.. code-block:: csharp

   // Server
   .UseTls(options => {
       options.Enabled = true;
       options.CertificatePath = "/path/to/cert.pfx";
       options.CertificatePassword = "password";
   });

   // Client
   new ClientOptions {
       UseTls = true
   };

Rate Limiting Issues
========================

"Rate limit exceeded"
---------------------

**Error:**

.. code-block:: text

   Rate limit exceeded: Too many messages

**Cause:** Message limit exceeded.

**Solution:**

.. code-block:: csharp

   // Increase limit on server
   .ConfigureRateLimiting(options => {
       options.MaxMessagesPerClientPerSecond = 5000;  // Increase
   });

   // Or reduce send frequency on client
   await client.PublishAsync("queue", message);
   await Task.Delay(100);  // Pause between messages

"Too many connections"
----------------------

**Error:**

.. code-block:: text

   Too many connections from this IP

**Cause:** Connection limit exceeded.

**Solution:**

.. code-block:: csharp

   // Increase limit on server
   .ConfigureRateLimiting(options => {
       options.MaxConnectionsPerIpPerWindow = 500;  // Increase
   });

Diagnostics
===========

Enabling Verbose Logging
--------------------------------

.. code-block:: csharp

   using Microsoft.Extensions.Logging;

   using var loggerFactory = LoggerFactory.Create(builder => {
       builder
           .SetMinimumLevel(LogLevel.Debug)  // Verbose logging
           .AddConsole()
           .AddDebug();
   });

   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .UseLoggerFactory(loggerFactory)
       .Build();

Health Check
-----------------

.. code-block:: bash

   # Health check
   curl http://localhost:8081/health/

   # Metrics
   curl http://localhost:8081/metrics/ | jq

Queue Check
-----------------

.. code-block:: csharp

   var queues = await queueManager.ListQueuesAsync();
   
   foreach (var queueName in queues) {
       var info = await queueManager.GetQueueInfoAsync(queueName);
       Console.WriteLine($"{queueName}: {info.MessageCount} messages, {info.SubscriberCount} subscribers");
   }

Connection Check
--------------------

.. code-block:: csharp

   Console.WriteLine($"Active connections: {broker.ActiveConnections}");
   Console.WriteLine($"Messages in flight: {broker.InFlightMessages}");

Next Steps
==========

- :doc:`monitoring` — monitoring
- :doc:`health-checks` — health checks
- :doc:`configuration` — configuration
