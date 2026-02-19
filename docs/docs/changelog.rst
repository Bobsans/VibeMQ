=========
Changelog
=========

VibeMQ project change history.

.. contents:: Contents
   :local:
   :depth: 2

Planned Versions
================

Version 1.0.0 (In Development)
------------------------------

**Date:** Q2 2026

**Status:** Current Stable Version

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
- Class-based subscriptions
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

VibeMQ uses `SemVer 2.0 <https://semver.org/>`_ for versioning with automatic version management via `Nerdbank.GitVersioning <https://github.com/dotnet/Nerdbank.GitVersioning>`_:

**Format:** ``MAJOR.MINOR.PATCH`` or ``MAJOR.MINOR.BUILD``

- **MAJOR** — incompatible API changes (manual increment via git tag)
- **MINOR** — new functionality (backward compatible, automatically incremented in feature branches)
- **PATCH/BUILD** — bug fixes and build number (automatically incremented)

**Automatic Versioning:**

Versions are automatically calculated from git commits and tags:

- **Stable releases:** ``1.0.0`` — version from git tag (e.g., ``v1.0.0``)
- **Development builds:** ``1.0.{build}`` — build number increments with each commit
- **Feature branches:** ``1.1.{build}-feature.{branch}`` — minor version auto-increments, includes branch name
- **Develop branch:** ``1.0.{build}-dev`` — development pre-release version

**Examples:**

- ``1.0.0`` — stable release from tag ``v1.0.0``
- ``1.0.42`` — development build on main branch (42 commits after last tag)
- ``1.1.15-feature.new-auth`` — feature branch with auto-incremented minor version
- ``1.0.50-dev`` — development branch build
- ``2.0.0`` — breaking changes release

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

Last updated: February 19, 2026
