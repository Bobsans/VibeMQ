============
Installation
============

This guide describes various ways to install VibeMQ.

.. contents:: Contents
   :local:
   :depth: 2

System Requirements
===================

**Minimum requirements:**

- OS: Windows, Linux, macOS
- .NET 8.0 SDK or higher
- RAM: 512 MB (1 GB+ recommended)
- Disk space: 100 MB

**For production:**

- .NET 8.0 Runtime or .NET 10.0
- 2+ GB RAM
- SSD for better performance

NuGet Installation
==================

Basic Packages
--------------

To use VibeMQ in your project, add the required packages:

.. code-block:: bash

   # Broker server
   dotnet add package VibeMQ.Server

   # Client for connection
   dotnet add package VibeMQ.Client

   # Core (models and interfaces)
   dotnet add package VibeMQ.Core

   # Communication protocol
   dotnet add package VibeMQ.Protocol

   # Health check server
   dotnet add package VibeMQ.Health

Dependency Injection Packages
-----------------------------

For integration with Microsoft.Extensions.DependencyInjection:

.. code-block:: bash

   # DI for server
   dotnet add package VibeMQ.Server.DependencyInjection

   # DI for client
   dotnet add package VibeMQ.Client.DependencyInjection

All Packages at Once
--------------------

For quick addition of all components:

.. code-block:: bash

   dotnet add package VibeMQ.Server
   dotnet add package VibeMQ.Client
   dotnet add package VibeMQ.Health
   dotnet add package VibeMQ.Server.DependencyInjection
   dotnet add package VibeMQ.Client.DependencyInjection

Docker Installation
===================

VibeMQ can be run in a Docker container.

Dockerfile
----------

Create a ``Dockerfile`` in your project:

.. code-block:: dockerfile

   FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
   WORKDIR /app
   EXPOSE 8080 8081

   FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
   WORKDIR /src
   COPY ["VibeMQ.Server.csproj", "."]
   RUN dotnet restore
   COPY . .
   RUN dotnet build -c Release -o /app/build

   FROM build AS publish
   RUN dotnet publish -c Release -o /app/publish

   FROM base AS final
   WORKDIR /app
   COPY --from=publish /app/publish .
   ENTRYPOINT ["dotnet", "VibeMQ.Server.dll"]

Build and run:

.. code-block:: bash

   docker build -t vibemq-server .
   docker run -p 8080:8080 -p 8081:8081 vibemq-server

Docker Compose
--------------

For deployment with other services, create a ``docker-compose.yml``:

.. code-block:: yaml

   version: '3.8'

   services:
     vibemq:
       image: vibemq-server:latest
       container_name: vibemq-broker
       ports:
         - "8080:8080"  # TCP port for clients
         - "8081:8081"  # HTTP port for health checks
       environment:
         - VIBEMQ_PORT=8080
         - VIBEMQ_AUTH_TOKEN=my-secret-token
         - VIBEMQ_MAX_CONNECTIONS=1000
       volumes:
         - vibemq-data:/data
       restart: unless-stopped

     # Example client application
     my-app:
       image: my-app:latest
       depends_on:
         - vibemq
       environment:
         - VIBEMQ_HOST=vibemq
         - VIBEMQ_PORT=8080

   volumes:
     vibemq-data:

Start:

.. code-block:: bash

   docker-compose up -d

Installation from Source Code
=============================

Clone the Repository
--------------------

.. code-block:: bash

   git clone https://github.com/DarkBoy/VibeMQ.git
   cd VibeMQ

Build the Project
-----------------

.. code-block:: bash

   # Build all projects
   dotnet build -c Release

   # Run tests
   dotnet test

   # Build a specific project
   dotnet build src/VibeMQ.Server/VibeMQ.Server.csproj -c Release

Local Package Installation
--------------------------

To use a locally built version:

.. code-block:: bash

   # Create local NuGet source
   dotnet pack src/VibeMQ.Server -c Release -o ./nupkg
   dotnet pack src/VibeMQ.Client -c Release -o ./nupkg

   # Add local source
   dotnet nuget add source ./nupkg --name LocalVibeMQ

   # Install from local source
   dotnet add package VibeMQ.Server --source LocalVibeMQ

Integration into Existing Projects
==================================

ASP.NET Core Application
------------------------

For ASP.NET Core applications, use DI integration:

.. code-block:: csharp

   using VibeMQ.Server.DependencyInjection;
   using VibeMQ.Client.DependencyInjection;

   var builder = WebApplication.CreateBuilder(args);

   // Add broker server
   builder.Services.AddVibeMQBroker(options => {
       options.Port = 8080;
       options.EnableAuthentication = true;
       options.AuthToken = builder.Configuration["VibeMQ:AuthToken"];
   });

   // Add client for sending messages
   builder.Services.AddVibeMQClient(settings => {
       settings.Host = "localhost";
       settings.Port = 8080;
       settings.ClientOptions.AuthToken = builder.Configuration["VibeMQ:AuthToken"];
   });

   var app = builder.Build();
   await app.RunAsync();

Worker Service
--------------

For background services (.NET Worker):

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

Console Application
-------------------

For console applications, use direct invocation:

.. code-block:: csharp

   using VibeMQ.Server;

   var broker = BrokerBuilder.Create()
       .UsePort(8080)
       .Build();

   await broker.RunAsync(CancellationToken.None);

Installation Verification
=========================

Server Verification
-------------------

After starting the server, check the health endpoint:

.. code-block:: bash

   curl http://localhost:8081/health/

Response should be:

.. code-block:: json

   {
     "status": "healthy",
     "active_connections": 0,
     "queue_count": 0,
     "memory_usage_mb": 128
   }

Client Verification
-------------------

Create a test script:

.. code-block:: csharp

   using VibeMQ.Client;

   try {
       await using var client = await VibeMQClient.ConnectAsync("localhost", 8080);
       Console.WriteLine("✓ Connection successful!");
       Console.WriteLine($"Status: {(client.IsConnected ? "Connected" : "Disconnected")}");
   } catch (Exception ex) {
       Console.WriteLine($"✗ Error: {ex.Message}");
   }

Troubleshooting
===============

Port Already in Use
-------------------

**Error:** ``Address already in use``

**Solution:** Change the port in configuration:

.. code-block:: csharp

   var broker = BrokerBuilder.Create()
       .UsePort(8081)  // Use a different port
       .Build();

Authentication Error
--------------------

**Error:** ``Authentication failed``

**Solution:** Make sure tokens match:

.. code-block:: csharp

   // Server
   .UseAuthentication("my-token")

   // Client
   new ClientOptions { AuthToken = "my-token" }

TLS/SSL Errors
--------------

**Error:** ``The remote certificate is invalid``

**Solution:** For tests, disable validation:

.. code-block:: csharp

   new ClientOptions {
       UseTls = true,
       SkipCertificateValidation = true  // Only for tests!
   }

For production, use valid certificates.

Next Steps
==========

- :doc:`getting-started` — quick start guide
- :doc:`server-setup` — server configuration
- :doc:`configuration` — configuration parameters
