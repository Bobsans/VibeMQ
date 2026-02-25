=============
Authorization
=============

This guide describes the username/password authorization system with per-queue ACL in VibeMQ.

.. contents:: Contents
   :local:
   :depth: 2

Overview
========

VibeMQ supports two authentication modes:

.. list-table::
   :header-rows: 1
   :widths: 22 38 30

   * - Mode
     - Description
     - When to use
   * - **Legacy token**
     - Single shared token for all clients
     - Simple setups, backward compatibility
   * - **Username/password + ACL**
     - Per-user BCrypt passwords with per-queue ACL stored in SQLite
     - Production, multi-tenant, fine-grained access control

When authorization is enabled via ``UseAuthorization()``, the legacy token mode
is automatically disabled. Without any auth configured, the broker accepts all
connections anonymously.

Quick Start
===========

.. code-block:: csharp

   var broker = BrokerBuilder.Create()
       .UsePort(2925)
       .UseAuthorization(o => {
           o.SuperuserUsername = "admin";
           o.SuperuserPassword = Environment.GetEnvironmentVariable("VIBEMQ_ADMIN_PASS");
           o.DatabasePath = "/var/lib/vibemq/auth.db";
       })
       .Build();

Connect the client:

.. code-block:: csharp

   await using var client = await VibeMQClient.ConnectAsync(
       "localhost",
       2925,
       new ClientOptions {
           Username = "alice",
           Password = "alice-secret"
       }
   );

Configuration
=============

AuthorizationOptions
--------------------

.. code-block:: csharp

   public sealed class AuthorizationOptions {
       public string SuperuserUsername { get; set; } = "vibemq";
       public string SuperuserPassword { get; set; } = "";
       public string DatabasePath { get; set; } = "auth.db";
   }

.. list-table::
   :header-rows: 1
   :widths: 28 14 48

   * - Property
     - Default
     - Description
   * - ``SuperuserUsername``
     - ``"vibemq"``
     - Username for the built-in superuser account.
   * - ``SuperuserPassword``
     - ``""``
     - Password for the superuser. **Must be set** before the first run.
       If empty and the account does not exist yet, the broker throws on startup.
   * - ``DatabasePath``
     - ``"auth.db"``
     - Path to the SQLite file that stores users and ACL entries.
       Separate from the message storage database.

.. warning::

   The superuser account is seeded automatically on the first broker start when
   the database is empty. If ``SuperuserPassword`` is empty at that point, the
   broker will throw and refuse to start. Store the password securely (e.g.
   environment variable, secrets manager).

Superuser
=========

The superuser account is a special account that bypasses all permission checks.
It is the only account that can execute admin commands (create/delete users,
grant/revoke permissions).

**Properties of the superuser:**

- Always authorized for every queue and every operation.
- Cannot be deleted via the ``AdminDeleteUser`` command (lockout protection).
- Password can be changed by any authenticated superuser via ``AdminChangePassword``.

Users and Passwords
===================

Passwords are hashed using **BCrypt** with a work factor of 12.
The hash is stored in the ``auth.db`` SQLite database.

Creating users (admin protocol command):

.. code-block:: text

   Command: AdminCreateUser
   Headers:
     username  — new user's username
     password  — plaintext password (hashed server-side)

Changing passwords:

.. code-block:: text

   Command: AdminChangePassword
   Headers:
     username    — target user (superuser can change any; regular user can only change own)
     newPassword — new plaintext password

Deleting users:

.. code-block:: text

   Command: AdminDeleteUser
   Headers:
     username — user to delete (cannot delete superuser accounts)

Per-Queue ACL
=============

Every user has a list of *permission entries*. Each entry associates a
**queue pattern** (glob) with a set of **allowed operations**.

QueueOperation
--------------

.. code-block:: csharp

   public enum QueueOperation {
       Publish,
       Subscribe,
       CreateQueue,
       DeleteQueue,
       PurgeQueue,
       GetQueueInfo,
       ListQueues,
   }

Glob Patterns
-------------

Queue patterns use ``*`` as a wildcard that matches **any sequence of characters,
including dots**. Matching is case-insensitive.

.. list-table::
   :header-rows: 1
   :widths: 28 62

   * - Pattern
     - Matches
   * - ``*``
     - Any queue name.
   * - ``orders.*``
     - ``orders.new``, ``orders.shipped``, ``orders.test.deep``, …
   * - ``*.events``
     - ``orders.events``, ``payments.events``, …
   * - ``tenant.alice.*``
     - Any queue under the ``tenant.alice.`` namespace.
   * - ``invoices``
     - Exactly ``invoices`` (case-insensitive).

