=================
Communication Protocol
=================

This guide describes the VibeMQ message exchange protocol.

.. contents:: Contents
   :local:
   :depth: 2

Overview
========

VibeMQ uses TCP as transport and a custom binary format for message serialization. The protocol is designed for simplicity and performance. Payload data is stored as JSON in UTF-8 for easy debugging and UI display, while the protocol message structure uses a compact binary format.

Protocol Layers
================

.. code-block:: text

   ┌─────────────────────────────────────┐
   │  Application Layer (Binary)         │
   ├─────────────────────────────────────┤
   │  Compression (optional, per-frame)  │
   ├─────────────────────────────────────┤
   │  Framing (Length-prefix)            │
   ├─────────────────────────────────────┤
   │  Transport (TCP)                    │
   └─────────────────────────────────────┘

Framing
========

**Length-prefix** approach is used to separate messages in the TCP stream.
Each frame is self-contained and carries its own compression information.

Frame Format
------------

.. code-block:: text

   [4 bytes: body length in Big Endian uint32] [1 byte: compression flags] [N bytes: body]

- **body length** — size of the (possibly compressed) body in bytes.
- **compression flags** — algorithm identifier (see :ref:`compression-flags`).
- **body** — serialized (and optionally compressed) ``ProtocolMessage``.

.. _compression-flags:

Compression Flags Byte
~~~~~~~~~~~~~~~~~~~~~~

+--------+-----------------------------+
| Value  | Meaning                     |
+========+=============================+
| 0x00   | No compression              |
+--------+-----------------------------+
| 0x01   | GZip                        |
+--------+-----------------------------+
| 0x02   | Brotli                      |
+--------+-----------------------------+

Frame Example (no compression)
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

.. code-block:: text

   Bytes 0-3:   [0x00 0x00 0x00 0x5A]  ← Body length: 90 bytes
   Byte  4:     [0x00]                 ← No compression
   Bytes 5-94:  [binary message data]  ← Binary protocol message

Frame Example (Brotli)
~~~~~~~~~~~~~~~~~~~~~~

.. code-block:: text

   Bytes 0-3:   [0x00 0x00 0x00 0x2C]  ← Compressed body length: 44 bytes
   Byte  4:     [0x02]                 ← Brotli compression
   Bytes 5-48:  [compressed data]      ← Brotli-compressed binary message

Compression
===========

VibeMQ supports optional frame-level compression using GZip and Brotli (both built in to .NET,
no external dependencies). Compression is negotiated during the Connect handshake and applied
transparently to all subsequent frames.

Negotiation
-----------

The client advertises its preferred algorithms in the Connect message (comma-separated, in
descending priority). The server replies with the selected algorithm in ConnectAck. If the
server omits the header, no compression is used.

.. code-block:: text

   Client → Connect:
   {
     "headers": {
       "supported-compression": "brotli,gzip"
     }
   }

   Server → ConnectAck:
   {
     "headers": {
       "negotiated-compression": "brotli"
     }
   }

After the handshake, ``FrameWriter`` on both sides uses the negotiated algorithm. ``FrameReader``
always reads the compression flag directly from each frame, so mixed-compression streams are
handled correctly without shared state.

Threshold
---------

Compression is applied only when the serialized body reaches the configured threshold
(default **1 024 bytes**). Frames below the threshold are sent uncompressed (flags = ``0x00``).

Configuration
-------------

**Server** (``BrokerOptions``):

.. code-block:: csharp

   builder.ConfigureFrom(new BrokerOptions {
       SupportedCompressions = [CompressionAlgorithm.Brotli, CompressionAlgorithm.GZip],
       CompressionThreshold = 1024,
   });

**Client** (``ClientOptions``):

.. code-block:: csharp

   var options = new ClientOptions {
       PreferredCompressions = [CompressionAlgorithm.Brotli, CompressionAlgorithm.GZip],
       CompressionThreshold = 1024,
   };

   // Disable compression entirely
   PreferredCompressions = []   // client side
   SupportedCompressions = []   // server side

Compression Headers
-------------------

+------------------------------+----------------------------+---------------------------------------------+
| Header                       | Direction                  | Description                                 |
+==============================+============================+=============================================+
| ``supported-compression``    | Client → Server (Connect)  | Comma-separated preferred algorithms        |
+------------------------------+----------------------------+---------------------------------------------+
| ``negotiated-compression``   | Server → Client (ConnectAck)| Selected algorithm; absent if none agreed  |
+------------------------------+----------------------------+---------------------------------------------+

