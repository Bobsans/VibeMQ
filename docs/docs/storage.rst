=====================
Persistence & Storage
=====================

This guide describes the persistence layer in VibeMQ — how messages, queues, and dead-letter entries survive server restarts.

.. contents:: Contents
   :local:
   :depth: 2

Overview
========

By default VibeMQ keeps everything in memory: fast, zero-config, but all state is lost on restart. Starting with version 1.3.0 you can plug in a **storage provider** so that queues, messages, and DLQ entries are persisted to disk (or to an external database in future versions).

**Key design decisions:**

- **Write-ahead pattern** — a message is saved to storage *before* it enters the in-memory queue. On ACK the persisted copy is removed.
- **Single interface** — ``IStorageProvider`` covers messages, queue metadata, and DLQ in one contract.
- **Separate NuGet packages** — each provider lives in its own assembly so you only take the dependency you need.
- **Backward compatible** — ``InMemory`` is the default; existing code works without changes.

Storage Providers
=================

+--------------------------------------+--------------------------------------------+
| Provider                             | Description                                |
+======================================+============================================+
| ``InMemoryStorageProvider``          | Default. No persistence, all in-memory.    |
+--------------------------------------+--------------------------------------------+
| ``SqliteStorageProvider``            | SQLite-based. Zero-config, single-file DB. |
+--------------------------------------+--------------------------------------------+

Future providers (RocksDB, PostgreSQL, Redis) are planned — see the project roadmap.

Quick Start
===========

InMemory (default)
------------------

No configuration required — this is the default behavior:

.. code-block:: csharp

   var broker = BrokerBuilder.Create()
       .UsePort(2925)
       .Build();

SQLite
------

Install the package:

.. code-block:: bash

   dotnet add package VibeMQ.Server.Storage.Sqlite

Configure via the fluent builder:

.. code-block:: csharp

   using VibeMQ.Server;
   using VibeMQ.Server.Storage.Sqlite;

   var broker = BrokerBuilder.Create()
       .UsePort(2925)
       .UseSqliteStorage(options => {
           options.DatabasePath = "vibemq.db";
           options.EnableWal = true;
           options.BusyTimeoutMs = 5000;
       })
       .Build();

   await broker.RunAsync(cancellationToken);

Or via Dependency Injection:

.. code-block:: csharp

   using VibeMQ.Server.DependencyInjection;
   using VibeMQ.Server.Storage.Sqlite;

   services.AddVibeMQSqliteStorage(options => {
       options.DatabasePath = "vibemq.db";
   });
   services.AddVibeMQBroker(options => {
       options.Port = 2925;
   });

.. note::

   Register the storage provider **before** ``AddVibeMQBroker`` so that the broker picks it up from the DI container.

SQLite Configuration
====================

SqliteStorageOptions
--------------------

+---------------------+-------------------+----------------------------------------------+
| Parameter           | Default           | Description                                  |
+=====================+===================+==============================================+
| ``DatabasePath``    | ``"vibemq.db"``   | Path to the SQLite database file             |
+---------------------+-------------------+----------------------------------------------+
| ``EnableWal``       | ``true``          | Enable WAL journal mode for better           |
|                     |                   | concurrent read/write performance            |
+---------------------+-------------------+----------------------------------------------+
| ``BusyTimeoutMs``   | ``5000``          | Milliseconds to wait when the DB is locked   |
+---------------------+-------------------+----------------------------------------------+

Database Schema
---------------

The SQLite provider automatically creates the following tables on first startup:

**queues** — persisted queue metadata:

.. code-block:: sql

   CREATE TABLE IF NOT EXISTS queues (
       name        TEXT PRIMARY KEY,
       options_json TEXT NOT NULL,
       created_at  TEXT NOT NULL
   );

**messages** — persisted messages with foreign key to queues:

.. code-block:: sql

   CREATE TABLE IF NOT EXISTS messages (
       id                TEXT PRIMARY KEY,
       queue_name        TEXT NOT NULL REFERENCES queues(name) ON DELETE CASCADE,
       payload_json      TEXT,
       timestamp         TEXT NOT NULL,
       headers_json      TEXT,
       version           INTEGER NOT NULL DEFAULT 1,
       priority          INTEGER NOT NULL DEFAULT 1,
       delivery_attempts INTEGER NOT NULL DEFAULT 0
   );

   CREATE INDEX IF NOT EXISTS ix_messages_queue_timestamp
       ON messages(queue_name, timestamp);
   CREATE INDEX IF NOT EXISTS ix_messages_queue_priority
       ON messages(queue_name, priority DESC, timestamp);

