======================================
VibeMQ — .NET Message Broker
======================================

.. image:: https://img.shields.io/badge/.NET-8.0+-blue.svg
   :alt: .NET Version
   :target: https://dotnet.microsoft.com/

.. image:: https://img.shields.io/badge/license-MIT-green.svg
   :alt: License
   :target: https://opensource.org/licenses/MIT

**VibeMQ** is a simple yet reliable message broker for .NET applications using TCP as transport. It supports publish/subscribe (pub/sub), queues with delivery guarantees, automatic reconnections, and other essential features for building distributed systems.

.. contents:: Contents
   :local:
   :depth: 2

🚀 Quick Start
===============

Install via NuGet:

.. code-block:: bash

   dotnet add package VibeMQ.Server
   dotnet add package VibeMQ.Client

Start the server:

.. code-block:: csharp

   using VibeMQ.Server;
   using VibeMQ.Core.Enums;

   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .UseAuthentication("my-secret-token")
       .ConfigureQueues(options => {
           options.DefaultDeliveryMode = DeliveryMode.RoundRobin;
           options.MaxQueueSize = 10_000;
       })
       .Build();

   await broker.RunAsync(cancellationToken);

Connect the client:

.. code-block:: csharp

   using VibeMQ.Client;

   await using var client = await VibeMQClient.ConnectAsync(
       "localhost",
       8080,
       new ClientOptions { AuthToken = "my-secret-token" }
   );

   // Publish a message
   await client.PublishAsync("notifications", new { Title = "Hello", Body = "World" });

   // Subscribe to messages
   await using var subscription = await client.SubscribeAsync<dynamic>(
       "notifications",
       msg => {
           Console.WriteLine($"Received: {msg.Title}");
           return Task.CompletedTask;
       }
   );

📋 Table of Contents
=====================

.. toctree::
   :maxdepth: 2
   :caption: Getting Started

   docs/getting-started
   docs/installation

.. toctree::
   :maxdepth: 2
   :caption: Core Concepts

   docs/architecture
   docs/features
   docs/protocol

.. toctree::
   :maxdepth: 2
   :caption: Setup & Usage

   docs/server-setup
   docs/client-usage
   docs/configuration
   docs/di-integration

.. toctree::
   :maxdepth: 2
   :caption: Monitoring & Operations

   docs/monitoring
   docs/health-checks
   docs/troubleshooting

.. toctree::
   :maxdepth: 2
   :caption: Additional Resources

   docs/examples
   docs/faq
   docs/changelog

🎯 Key Features
================

**Message Delivery Modes:**

- **Round-robin** — each message delivered to one subscriber (cyclically)
- **Fan-out with acknowledgment** — to all subscribers with delivery guarantee
- **Fan-out without acknowledgment** — to all subscribers without confirmation
- **Priority-based** — delivery by priority (Critical > High > Normal > Low)

**Delivery Guarantees:**

- Acknowledgments (ACK) from receivers
- Automatic retry attempts with exponential backoff
- Dead Letter Queue for failed messages
- In-flight message tracking

**Reliability:**

- Keep-alive (PING/PONG) for connection maintenance
- Automatic reconnections on client side
- Graceful shutdown without message loss
- Health checks for orchestrators

**Security:**

- Token-based authentication
- TLS/SSL encryption support
- Rate limiting for overload protection

**Monitoring:**

- Built-in performance metrics
- HTTP endpoints for health checks
- Statistics for queues and connections

📦 Modular Architecture
========================

VibeMQ consists of several NuGet packages:

+--------------------------------+------------------------------------------+
| Package                        | Description                                |
+================================+==========================================+
| ``VibeMQ.Server``              | Broker server                              |
+--------------------------------+------------------------------------------+
| ``VibeMQ.Client``              | Client for broker connection               |
+--------------------------------+------------------------------------------+
| ``VibeMQ.Core``                | Core: models, interfaces, configuration    |
+--------------------------------+------------------------------------------+
| ``VibeMQ.Protocol``            | Message exchange protocol                  |
+--------------------------------+------------------------------------------+
| ``VibeMQ.Health``              | HTTP health check server                   |
+--------------------------------+------------------------------------------+
| ``VibeMQ.Server.DependencyInjection``    | Server DI integration           |
+--------------------------------+------------------------------------------+
| ``VibeMQ.Client.DependencyInjection``    | Client DI integration            |
+--------------------------------+------------------------------------------+

💡 Usage Examples
==================

Server with Dependency Injection:

.. code-block:: csharp

   using VibeMQ.Server.DependencyInjection;
   using VibeMQ.Core.Enums;

   var host = Host.CreateDefaultBuilder(args)
       .ConfigureServices(services => {
           services.AddVibeMQBroker(options => {
               options.Port = 8080;
               options.EnableAuthentication = true;
               options.AuthToken = "my-secret-token";
               options.QueueDefaults.DefaultDeliveryMode = DeliveryMode.RoundRobin;
           });
       })
       .Build();

   await host.RunAsync();

Client with Dependency Injection:

.. code-block:: csharp

   using VibeMQ.Client;
   using VibeMQ.Client.DependencyInjection;

   var host = Host.CreateDefaultBuilder(args)
       .ConfigureServices(services => {
           services.AddVibeMQClient(settings => {
               settings.Host = "localhost";
               settings.Port = 8080;
               settings.ClientOptions.AuthToken = "my-secret-token";
           });
       })
       .Build();

   // Option A: inject IVibeMQClient (shared, lazy-connected)
   var client = host.Services.GetRequiredService<IVibeMQClient>();
   await client.PublishAsync("notifications", new { Title = "Hello" });

   // Option B: create a dedicated client (you dispose it)
   var factory = host.Services.GetRequiredService<IVibeMQClientFactory>();
   await using var dedicatedClient = await factory.CreateAsync();

🔗 Links
=========

- `GitHub Repository <https://github.com/DarkBoy/VibeMQ>`_
- `NuGet Packages <https://www.nuget.org/packages?q=VibeMQ>`_
- `Issues & Feature Requests <https://github.com/DarkBoy/VibeMQ/issues>`_

📄 License
===========

VibeMQ is distributed under the MIT License. See LICENSE file for details.
