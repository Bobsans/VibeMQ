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
   using VibeMQ.Enums;

   var broker = BrokerBuilder.Create()
       .UsePort(2925)
       .UseAuthorization(options => {
       options.SuperuserUsername = "admin";
       options.SuperuserPassword = "my-secret-password";
   })
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
       2925,
       new ClientOptions { Username = "admin", Password = "my-secret-password"  }
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
   docs/docker
   docs/client-usage
   docs/configuration
   docs/authorization
   docs/di-integration
   docs/storage

.. toctree::
   :maxdepth: 2
   :caption: Monitoring & Operations

   docs/monitoring
   docs/health-checks
   docs/web-ui
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

**Persistence:**

- Pluggable storage providers (InMemory by default, SQLite available)
- Write-ahead logging — messages saved before delivery
- Automatic recovery of queues and messages on restart
- Dead Letter Queue persistence

**Security:**

- Username/password authentication with per-queue ACL (BCrypt + SQLite)
- Legacy username/password authentication
- TLS/SSL encryption support
- Rate limiting for overload protection

**Monitoring:**

- Built-in performance metrics
- HTTP endpoints for health checks
- Optional Web UI dashboard (VibeMQ.Server.WebUI)
- Statistics for queues and connections

📦 Modular Architecture
========================

VibeMQ consists of several NuGet packages:

.. list-table::
   :header-rows: 1
   :widths: 42 38

   * - Package
     - Description
   * - ``VibeMQ.Server``
     - Broker server
   * - ``VibeMQ.Client``
     - Client for broker connection
   * - ``VibeMQ.Core``
     - Core: models, interfaces, configuration
   * - ``VibeMQ.Protocol``
     - Message exchange protocol
   * - ``VibeMQ.Health``
     - HTTP health check server
   * - ``VibeMQ.Server.DependencyInjection``
     - Server DI integration
   * - ``VibeMQ.Client.DependencyInjection``
     - Client DI integration
   * - ``VibeMQ.Server.Storage.Sqlite``
     - SQLite persistence provider
   * - ``VibeMQ.Server.WebUI``
     - Optional Web dashboard (health, metrics, queues) on a separate port

💡 Usage Examples
==================

Server with Dependency Injection:

.. code-block:: csharp

   using VibeMQ.Server.DependencyInjection;
   using VibeMQ.Configuration;
   using VibeMQ.Enums;

   var host = Host.CreateDefaultBuilder(args)
       .ConfigureServices(services => {
           services.AddVibeMQBroker(options => {
               options.Port = 2925;
               options.Authorization = new AuthorizationOptions {
                   SuperuserUsername = "admin",
                   SuperuserPassword = "change-me"
               };
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
               settings.Port = 2925;
               settings.ClientOptions.Username = "admin";
               settings.ClientOptions.Password = "my-secret-password";
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
