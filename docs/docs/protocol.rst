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
   │  Framing (Length-prefix)            │
   ├─────────────────────────────────────┤
   │  Transport (TCP)                    │
   └─────────────────────────────────────┘

Framing
========

**Length-prefix** approach is used to separate messages in TCP stream:

.. code-block:: text

   [4 bytes: body length in Big Endian uint32] [N bytes: binary body]

Length Format
------------

First 4 bytes of each frame contain message body length encoded as Big Endian:

.. code-block:: csharp

   // Read length
   byte[] lengthBytes = await stream.ReadAsync(4);
   uint length = BitConverter.ToUInt32(lengthBytes.Reverse().ToArray(), 0);
   
   // Write length
   uint length = (uint)messageBytes.Length;
   byte[] lengthBytes = BitConverter.GetBytes(length).Reverse().ToArray();
   await stream.WriteAsync(lengthBytes);

Frame Example
-------------

.. code-block:: text

   Bytes 0-3:   [0x00 0x00 0x00 0x5A]  ← Length 90 bytes
   Bytes 4-93:  [binary message data]  ← Binary protocol message

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
| ``schemaVersion`` | string           | Protocol version (default        │
|                   |                  | "1.0")                            │
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
       "authToken": "my-token"
     }
   }

   Server → Client:
   {
     "id": "conn_001",
     "type": "connectAck",
     "headers": {
       "connectionId": "srv_100"
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

Current version: **1.0**

+----------------+----------------------------------+
| Version         | Changes                           |
+================+==================================+
| 1.0            | Base version                       |
+----------------+----------------------------------+

Next Steps
==========

- :doc:`architecture` — system architecture
- :doc:`features` — VibeMQ capabilities
- :doc:`monitoring` — monitoring
