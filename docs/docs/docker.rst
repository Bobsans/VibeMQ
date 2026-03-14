===============
Docker
===============

The official Docker image runs **VibeMQ.Server.Host**: a standalone broker with configuration from environment variables and an optional config file.

.. contents:: Contents
   :local:
   :depth: 2

Quick Start
===========

Pull and run from Docker Hub (recommended):

.. code-block:: bash

   docker pull bobsans/vibemq
   docker run -p 2925:2925 bobsans/vibemq

Clients can connect to ``localhost:2925``. By default the broker runs without authentication.

To build from source instead, use ``docker/Dockerfile`` from the repository (see image variants below).

Image variants
--------------

The default ``docker/Dockerfile`` builds a **size-optimized** image (Alpine, self-contained single-file).
If you prefer a **framework-dependent** image (runs via ``dotnet`` in the container), build the ``runtime`` target:

.. code-block:: bash

   docker build -f docker/Dockerfile --target runtime -t vibemq .

.. note::

   Docker images are built with **invariant globalization** mode for smaller base images.
   If your application relies on culture-specific formatting/parsing, test accordingly.

Broker + Web UI image
---------------------

To run the broker with the embedded dashboard (port **12925**), use the official Web UI image:

.. code-block:: bash

   docker pull bobsans/vibemq-webui
   docker run -p 2925:2925 -p 12925:12925 bobsans/vibemq-webui

Using Docker Compose
--------------------

.. code-block:: bash

   docker compose -f docker/docker-compose.yml up -d

The broker listens on port 2925. Edit ``docker/docker-compose.yml`` to change ports or add environment variables.

Configuration
=============

The image uses the same options as the rest of VibeMQ (see :doc:`configuration`). Two mechanisms are supported:

1. **Environment variables** — recommended for Docker/Kubernetes. Use the ``VibeMQ__`` prefix and double underscore for nested keys.
2. **Config file** — optional JSON file (e.g. ``appsettings.json``). Set ``VIBEMQ_CONFIG_PATH`` to the full path of the file; it is loaded first, then env vars override.

Environment variables (examples)
---------------------------------

.. list-table::
   :header-rows: 1
   :widths: 40 60

   * - Variable
     - Description
   * - ``VibeMQ__Port``
     - TCP port (default: 2925)
   * - ``VibeMQ__MaxConnections``
     - Max concurrent connections (default: 1000)
   * - ``VibeMQ__MaxMessageSize``
     - Max message size in bytes (default: 1048576)
   * - ``VibeMQ__EnableAuthentication``
     - ``true`` or ``false``
   * - ``VibeMQ__AuthToken``
     - Legacy token when ``EnableAuthentication`` is true
   * - ``VibeMQ__Authorization__SuperuserUsername``
     - Superuser login (username/password auth)
   * - ``VibeMQ__Authorization__SuperuserPassword``
     - Superuser password on first run (e.g. from secret)
   * - ``VibeMQ__Authorization__DatabasePath``
     - Path to SQLite auth DB (default: auth.db)
   * - ``VibeMQ__StorageType``
     - Host-specific: ``Sqlite`` to enable SQLite persistence (not in ``BrokerOptions``; the Docker Host reads this to register ``AddVibeMQSqliteStorage``). Omit or any other value = in-memory.
   * - ``VibeMQ__SqliteStorage__DatabasePath``
     - Path to SQLite queue DB when ``StorageType`` is Sqlite (section ``VibeMQ:SqliteStorage``).
   * - ``Logging__LogLevel__Default``
     - Log level: Debug, Information, Warning, Error

Config file path
----------------

Set ``VIBEMQ_CONFIG_PATH`` to load an additional or custom JSON config file (e.g. mounted in the container):

.. code-block:: bash

   docker run -p 2925:2925 \
     -v /host/path/appsettings.json:/app/config/appsettings.json \
     -e VIBEMQ_CONFIG_PATH=/app/config/appsettings.json \
     bobsans/vibemq

The built-in ``appsettings.json`` in the image provides defaults; env vars override any value from config.

Example: auth and port
----------------------

Token-based auth on port 9000:

.. code-block:: bash

   docker run -p 9000:9000 \
     -e VibeMQ__Port=9000 \
     -e VibeMQ__EnableAuthentication=true \
     -e VibeMQ__AuthToken=your-secret-token \
     bobsans/vibemq

Username/password auth with persistent auth DB:

.. code-block:: bash

   docker run -p 2925:2925 \
     -e VibeMQ__EnableAuthentication=true \
     -e VibeMQ__Authorization__SuperuserUsername=admin \
     -e VibeMQ__Authorization__SuperuserPassword=secret \
     -e VibeMQ__Authorization__DatabasePath=/data/auth.db \
     -v vibemq-data:/data \
     bobsans/vibemq

Example: SQLite persistence
----------------------------

.. code-block:: bash

   docker run -p 2925:2925 \
     -e VibeMQ__StorageType=Sqlite \
     -e VibeMQ__SqliteStorage__DatabasePath=/data/vibemq.db \
     -v vibemq-data:/data \
     bobsans/vibemq

Image details
=============

- **Published images:** ``bobsans/vibemq`` and ``bobsans/vibemq-webui``
- **Entrypoint:** ``dotnet VibeMQ.Server.Host.dll``
- **Working directory:** ``/app``
- **Exposed port:** 2925 (override with ``VibeMQ__Port`` and republish)

The host project is ``VibeMQ.Server.Host`` in the ``docker/`` folder of the repository; it uses :doc:`di-integration` and binds :doc:`configuration` from the ``VibeMQ`` section.
