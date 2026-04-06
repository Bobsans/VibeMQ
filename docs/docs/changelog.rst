=========
Changelog
=========

VibeMQ project change history.

.. contents:: Contents
   :local:
   :depth: 2

Version History
================

Version 1.8.0
-------------

**Date:** April 2026

**Status:** Current Stable Version

**Breaking changes:**

- **Legacy token authentication removed** — ``BrokerBuilder.UseAuthentication(string)``, ``BrokerOptions.AuthToken``, ``ClientOptions.AuthToken``, ``BrokerOptions.EnableAuthentication``, and the Connect handshake ``authToken`` header path are removed. Use ``UseAuthorization()`` with ``Username`` and ``Password`` on the client.
- **Removed types** — ``IAuthenticationService`` and ``TokenAuthenticationService``.

**Maintenance:**

- **Connect validation** — client requires ``Username`` and ``Password`` to be set together when using credentials.
- **Dispose/disconnect** — ``DisposeAsync`` uses a shared disconnect path to avoid redundant work.
- **Tests** — integration tests use shared timeouts and fixtures; token-auth unit tests removed.
- **Documentation** — guides updated for authorization-only authentication; Docker and configuration examples aligned.

**Migration from 1.7.x:**

- Replace any ``UseAuthentication(token)`` / ``AuthToken`` usage with ``UseAuthorization(...)`` and ``Username`` + ``Password`` before upgrading.

Version 1.7.1
-------------

**Date:** March 2026

**Status:** Stable

**Maintenance:**

- Stabilized Redis in-flight message persistence and restart recovery paths for unacknowledged deliveries.
- Improved queue purge efficiency via bulk storage deletion support for compatible providers.
- Hardened client reconnect flow and command timeout handling for more predictable shutdown/reconnect behavior.
- Documented official Docker Hub images: ``bobsans/vibemq`` and ``bobsans/vibemq-webui``.
- Updated documentation and Russian translations for release consistency.

Version 1.7.0
-------------

**Date:** March 2026

**Status:** Stable

**New features:**