Message Format
================

Binary Protocol Structure
-------------------------

All protocol messages are serialized in a custom binary format with fixed field order:

.. code-block:: text

   version (1 byte) | type (1 byte) | id (2B len + UTF-8) | 
   queue (2B len + UTF-8) | payload (4B len + UTF-8 JSON) | 
   headers (2B count + pairs) | errorCode (2B len + UTF-8) | 
   errorMessage (2B len + UTF-8)

All lengths are encoded as Big Endian. Length 0 means the field is absent/null.

**Important:** Payload is stored as JSON in UTF-8 for easy debugging and UI display, but the protocol message structure itself uses binary encoding for optimal performance.

Logical Structure
-----------------

The logical structure of messages (shown as JSON for clarity):

.. code-block:: json

   {
     "version": 1,
     "id": "msg_123",
     "type": "publish",
     "queue": "notifications",
     "payload": {...},
     "headers": {...},
     "errorCode": null,
     "errorMessage": null
   }

Message Fields
--------------

+-------------------+------------------+----------------------------------+
| Field             | Type             | Description                       |
+===================+==================+==================================+
| ``id``            | string           | Unique identifier                 │
+-------------------+------------------+----------------------------------+
| ``type``          | CommandType      | Command type                      │
+-------------------+------------------+----------------------------------+
| ``queue``         | string?          | Queue name                        │
+-------------------+------------------+----------------------------------+
| ``payload``       | JsonElement?     | Payload                           │
+-------------------+------------------+----------------------------------+
| ``headers``       | object?          | Headers                           │
+-------------------+------------------+----------------------------------+
| ``version``       | int              | Protocol version (default: 1)     │
+-------------------+------------------+----------------------------------+
| ``errorCode``     | string?          | Error code (for Error)            │
+-------------------+------------------+----------------------------------+
| ``errorMessage``  | string?          | Error message                     │
+-------------------+------------------+----------------------------------+

Command Types (CommandType)
=========================

.. code-block:: csharp

   public enum CommandType {
       // Connection management
       Connect = 0,
       ConnectAck = 1,
       Disconnect = 2,

       // Keep-alive
       Ping = 10,
       Pong = 11,

       // Pub/Sub
       Publish = 20,
       PublishAck = 21,
       Subscribe = 22,
       SubscribeAck = 23,
       Unsubscribe = 24,
       UnsubscribeAck = 25,
       Deliver = 26,

       // Acknowledgment
       Ack = 30,

       // Queue management
       CreateQueue = 40,
       DeleteQueue = 41,
       QueueInfo = 42,
       ListQueues = 43,

       // Errors
       Error = 99,
   }

Command Descriptions
===============

Connection Management
----------------------

Connect
~~~~~~~

**Direction:** Client → Server

**Description:** Connection request.

.. code-block:: json

   {
     "id": "conn_123",
     "type": "connect",
     "headers": {
       "authToken": "my-secret-token",
       "clientVersion": "1.0.0"
     }
   }

ConnectAck
~~~~~~~~~~

**Direction:** Server → Client

**Description:** Connection acknowledgment.

.. code-block:: json

   {
     "id": "conn_123",
     "type": "connectAck",
     "headers": {
       "serverVersion": "1.0.0",
       "connectionId": "srv_456"
     }
   }

Disconnect
~~~~~~~~~~

**Direction:** Client → Server

**Description:** Disconnection request.

.. code-block:: json

   {
     "id": "disc_123",
     "type": "disconnect"
   }

Keep-alive
----------

Ping
~~~~

**Direction:** Client → Server

**Description:** Connection keep-alive check.

.. code-block:: json

   {
     "id": "ping_123",
     "type": "ping"
   }

Pong
~~~~

**Direction:** Server → Client

**Description:** Response to Ping.

.. code-block:: json

   {
     "id": "ping_123",
     "type": "pong"
   }

Publish/Subscribe
-------------------

Publish
~~~~~~~

**Direction:** Client → Server

**Description:** Publish message to queue.

.. code-block:: json

   {
     "id": "msg_123",
     "type": "publish",
     "queue": "notifications",
     "payload": {
       "title": "Hello",
       "body": "World"
     },
     "headers": {
       "priority": "high",
       "correlationId": "corr_456",
       "timestamp": "2026-02-18T10:30:00Z"
     }
   }