**Union semantics:** when a user has multiple matching entries, the effective
permission is the **union** of all matched operations.

Granting permissions (admin protocol command):

.. code-block:: text

   Command: AdminGrantPermission
   Headers:
     username      — target user
     queuePattern  — glob pattern (e.g. "orders.*")
     operations    — comma-separated operations (e.g. "Publish,Subscribe")

Revoking permissions:

.. code-block:: text

   Command: AdminRevokePermission
   Headers:
     username      — target user
     queuePattern  — pattern to revoke (must match an existing entry exactly)

Permission Caching
==================

When a client connects and authenticates successfully, the server loads all
permission entries for that user and stores them in the session. All subsequent
authorization checks use this in-memory cache — no database I/O per request.

**Implication:** if you grant or revoke permissions for a connected user, the
change takes effect only after the user reconnects.

Admin Protocol Commands
=======================

Admin commands are available **only to superuser** sessions.

.. list-table::
   :header-rows: 1
   :widths: 32 58

   * - Command
     - Description
   * - ``AdminCreateUser``
     - Create a new user with a BCrypt-hashed password.
   * - ``AdminDeleteUser``
     - Delete a user (superusers cannot be deleted).
   * - ``AdminChangePassword``
     - Change a user's password (superuser: any user; regular: own only).
   * - ``AdminGrantPermission``
     - Add a permission entry (queue pattern + operations) for a user.
   * - ``AdminRevokePermission``
     - Remove a specific permission entry for a user.
   * - ``AdminListUsers``
     - List all users (returns username, isSuperuser, createdAt).
   * - ``AdminGetUserPermissions``
     - List all ACL entries for a specific user.

ListQueues Filtering
====================

When authorization is enabled, ``ListQueues`` returns only the queues
that match at least one of the user's permission patterns with the
``ListQueues`` operation. Superusers always see all queues.

Security Recommendations
=========================

.. warning::

   Follow these guidelines for production deployments:

- Store ``SuperuserPassword`` in an environment variable or secrets manager;
  never hard-code it.
- Use a dedicated ``DatabasePath`` outside the application directory with
  appropriate file system permissions (readable only by the broker process).
- Combine authorization with TLS (:doc:`server-setup`) so credentials are
  never transmitted in cleartext.
- Apply the *principle of least privilege*: grant each user only the operations
  and queue patterns they actually need.
- Rotate passwords regularly using ``AdminChangePassword``.

Examples
========

Multi-tenant Setup
------------------

.. code-block:: csharp

   // Server
   var broker = BrokerBuilder.Create()
       .UsePort(2925)
       .UseAuthorization(o => {
           o.SuperuserPassword = Environment.GetEnvironmentVariable("VIBEMQ_ADMIN_PASS");
           o.DatabasePath = "/var/lib/vibemq/auth.db";
       })
       .Build();

After startup, create tenants via admin commands (pseudocode — replace with a
management tool or admin client that sends raw protocol messages):

.. code-block:: text

   AdminCreateUser: username=alice, password=<secret>
   AdminGrantPermission: username=alice, queuePattern=tenant.alice.*, operations=Publish,Subscribe,CreateQueue,GetQueueInfo,ListQueues

   AdminCreateUser: username=bob, password=<secret>
   AdminGrantPermission: username=bob, queuePattern=tenant.bob.*, operations=Publish,Subscribe,CreateQueue,GetQueueInfo,ListQueues

Connect as tenant:

.. code-block:: csharp

   await using var client = await VibeMQClient.ConnectAsync(
       "localhost", 2925,
       new ClientOptions { Username = "alice", Password = "alice-secret" }
   );

   // alice can only access tenant.alice.* queues
   await client.PublishAsync("tenant.alice.orders", new { OrderId = 1 });

Worker Pool (Publish-only)
--------------------------

.. code-block:: text

   AdminCreateUser: username=worker-1, password=<secret>
   AdminGrantPermission: username=worker-1, queuePattern=jobs.*, operations=Publish

.. code-block:: csharp

   await using var worker = await VibeMQClient.ConnectAsync(
       "localhost", 2925,
       new ClientOptions { Username = "worker-1", Password = "..." }
   );

   await worker.PublishAsync("jobs.process", new { TaskId = "abc" });

Next Steps
==========

- :doc:`server-setup` — TLS encryption for secure credential transmission
- :doc:`configuration` — full BrokerOptions and ClientOptions reference
- :doc:`features` — all broker features overview