- **Redis persistence provider** — optional package ``VibeMQ.Server.Storage.Redis``; ``RedisStorageProvider`` implements ``IStorageProvider`` using Redis (LIST + HASH). ``BrokerBuilder.UseRedisStorage(connectionString, configure)``, ``AddVibeMQRedisStorage(services, connectionString, configure)``, and ``AddVibeMQRedisStorage(services, IConfiguration)`` for config section ``VibeMQ:Storage:Redis`` or ``ConnectionStrings:Redis``. Options: ``ConnectionString``, ``Database``, ``KeyPrefix``, ``ConnectTimeoutMs``, ``SyncTimeoutMs``.
- **Redis delivery durability improvements** — in-flight delivery state is now persisted and recovered on broker startup, so unacknowledged messages are re-queued after restart. Redis purge/delete paths also use batched operations to reduce round-trips.
- **Web UI dashboard** — optional package ``VibeMQ.Server.WebUI``; Vue 3 SPA for monitoring (health, metrics, queues) on a separate HTTP port (default 12925)
- **``RunWithWebUIAsync()``** — extension method to run broker and Web UI in one call
- **``WebUIServer``** — HttpListener-based server; API endpoints ``/api/health``, ``/api/metrics``, ``/api/queues``, ``/api/queues/{name}``
- **BrokerServer** — new read-only API for dashboard: ``QueueCount``, ``ListQueuesAsync()``, ``GetQueueInfoAsync(name)``
- **Connection string** — connect using a single URL (e.g. ``vibemq://user:pass@host:2925?tls=true``) or key=value string (e.g. ``Host=localhost;Port=2925``). ``VibeMQConnectionString.Parse`` / ``TryParse``; ``VibeMQClient.ConnectAsync(connectionString)``; ``AddVibeMQClient(services, connectionString)`` and ``AddVibeMQClient(services, IConfiguration)`` reading ``ConnectionStrings:VibeMQ`` or ``VibeMQ:Client:ConnectionString``. Optional query/keys: TLS, keep-alive, compression, reconnect policy, ``queues`` (declare-on-connect). ``VibeMQConnectionStringException`` for invalid strings.

**Maintenance:**

- **Reconnect robustness in ``VibeMQ.Client``** — reconnect loop now respects client shutdown cancellation during backoff and reconnect attempts, preventing background reconnect after dispose/disconnect.
- **Reliable resubscribe after reconnect** — subscription restore now waits for broker response and treats resubscribe errors as reconnect failures (with retry).
- **Connection string parser fix** — URL user info now correctly handles percent-encoded ``:`` in usernames (e.g. ``user%3Aname``).
- **Clear command timeout errors** — client now throws ``TimeoutException`` when broker response exceeds ``CommandTimeout`` instead of a generic cancellation.

**Documentation:**

- New :doc:`web-ui` guide (quick start, options, API, building frontend)
- Client usage and DI integration: connection string format, examples, configuration keys
- Docker images: size-optimized Alpine builds (self-contained single-file) for broker and Web UI
- Storage guide: Redis provider quick start, options, and configuration examples
- Configuration and storage docs aligned with code: ``BrokerOptions`` no longer has ``StorageType`` (persistence is configured via ``UseSqliteStorage``/``UseRedisStorage``); ``ClientOptions`` documents ``Username``/``Password``; ``QueueDefaults`` does not include ``OverflowStrategy`` (use ``QueueOptions`` when creating queues); ``RedisStorageOptions`` table corrected (removed non-existent ``DefaultQueueTtlSeconds``)
- Full docs pass: FAQ (persistence, server restart), features (MaxRetryAttempts in QueueOptions), examples (CQRS extra brace), docker (StorageType Host-specific note), installation (env vars ``VIBEMQ__*`` / ``VIBEMQCLIENT__*`` for .NET config)

Version 1.6.1
-------------

**Date:** February 2026

**Status:** Stable

**Maintenance:**

- Documentation and translation updates (changelog, obsolete .po cleanup, update-docs skill)

Version 1.6.0
-------------

**Date:** February 2026

**Status:** Stable

**New features:**

- **Username/password authorization** — BCrypt-hashed passwords stored in a dedicated SQLite ``auth.db``; superuser account seeded automatically on first run
- **Per-queue ACL** — glob patterns (``*`` matches any sequence including dots) mapped to ``QueueOperation`` sets; union semantics when multiple patterns match
- **Superuser bypass** — accounts with ``IsSuperuser=true`` skip all permission checks
- **Session permission cache** — permissions loaded once at login into ``ClientConnection.CachedPermissions``; no per-request DB I/O
- **Filtered ListQueues** — regular users see only queues matching their ACL patterns with ``ListQueues`` operation
- **7 admin protocol commands** — ``AdminCreateUser``, ``AdminDeleteUser``, ``AdminChangePassword``, ``AdminGrantPermission``, ``AdminRevokePermission``, ``AdminListUsers``, ``AdminGetUserPermissions`` (superuser-only)
- **Client admin API** — ``CreateUserAsync``, ``DeleteUserAsync``, ``ChangePasswordAsync``, ``GrantPermissionAsync``, ``RevokePermissionAsync``, ``ListUsersAsync``, ``GetUserPermissionsAsync`` on ``VibeMQClient`` (superuser-only)
- **``ClientOptions.Username`` / ``ClientOptions.Password``** — new client credentials for username/password authorization
- **``BrokerBuilder.UseAuthorization(Action<AuthorizationOptions>)``** — fluent API to enable the new auth mode
- **Backward compatibility** — migration release kept legacy authentication path unchanged (removed in 1.8.0)
- **Docker image** — official image built from ``VibeMQ.Server.Host``; configuration via ``VibeMQ__*`` environment variables and optional config file (see :doc:`docker`)

**Documentation:**

- New :doc:`docker` guide (build, run, environment variables, examples)
- New :doc:`authorization` guide covering users, ACL, glob patterns, admin commands, and security recommendations
- Configuration updated: ``AuthorizationOptions``, ``Username``/``Password`` in ``ClientOptions``
- Server setup updated with ``UseAuthorization`` example and migration note
- Russian translations for all updated pages

**Migration from 1.5.x:**

- No breaking API changes — existing deployments remain compatible
- New ``UseAuthorization()`` is opt-in; existing servers are unaffected
- Legacy authentication fields were marked deprecated in this release

Version 1.5.0
-------------

**Date:** February 2026

**Status:** Stable

**New features:**

- **Queue Declarations** — declare queues in ``ClientOptions`` via ``DeclareQueue()``; client provisions or verifies queues on every ``ConnectAsync``
- **Conflict resolution** — when a queue already exists, compare declared vs live settings; strategies ``Ignore``, ``Fail``, ``Override``; severity classification (Info, Soft, Hard)
- **QueueConflictException** — thrown when ``OnConflict = Fail`` and drift is detected; carries full diff for diagnostics
- **Pre-flight validation** — declarations validated before TCP connection; invalid combinations (e.g. ``RedirectToDlq`` without DLQ) throw immediately
- **FailOnProvisioningError** — option to skip failed provisioning and continue connecting; conflicts always propagate

**Documentation:**

- Client usage guide: Queue Declarations, conflict resolution tables, DI integration
- Configuration: QueueDeclarations, QueueDeclaration, QueueConflictResolution, ConflictSeverity
- Features: GetQueueInfoAsync snapshot, Queue Declarations section
- Russian translations updated for client-usage, configuration, features, protocol

**Migration from 1.4.x:**

- No breaking API changes — queue declarations are optional
- New ``ClientOptions.DeclareQueue()`` fluent API; existing code unchanged if not using declarations

Version 1.4.0
-------------

**Date:** February 2026

**Status:** Stable

**New features:**

- **Frame-level compression** — optional GZip and Brotli compression for protocol frames (negotiated at Connect handshake, configurable threshold)
- **Protocol 1.1** — frame header extended with compression flags byte; mixed-compression streams supported per frame
- **BrokerOptions** — ``SupportedCompressions``, ``CompressionThreshold``; **ClientOptions** — ``PreferredCompressions``, ``CompressionThreshold``

**Documentation:**

- Protocol guide updated with framing format, compression flags, negotiation, and configuration
- Russian translations updated and corrected for protocol, configuration, FAQ, features, and server-setup

**Migration from 1.3.x:**

- No breaking API changes — compression is optional; set empty ``SupportedCompressions`` / ``PreferredCompressions`` to disable
- Protocol version 1.1 adds a compression flags byte to each frame; all clients and servers must use the new frame format

Version 1.3.0
-------------

**Date:** February 2026

**Status:** Stable

**New features:**

- **Persistence layer** — pluggable storage provider system for message durability
- **SQLite storage** — ``VibeMQ.Server.Storage.Sqlite`` package with zero-config file-based persistence
- **Write-ahead pattern** — messages are saved to storage before entering the in-memory queue; on restart all pending messages are recovered automatically
- **IStorageProvider interface** — unified contract covering messages, queue metadata, and DLQ in a single abstraction
- **IStorageManagement interface** — optional maintenance operations: backup, restore, compact (VACUUM), storage statistics
- **Startup recovery** — queues and pending messages are replayed from storage on server start, preserving original ``CreatedAt`` timestamps
- **DLQ persistence** — dead-lettered messages are saved to storage before being added to the in-memory DLQ

**New packages:**

- ``VibeMQ.Server.Storage.Sqlite`` — SQLite storage provider with WAL mode, busy timeout, and automatic schema creation

**Migration from 1.2.x:**

- No breaking changes — existing code works without modifications
- The default storage is still ``InMemoryStorageProvider`` (no persistence)
- Adding persistence is opt-in via ``UseSqliteStorage()`` or ``AddVibeMQSqliteStorage()``
- ``IMessageStore`` is deprecated in favor of ``IStorageProvider`` (still functional, will be removed in 2.0)

Version 1.2.0
-------------

**Date:** February 2026

**Status:** Stable

**New features:**

- **Class-based subscriptions** — subscribe using ``IMessageHandler<T>`` interface instead of lambda handlers
- **Automatic subscription** — handlers with ``[Queue]`` attribute are automatically subscribed on application start via ``AddMessageHandlerSubscriptions()``
- **CreateQueueAsync** — client method to explicitly create queues with custom options
- **Message handler registration** — ``AddMessageHandler<TMessage, THandler>()`` and ``AddMessageHandlers(Assembly)`` for DI integration

**Improvements:**

- Better testability with class-based handlers
- Improved code organization and dependency injection support
- Updated documentation with class-based subscription examples

Version 1.1.0
-------------

**Date:** February 2026

**Status:** Stable

**New features:**

- **IVibeMQClient interface** — injectable client interface for dependency injection
- **ManagedVibeMQClient** — shared, lazily-connected client registered as Singleton in DI
- Single call to ``AddVibeMQClient()`` now registers both ``IVibeMQClientFactory`` and ``IVibeMQClient``
- Client connects automatically on first ``PublishAsync`` or ``SubscribeAsync`` call
- Graceful shutdown with sync-over-async dispose and timeout

**Improvements:**

- Updated documentation with DI usage examples
- Added Russian translations for new sections
- Unit tests for DI client registration

Version 1.0.0
-------------

**Date:** Q2 2026

**Status:** Stable

**New features:**

- Basic message broker functionality
- Pub/Sub with queues
- Delivery guarantees via acknowledgments (ACK)
- Keep-alive (PING/PONG) and automatic reconnections
- Support for delivery modes (round-robin, fan-out)
- username/password authentication
- Graceful shutdown
- Health checks for orchestrators
- Performance metrics collection
- **Custom binary protocol** for improved performance:
  - Fixed field order for unambiguous parsing
  - Payload stored as JSON in UTF-8 for easy debugging and UI display
  - Zero external dependencies
  - Protocol version support for backward compatibility

**Components:**

- ``VibeMQ.Core`` — system core
- ``VibeMQ.Server`` — broker server
- ``VibeMQ.Client`` — client for connection
- ``VibeMQ.Protocol`` — message exchange protocol
- ``VibeMQ.Health`` — HTTP health check server
- ``VibeMQ.Server.DependencyInjection`` — server DI integration
- ``VibeMQ.Client.DependencyInjection`` — client DI integration

**Documentation:**

- Full documentation on ReadTheDocs
- Usage examples
- Deployment guides

Backlog (Version 2.0+)
----------------------

**Planned features:**

- Clustering for horizontal scaling
- Support for other protocols (AMQP, MQTT, HTTP)
- Advanced monitoring (Prometheus, Grafana)
- Delayed messages
- Transactional messages

Version Archive
===============

Version 0.1.0 (Alpha)
---------------------

**Date:** February 2026

**Status:** Alpha version for testing

**Includes:**

- Basic project architecture
- Communication protocol prototype
- Simple queue implementation
- Basic client and server

**Known limitations:**

- No delivery guarantees
- No persistent storage
- Limited documentation
- API may change

Version Format
==============

VibeMQ uses `SemVer 2.0 <https://semver.org/>`_ with version set from the repository tag in the Release pipeline (passed as MSBuild property ``Version``):

**Format:** ``MAJOR.MINOR.PATCH``

- **MAJOR** — incompatible API changes (set via release tag)
- **MINOR** — new functionality, backward compatible (set via release tag)
- **PATCH** — bug fixes (set via release tag)

**Versioning:**

- **Releases:** version is taken from the git tag (e.g. ``v1.0.0``) and passed directly into the pipeline (``-p:Version=...``); no version file in the repo.
- **Local builds:** default ``1.0.0`` (or pass ``-p:Version=1.2.3``).

**Examples:**

- ``1.0.0`` — release from tag ``v1.0.0``
- ``1.1.0`` — release from tag ``v1.1.0``
- ``2.0.0`` — breaking changes release from tag ``v2.0.0``

Pre-release versions:

- ``1.0.0-alpha.1`` — alpha version
- ``1.0.0-beta.1`` — beta version
- ``1.0.0-rc.1`` — release candidate

Versioning Policy
=================

Version Support
---------------

- **Current version** — full support
- **Previous MINOR** — security patch support
- **Older versions** — not supported

Release Schedule
----------------

- **MAJOR releases** — as breaking changes accumulate
- **MINOR releases** — quarterly
- **PATCH releases** — as needed

Change Notifications
--------------------

**Breaking changes** are announced:

- In changelog
- In release notes on GitHub
- One month before new MAJOR version release

Contributing to the Project
===========================

How to Suggest an Improvement
------------------------------

1. Create an issue on GitHub with feature description
2. Discuss feasibility with maintainers
3. If approved — create a pull request

How to Report a Bug
-------------------

1. Check existing issues
2. Create a new issue with description:
   - VibeMQ version
   - Steps to reproduce
   - Expected behavior
   - Actual behavior
   - Logs and errors

How to Contribute to Documentation
-----------------------------------

1. Find a documentation issue
2. Create an issue or pull request
3. Describe proposed changes

Links
=====

- `GitHub Repository <https://github.com/DarkBoy/VibeMQ>`_
- `NuGet Packages <https://www.nuget.org/packages?q=VibeMQ>`_
- `Issues <https://github.com/DarkBoy/VibeMQ/issues>`_
- `Discussions <https://github.com/DarkBoy/VibeMQ/discussions>`_

Last updated: April 4, 2026