PublishAck
~~~~~~~~~~

**Direction:** Server → Client

**Description:** Publish acknowledgment.

.. code-block:: json

   {
     "id": "msg_123",
     "type": "publishAck",
     "headers": {
       "messageId": "msg_123",
       "queueName": "notifications"
     }
   }

Subscribe
~~~~~~~~~

**Direction:** Client → Server

**Description:** Subscribe to queue.

.. code-block:: json

   {
     "id": "sub_123",
     "type": "subscribe",
     "queue": "notifications"
   }

SubscribeAck
~~~~~~~~~~~~

**Direction:** Server → Client

**Description:** Subscription acknowledgment.

.. code-block:: json

   {
     "id": "sub_123",
     "type": "subscribeAck",
     "headers": {
       "queueName": "notifications",
       "subscriptionId": "sub_123"
     }
   }

Unsubscribe
~~~~~~~~~~~

**Direction:** Client → Server

**Description:** Unsubscribe from queue.

.. code-block:: json

   {
     "id": "unsub_123",
     "type": "unsubscribe",
     "queue": "notifications"
   }

UnsubscribeAck
~~~~~~~~~~~~~~

**Direction:** Server → Client

**Description:** Unsubscription acknowledgment.

.. code-block:: json

   {
     "id": "unsub_123",
     "type": "unsubscribeAck",
     "headers": {
       "queueName": "notifications"
     }
   }

Deliver
~~~~~~~

**Direction:** Server → Client

**Description:** Message delivery to subscriber.

.. code-block:: json

   {
     "id": "msg_123",
     "type": "deliver",
     "queue": "notifications",
     "payload": {
       "title": "Hello",
       "body": "World"
     },
     "headers": {
       "priority": "high",
       "deliveryAttempts": "1"
     }
   }

Acknowledgment
-------------

Ack
~~~

**Direction:** Client → Server

**Description:** Message receipt acknowledgment.

.. code-block:: json

   {
     "id": "ack_123",
     "type": "ack",
     "headers": {
       "messageId": "msg_123"
     }
   }

Queue Management
--------------------

CreateQueue
~~~~~~~~~~~

**Direction:** Client → Server

**Description:** Create queue.

.. code-block:: json

   {
     "id": "create_123",
     "type": "createQueue",
     "queue": "my-queue",
     "headers": {
       "deliveryMode": "RoundRobin",
       "maxQueueSize": "10000",
       "messageTtl": "3600000"
     }
   }

DeleteQueue
~~~~~~~~~~~

**Direction:** Client → Server

**Description:** Delete queue.

.. code-block:: json

   {
     "id": "delete_123",
     "type": "deleteQueue",
     "queue": "my-queue"
   }

QueueInfo
~~~~~~~~~

**Direction:** Client → Server, Server → Client

**Description:** Queue information request/response.

**Request:**

.. code-block:: json

   {
     "id": "info_123",
     "type": "queueInfo",
     "queue": "my-queue"
   }

**Response:**

.. code-block:: json

   {
     "id": "info_123",
     "type": "queueInfo",
     "queue": "my-queue",
     "payload": {
       "name": "my-queue",
       "messageCount": 42,
       "subscriberCount": 3,
       "deliveryMode": "RoundRobin",
       "maxSize": 10000,
       "createdAt": "2026-02-18T10:00:00Z"
     }
   }

ListQueues
~~~~~~~~~~

**Direction:** Client → Server, Server → Client

**Description:** Queue list request/response.

**Request:**

.. code-block:: json

   {
     "id": "list_123",
     "type": "listQueues"
   }

**Response:**

.. code-block:: json

   {
     "id": "list_123",
     "type": "listQueues",
     "payload": ["queue1", "queue2", "queue3"]
   }

Errors
------

Error
~~~~~

**Direction:** Server → Client

**Description:** Error message.

.. code-block:: json

   {
     "id": "err_123",
     "type": "error",
     "errorCode": "AUTH_FAILED",
     "errorMessage": "Invalid authentication token"
   }

Error Codes
~~~~~~~~~~~

