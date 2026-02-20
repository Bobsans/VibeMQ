=========
Changelog
=========

VibeMQ project change history.

.. contents:: Contents
   :local:
   :depth: 2

Version History
================

Version 1.2.0
-------------

**Date:** February 2026

**Status:** Current Stable Version

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
- Token-based authentication
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

- Persistence layer for message storage
- Clustering for horizontal scaling
- Support for other protocols (AMQP, MQTT, HTTP)
- Advanced monitoring (Prometheus, Grafana)
- Web management interface
- .NET 10 support
- Protocol-level compression
- Granular authorization
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

Last updated: February 20, 2026
