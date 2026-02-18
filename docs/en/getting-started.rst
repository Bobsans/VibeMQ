===============
Quick Start
===============

This guide will help you quickly get started with VibeMQ — from installation to your first message.

.. contents:: Contents
   :local:
   :depth: 2

Prerequisites
=============

- **.NET 8.0 SDK** or higher
- Basic knowledge of C# and asynchronous programming

Installation
============

Create a new project or open an existing one:

.. code-block:: bash

   dotnet new console -n VibeMQ.Demo
   cd VibeMQ.Demo

Install the required NuGet packages:

.. code-block:: bash

   dotnet add package VibeMQ.Server
   dotnet add package VibeMQ.Client

First Server
============

Create a ``Program.cs`` file with the server startup code:

.. code-block:: csharp

   using Microsoft.Extensions.Logging;
   using VibeMQ.Server;
   using VibeMQ.Core.Enums;

   using var loggerFactory = LoggerFactory.Create(builder => {
       builder.SetMinimumLevel(LogLevel.Information).AddConsole();
   });

   // Create and configure the broker
   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .UseAuthentication("my-secret-token")
       .ConfigureQueues(options => {
           options.DefaultDeliveryMode = DeliveryMode.RoundRobin;
           options.MaxQueueSize = 10_000;
           options.EnableAutoCreate = true;
       })
       .UseLoggerFactory(loggerFactory)
       .Build();

   Console.WriteLine("Starting VibeMQ server on port 8080...");

   // Start the server
   await broker.RunAsync(CancellationToken.None);

Start the server:

.. code-block:: bash

   dotnet run

First Client
============

Create a second project for the client:

.. code-block:: bash

   dotnet new console -n VibeMQ.Client.Demo
   cd VibeMQ.Client.Demo
   dotnet add package VibeMQ.Client

Edit ``Program.cs``:

.. code-block:: csharp

   using Microsoft.Extensions.Logging;
   using VibeMQ.Client;

   using var loggerFactory = LoggerFactory.Create(builder => {
       builder.SetMinimumLevel(LogLevel.Information).AddConsole();
   });

   var logger = loggerFactory.CreateLogger<VibeMQClient>();

   // Connect to the server
   await using var client = await VibeMQClient.ConnectAsync(
       "localhost",
       8080,
       new ClientOptions {
           AuthToken = "my-secret-token",
           ReconnectPolicy = new ReconnectPolicy {
               MaxAttempts = 5,
               UseExponentialBackoff = true
           }
       },
       logger
   );

   Console.WriteLine("Connected to VibeMQ server!");

   // Subscribe to the "notifications" queue
   await using var subscription = await client.SubscribeAsync<dynamic>(
       "notifications",
       async msg => {
           Console.WriteLine($"📨 Received message:");
           Console.WriteLine($"   Title: {msg.Title}");
           Console.WriteLine($"   Body: {msg.Body}");
           Console.WriteLine($"   Time: {msg.Timestamp}");
       }
   );

   Console.WriteLine("Subscription created. Waiting for messages...");
   Console.WriteLine("Press Enter to send a test message.");
   Console.ReadLine();

   // Send a test message
   await client.PublishAsync("notifications", new {
       Title = "Hello!",
       Body = "This is the first message in VibeMQ",
       Timestamp = DateTime.Now.ToString("HH:mm:ss")
   });

   Console.WriteLine("Message sent!");
   Console.WriteLine("Press Enter to exit...");
   Console.ReadLine();

Start the client:

.. code-block:: bash

   dotnet run

Complete Example: Publisher and Subscriber
==========================================

Create two separate applications:

**Publisher (Publisher.cs):**

.. code-block:: csharp

   using VibeMQ.Client;

   await using var publisher = await VibeMQClient.ConnectAsync("localhost", 8080, new ClientOptions {
       AuthToken = "my-secret-token"
   });

   Console.WriteLine("Publisher connected. Enter a message to send:");

   while (true) {
       var input = Console.ReadLine();
       if (string.IsNullOrWhiteSpace(input)) break;

       await publisher.PublishAsync("notifications", new {
           Title = "New Message",
           Body = input,
           Timestamp = DateTime.Now.ToString("HH:mm:ss")
       });

       Console.WriteLine("✓ Message sent");
   }

**Subscriber (Subscriber.cs):**

.. code-block:: csharp

   using VibeMQ.Client;

   await using var subscriber = await VibeMQClient.ConnectAsync("localhost", 8080, new ClientOptions {
       AuthToken = "my-secret-token"
   });

   await using var subscription = await subscriber.SubscribeAsync<dynamic>(
       "notifications",
       async msg => {
           Console.WriteLine($"🔔 {msg.Title}: {msg.Body} (at {msg.Timestamp})");
       }
   );

   Console.WriteLine("Subscriber started. Press Enter to exit...");
   Console.ReadLine();

Start the subscriber first, then the publisher, and send a few messages.

Next Steps
==========

Now that you're familiar with the basics, you can explore:

- :doc:`architecture` — how VibeMQ works internally
- :doc:`features` — detailed overview of all features
- :doc:`server-setup` — detailed server configuration
- :doc:`client-usage` — advanced client usage
- :doc:`configuration` — all configuration parameters
- :doc:`di-integration` — integration with Dependency Injection
