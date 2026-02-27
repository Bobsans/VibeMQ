================
Web UI Dashboard
================

Optional web dashboard for monitoring the VibeMQ broker: health, metrics, and queues in a browser. The dashboard runs on a separate HTTP port (default **12925**) and does not require ASP.NET Core.

.. contents:: Contents
   :local:
   :depth: 2

Overview
========

The **VibeMQ.Server.WebUI** package provides:

- A Vue 3 SPA served from the same process as the broker
- REST API endpoints for health, metrics, and queue list/detail
- Lightweight ``HttpListener``-based server (no Kestrel/ASP.NET)
- All UI assets embedded in the assembly after building the frontend

Quick Start
===========

Add the package:

.. code-block:: bash

   dotnet add package VibeMQ.Server.WebUI

Run the broker with the dashboard in one call:

.. code-block:: csharp

   using VibeMQ.Server;
   using VibeMQ.Server.WebUI;

   var broker = BrokerBuilder.Create()
       .UsePort(2925)
       .UseLoggerFactory(loggerFactory)
       .Build();

   await broker.RunWithWebUIAsync();

Open **http://localhost:12925/** in a browser to see the dashboard.

With Options
------------

.. code-block:: csharp

   await broker.RunWithWebUIAsync(new WebUIOptions {
       Port = 12925,
       Enabled = true,
       PathPrefix = "/",
   }, cancellationToken);

Manual Start
------------

You can start the Web UI server yourself and run it alongside the broker:

.. code-block:: csharp

   var webUi = new WebUIServer(broker, new WebUIOptions { Port = 12925 }, logger);
   webUi.Start();
   try {
       await broker.RunAsync(cancellationToken);
   } finally {
       await webUi.DisposeAsync();
   }

Configuration
=============

.. list-table::
   :header-rows: 1
   :widths: 24 14 42

   * - Parameter
     - Default
     - Description
   * - ``Enabled``
     - true
     - Whether the Web UI HTTP server is started
   * - ``Port``
     - 12925
     - HTTP port for the dashboard
   * - ``PathPrefix``
     - "/"
     - URL path prefix (must start and end with ``/``)

API Endpoints
=============

All responses are JSON with ``snake_case`` property names.

.. list-table::
   :header-rows: 1
   :widths: 12 28 40

   * - Method
     - Path
     - Description
   * - GET
     - ``/api/version``
     - Server and Web UI assembly versions (``server_version``, ``webui_version``)
   * - GET
     - ``/api/health``
     - Broker health: connections, queue count, in-flight, memory, timestamp
   * - GET
     - ``/api/metrics``
     - Full metrics snapshot (counters, gauges, latency, uptime)
   * - GET
     - ``/api/queues``
     - List of queue names
   * - GET
     - ``/api/queues/{name}``
     - Single queue metadata (message count, subscribers, delivery mode, etc.)
   * - GET
     - ``/api/queues/{name}/messages``
     - List messages in queue (query: ``limit``, ``offset``). Peek only.
   * - GET
     - ``/api/queues/{name}/messages/{messageId}``
     - Single message (full body) for viewing
   * - DELETE
     - ``/api/queues/{name}/messages/{messageId}``
     - Remove one message from the queue
   * - DELETE
     - ``/api/queues/{name}/messages``
     - Purge all messages in the queue
   * - DELETE
     - ``/api/queues/{name}``
     - Delete the queue and all its messages

Building the Frontend
=====================

The dashboard UI is a Vue 3 + Vite application. To refresh the embedded assets after changing the UI:

.. code-block:: bash

   cd src/VibeMQ.Server.WebUI/App
   npm install
   npm run build

Then rebuild the .NET project. The contents of ``App/dist/`` are embedded into the assembly. If ``dist/`` is missing, the project still builds, but opening the dashboard in a browser will show a 503 with instructions.

Requirements
============

- .NET 8.0 or later
- Reference ``VibeMQ.Server`` (and transitively ``VibeMQ.Core``)
- Node.js and npm only for **building** the frontend; not required at runtime

Security
========

The dashboard is intended for trusted networks (e.g. internal admin). It has no built-in authentication. If you expose the dashboard port to the internet, consider putting it behind a reverse proxy with HTTP Basic auth or similar.