+------------------------+----------------------------------+
| Code                    | Description                       |
+========================+==================================+
| ``AUTH_FAILED``        | Authentication error              |
+------------------------+----------------------------------+
| ``INVALID_MESSAGE``    | Invalid message format            |
+------------------------+----------------------------------+
| ``QUEUE_NOT_FOUND``    | Queue not found                   |
+------------------------+----------------------------------+
| ``QUEUE_EXISTS``       | Queue already exists               |
+------------------------+----------------------------------+
| ``RATE_LIMITED``       | Rate limit exceeded               |
+------------------------+----------------------------------+
| ``SERVER_ERROR``       | Internal server error             |
+------------------------+----------------------------------+

Headers
===================

Common Headers
---------------

+------------------------+----------------------------------+
| Header                  | Description                       |
+========================+==================================+
| ``authToken``          | Authentication token              |
+------------------------+----------------------------------+
| ``clientVersion``      | Client version                    |
+------------------------+----------------------------------+
| ``serverVersion``      | Server version                    |
+------------------------+----------------------------------+
| ``connectionId``       | Connection identifier             |
+------------------------+----------------------------------+

Message Headers
-------------------

+------------------------+----------------------------------+
| Header                  | Description                       |
+========================+==================================+
| ``priority``           | Priority (Low, Normal, High,     │
|                        │ Critical)                         |
+------------------------+----------------------------------+
| ``correlationId``      | ID for request correlation        |
+------------------------+----------------------------------+
| ``timestamp``          | Creation time (ISO 8601)           |
+------------------------+----------------------------------+
| ``deliveryAttempts``   | Number of delivery attempts       |
+------------------------+----------------------------------+
| ``messageId``          | Message ID                        |
+------------------------+----------------------------------+
| ``queueName``          | Queue name                        |
+------------------------+----------------------------------+
| ``subscriptionId``     | Subscription ID                   |
+------------------------+----------------------------------+

Queue Headers
------------------

+------------------------+----------------------------------+
| Header                  | Description                       |
+========================+==================================+
| ``deliveryMode``       | Delivery mode                     |
+------------------------+----------------------------------+
| ``maxQueueSize``       | Maximum size                      |
+------------------------+----------------------------------+
| ``messageTtl``         | TTL in milliseconds              |
+------------------------+----------------------------------+
| ``enableDeadLetterQueue`` | Enable DLQ                      |
+------------------------+----------------------------------+
| ``maxRetryAttempts``   | Max delivery attempts             |
+------------------------+----------------------------------+

Exchange Examples
==============

Connection Establishment
--------------------

.. code-block:: text

   Client → Server:
   {
     "id": "conn_001",
     "type": "connect",
     "headers": {
       "authToken": "my-token",
       "supported-compression": "brotli,gzip"
     }
   }

   Server → Client:
   {
     "id": "conn_001",
     "type": "connectAck",
     "headers": {
       "connectionId": "srv_100",
       "negotiated-compression": "brotli"
     }
   }

Message Publishing
--------------------

.. code-block:: text

   Client → Server:
   {
     "id": "msg_001",
     "type": "publish",
     "queue": "notifications",
     "payload": {
       "title": "Hello",
       "body": "World"
     }
   }

   Server → Client:
   {
     "id": "msg_001",
     "type": "publishAck",
     "headers": {
       "messageId": "msg_001",
       "queueName": "notifications"
     }
   }

Message Delivery
------------------

.. code-block:: text

   Server → Client:
   {
     "id": "msg_001",
     "type": "deliver",
     "queue": "notifications",
     "payload": {
       "title": "Hello",
       "body": "World"
     }
   }

   Client → Server:
   {
     "id": "ack_001",
     "type": "ack",
     "headers": {
       "messageId": "msg_001"
     }
   }

Keep-alive
----------

.. code-block:: text

   Client → Server:
   {
     "id": "ping_001",
     "type": "ping"
   }

   Server → Client:
   {
     "id": "ping_001",
     "type": "pong"
   }

Protocol Versions
================

Current version: **1.1**

+----------------+---------------------------------------------------+
| Version        | Changes                                           |
+================+===================================================+
| 1.0            | Base version                                      |
+----------------+---------------------------------------------------+
| 1.1            | Frame header extended with compression flags byte |
+----------------+---------------------------------------------------+

Next Steps
==========

- :doc:`architecture` — system architecture
- :doc:`features` — VibeMQ capabilities
- :doc:`monitoring` — monitoring