**dead_letters** — persisted dead-lettered messages:

.. code-block:: sql

   CREATE TABLE IF NOT EXISTS dead_letters (
       id           INTEGER PRIMARY KEY AUTOINCREMENT,
       message_id   TEXT NOT NULL,
       message_json TEXT NOT NULL,
       reason       INTEGER NOT NULL,
       failed_at    TEXT NOT NULL
   );

   CREATE INDEX IF NOT EXISTS ix_dead_letters_failed_at
       ON dead_letters(failed_at);

.. note::

   Deleting a queue (``RemoveQueueAsync``) cascade-deletes all its messages via the foreign key constraint.

How Persistence Works
=====================

Write-Ahead Pattern
-------------------

When a message is published:

.. code-block:: text

   Publisher
       │
       ▼
   SaveMessageAsync(message)      ← write to storage first
       │
       ▼
   queue.Enqueue(message)         ← then add to in-memory queue
       │
       ▼
   PublishAck → Publisher

If the server crashes after the storage write but before the in-memory enqueue, the message is recovered on next startup.

Acknowledgment Flow
-------------------

When a subscriber acknowledges a message:

.. code-block:: text

   Subscriber
       │
       ▼
   AckTracker.TryAcknowledge(messageId)
       │
       ▼
   RemoveMessageAsync(messageId)  ← delete from storage
       │
       ▼
   Metrics updated

Startup Recovery
----------------

On server startup, the broker automatically recovers persisted state:

1. ``IStorageProvider.InitializeAsync()`` — creates schema if needed
2. ``GetAllQueuesAsync()`` — loads all queue metadata
3. For each queue: ``GetPendingMessagesAsync(queueName)`` — replays undelivered messages
4. Queues are recreated with their original ``CreatedAt`` timestamps

.. code-block:: text

   BrokerServer.RunAsync()
       │
       ▼
   QueueManager.InitializeAsync()
       │
       ├── StorageProvider.InitializeAsync()
       │
       ├── GetAllQueuesAsync()
       │       │
       │       ▼
       │   Recreate MessageQueue for each persisted queue
       │
       └── GetPendingMessagesAsync(queueName)
               │
               ▼
           Enqueue recovered messages

Dead Letter Queue Persistence
-----------------------------

Failed messages are persisted to the ``dead_letters`` table before being added to the in-memory DLQ:

.. code-block:: text

   Message delivery failed (max retries exceeded)
       │
       ▼
   SaveDeadLetteredMessageAsync(dlqMessage)  ← persist first
       │
       ▼
   In-memory DLQ enqueue

Storage Management
==================

The ``IStorageManagement`` interface provides optional maintenance operations. Not all providers support it — check at runtime:

.. code-block:: csharp

   if (storageProvider is IStorageManagement mgmt)
   {
       // Backup
       await mgmt.BackupAsync("/backups/vibemq-backup.db");

       // Restore
       await mgmt.RestoreAsync("/backups/vibemq-backup.db");

       // Compact (VACUUM for SQLite)
       await mgmt.CompactAsync();

       // Statistics
       var stats = await mgmt.GetStatsAsync();
       Console.WriteLine($"Messages: {stats.TotalMessages}");
       Console.WriteLine($"Queues: {stats.TotalQueues}");
       Console.WriteLine($"Dead-lettered: {stats.TotalDeadLettered}");
       Console.WriteLine($"Storage size: {stats.StorageSizeBytes} bytes");
   }

StorageStats
------------

+-------------------------+-----------+------------------------------------------+
| Property                | Type      | Description                              |
+=========================+===========+==========================================+
| ``TotalMessages``       | ``long``  | Total persisted messages                 |
+-------------------------+-----------+------------------------------------------+
| ``TotalQueues``         | ``long``  | Total persisted queues                   |
+-------------------------+-----------+------------------------------------------+
| ``TotalDeadLettered``   | ``long``  | Total dead-lettered entries              |
+-------------------------+-----------+------------------------------------------+
| ``StorageSizeBytes``    | ``long``  | Database file size in bytes              |
+-------------------------+-----------+------------------------------------------+

IStorageProvider Interface
==========================

All storage providers implement this interface:

.. code-block:: csharp

   public interface IStorageProvider : IAsyncDisposable
   {
       // Lifecycle
       Task InitializeAsync(CancellationToken cancellationToken = default);
       Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

       // Messages
       Task SaveMessageAsync(BrokerMessage message, CancellationToken ct = default);
       Task SaveMessagesAsync(IReadOnlyList<BrokerMessage> messages, CancellationToken ct = default);
       Task<BrokerMessage?> GetMessageAsync(string id, CancellationToken ct = default);
       Task<bool> RemoveMessageAsync(string id, CancellationToken ct = default);
       Task<IReadOnlyList<BrokerMessage>> GetPendingMessagesAsync(string queueName, CancellationToken ct = default);

       // Queues
       Task SaveQueueAsync(string name, QueueOptions options, CancellationToken ct = default);
       Task RemoveQueueAsync(string name, CancellationToken ct = default);
       Task<IReadOnlyList<StoredQueue>> GetAllQueuesAsync(CancellationToken ct = default);

       // Dead Letter Queue
       Task SaveDeadLetteredMessageAsync(DeadLetteredMessage message, CancellationToken ct = default);
       Task<IReadOnlyList<DeadLetteredMessage>> GetDeadLetteredMessagesAsync(int count, CancellationToken ct = default);
       Task<bool> RemoveDeadLetteredMessageAsync(string messageId, CancellationToken ct = default);
   }

**Contract rules:**

1. ``InitializeAsync`` **must** be called before any other operations.
2. ``RemoveQueueAsync`` **must** cascade-delete all messages in the queue.
3. ``SaveMessagesAsync`` has a default implementation that calls ``SaveMessageAsync`` in a loop. Providers should override for batch optimization.
4. ``IsAvailableAsync`` returns ``true`` for embedded providers (InMemory, SQLite). Network providers should check connectivity.
5. All methods **must** be thread-safe.

Implementing a Custom Provider
==============================

To create your own storage provider:

1. Create a new class library project referencing ``VibeMQ.Core``.
2. Implement ``IStorageProvider`` (and optionally ``IStorageManagement``).
3. Add an extension method for ``BrokerBuilder`` and/or ``IServiceCollection``:

.. code-block:: csharp

   public static class MyStorageExtensions
   {
       public static BrokerBuilder UseMyStorage(
           this BrokerBuilder builder,
           Action<MyStorageOptions>? configure = null)
       {
           var options = new MyStorageOptions();
           configure?.Invoke(options);

           return builder.UseStorageProvider(_ => new MyStorageProvider(options));
       }

       public static IServiceCollection AddVibeMQMyStorage(
           this IServiceCollection services,
           Action<MyStorageOptions>? configure = null)
       {
           var options = new MyStorageOptions();
           configure?.Invoke(options);

           services.AddSingleton<IStorageProvider>(new MyStorageProvider(options));
           return services;
       }
   }

Configuration Examples
======================

Development (InMemory)
----------------------

.. code-block:: csharp

   var broker = BrokerBuilder.Create()
       .UsePort(2925)
       .Build();

Production (SQLite)
-------------------

.. code-block:: csharp

   var broker = BrokerBuilder.Create()
       .UsePort(2925)
       .UseAuthentication(Environment.GetEnvironmentVariable("VIBEMQ_TOKEN"))
       .UseSqliteStorage(options => {
           options.DatabasePath = "/data/vibemq.db";
           options.EnableWal = true;
       })
       .Build();

Production with DI (SQLite)
---------------------------

.. code-block:: csharp

   services.AddVibeMQSqliteStorage(options => {
       options.DatabasePath = "/data/vibemq.db";
       options.EnableWal = true;
   });

   services.AddVibeMQBroker(options => {
       options.Port = 2925;
       options.EnableAuthentication = true;
       options.AuthToken = Environment.GetEnvironmentVariable("VIBEMQ_TOKEN");
   });

appsettings.json (SQLite)
-------------------------

.. code-block:: json

   {
     "VibeMQ": {
       "Port": 2925,
       "StorageType": "Sqlite",
       "EnableAuthentication": true,
       "AuthToken": "my-secret-token"
     }
   }

Migration from Previous Versions
=================================

If you are upgrading from VibeMQ 1.2.x:

1. ``IMessageStore`` is now deprecated. It still works but will be removed in a future version. Migrate to ``IStorageProvider``.
2. ``InMemoryMessageStore`` is deprecated in favor of ``InMemoryStorageProvider``.
3. No breaking changes — the default behavior (in-memory) is preserved. Adding persistence is opt-in.

Next Steps
==========

- :doc:`server-setup` — server configuration
- :doc:`configuration` — all configuration parameters
- :doc:`di-integration` — DI integration
- :doc:`architecture` — system architecture
