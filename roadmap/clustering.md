# VibeMQ Clustering — Design Plan

## 1. Goals and Non-Goals

### Goals
- Allow multiple VibeMQ broker nodes to work as a single logical broker
- Transparent to clients: connect to any node, publish/subscribe as usual
- Exactly-once delivery for RoundRobin across the whole cluster
- FanOut delivers to all subscribers across all nodes
- Automatic failover when a node goes down
- No new mandatory external dependencies (clustering is opt-in)
- Embeddable: clustering is configured via the existing `BrokerBuilder` API

### Non-Goals
- Total ordering of messages across queues (within a queue, order is preserved per-node)
- Strong consistency under network partition (we target AP, not CP)
- Cross-datacenter replication (same LAN / private network assumed)
- Replace a dedicated MQ broker like Kafka for very high throughput scenarios

---

## 2. Mental Model

A VibeMQ cluster is a **mesh of broker nodes** where:

- Every node can accept client connections and serve publish/subscribe operations
- Nodes discover each other through a shared **cluster registry** (stored in the shared persistence backend)
- After discovery, nodes establish **direct TCP connections** to each other for low-latency event forwarding
- A shared **cluster coordinator** (backed by the same storage) handles atomic operations: message claiming for RoundRobin, FanOut ACK counting, subscription counts

Clients interact with any node. They don't need to know about the cluster topology.

---

## 3. Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                        Client Layer                             │
│   VibeMQClient     VibeMQClient     VibeMQClient                │
└───────┬─────────────────┬────────────────────┬──────────────────┘
        │                 │                    │
        ▼                 ▼                    ▼
┌───────────────┐ ┌───────────────┐ ┌───────────────┐
│    Node A     │ │    Node B     │ │    Node C     │
│ (BrokerServer)│ │ (BrokerServer)│ │ (BrokerServer)│
│               │◄──────────────►│◄──────────────►│
│  ClusterMesh  │ │  ClusterMesh  │ │  ClusterMesh  │
└───────┬───────┘ └───────┬───────┘ └───────┬───────┘
        │                 │                  │
        └────────┬─────────────────┬──────────┘
                 ▼                 ▼
        ┌─────────────────────────────┐
        │   Shared Cluster Storage    │
        │  (PostgreSQL / Redis / etc) │
        │                             │
        │  • cluster_nodes            │
        │  • cluster_subscriptions    │
        │  • message_claims           │
        │  • fanout_ack_counters      │
        │  • messages (WAL)           │
        └─────────────────────────────┘
```

Each node has two listening ports:
- **Client port** (default: 2925) — existing protocol, client connections
- **Cluster port** (default: clientPort + 10000, e.g., 12925) — internal cluster mesh

---

## 4. New Abstractions

### 4.1 `IClusterCoordinator`

Responsible for shared cluster-wide state. Backed by the cluster storage backend.

```csharp
public interface IClusterCoordinator : IAsyncDisposable
{
    // Node lifecycle
    Task RegisterNodeAsync(NodeInfo self, CancellationToken ct);
    Task UpdateHeartbeatAsync(CancellationToken ct);
    Task UnregisterNodeAsync(CancellationToken ct);
    Task<IReadOnlyList<NodeInfo>> GetActiveNodesAsync(CancellationToken ct);

    // Cluster-wide subscription tracking
    Task IncrementSubscriberCountAsync(string nodeId, string queueName, CancellationToken ct);
    Task DecrementSubscriberCountAsync(string nodeId, string queueName, CancellationToken ct);
    Task<IReadOnlyList<QueueSubscriberMap>> GetSubscriberMapAsync(string queueName, CancellationToken ct);

    // RoundRobin: atomic message claim
    // Returns true if this node successfully claimed the message for delivery
    Task<bool> TryClaimMessageAsync(string messageId, string nodeId, CancellationToken ct);
    Task ReleaseClaimAsync(string messageId, CancellationToken ct);   // on node failure

    // FanOut with ACK: distributed counter
    Task InitFanOutTrackingAsync(string messageId, int expectedAckCount, CancellationToken ct);
    Task<int> RecordFanOutAckAsync(string messageId, CancellationToken ct); // returns remaining
}
```

### 4.2 `IClusterTransport`

Responsible for node-to-node TCP messaging.

```csharp
public interface IClusterTransport : IAsyncDisposable
{
    NodeInfo LocalNode { get; }

    Task StartAsync(CancellationToken ct);
    Task StopAsync(CancellationToken ct);

    // Send a message to a specific node
    Task SendAsync(string targetNodeId, ClusterEnvelope msg, CancellationToken ct);

    // Broadcast to all connected peers
    Task BroadcastAsync(ClusterEnvelope msg, CancellationToken ct);

    // Incoming cluster messages from peers
    IAsyncEnumerable<ClusterEnvelope> IncomingMessages { get; }

    // Peer connection events
    event Action<NodeInfo> PeerConnected;
    event Action<string> PeerDisconnected;  // nodeId
}
```

### 4.3 `NodeInfo`

```csharp
public class NodeInfo
{
    public string NodeId { get; init; }          // stable GUID
    public string ClusterHost { get; init; }     // reachable hostname/IP
    public int ClusterPort { get; init; }        // cluster mesh port
    public int ClientPort { get; init; }         // client-facing port (for client hints)
    public DateTime JoinedAt { get; init; }
    public DateTime LastHeartbeat { get; set; }
    public bool IsActive { get; set; }
}
```

### 4.4 `ClusterEnvelope` and `ClusterMessageType`

```csharp
public enum ClusterMessageType
{
    Hello,                    // Initial handshake
    Heartbeat,                // Alive ping (redundant with DB but faster)
    Bye,                      // Graceful shutdown notification

    MessageAvailable,         // "A new message is in queue X" → for FanOut
    DeliverMessage,           // "Deliver this specific message to your local subscribers"
    AckForward,               // Forward ACK from subscriber to originating node
    NackForward,              // Forward NACK

    SubscriptionAdded,        // "I now have N subscribers for queue X"
    SubscriptionRemoved,

    PermissionInvalidated,    // "Invalidate permission cache for user Y"
}

public class ClusterEnvelope
{
    public string MessageId { get; init; }          // idempotency
    public ClusterMessageType Type { get; init; }
    public string SourceNodeId { get; init; }
    public string? TargetNodeId { get; init; }      // null = broadcast
    public string? QueueName { get; init; }
    public string? BrokerMessageId { get; init; }   // reference to broker message
    public byte[]? Payload { get; init; }           // serialized BrokerMessage (for DeliverMessage)
    public Dictionary<string, string>? Meta { get; init; }
    public DateTime SentAt { get; init; }
}
```

### 4.5 `ClusterOptions`

```csharp
public class ClusterOptions
{
    public string NodeId { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string ClusterHost { get; set; } = "127.0.0.1";
    public int ClusterPort { get; set; }        // defaults to ClientPort + 10000
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan NodeTimeout { get; set; } = TimeSpan.FromSeconds(15);
    public TimeSpan PeerReconnectDelay { get; set; } = TimeSpan.FromSeconds(3);
    public int MaxPeerReconnectAttempts { get; set; } = 10;
}
```

---

## 5. Cluster Storage Schema

The cluster coordinator needs a handful of tables in the shared storage (PostgreSQL as reference implementation).

```sql
-- Active cluster nodes
CREATE TABLE cluster_nodes (
    node_id          TEXT PRIMARY KEY,
    cluster_host     TEXT NOT NULL,
    cluster_port     INT  NOT NULL,
    client_port      INT  NOT NULL,
    joined_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_heartbeat   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_active        BOOLEAN NOT NULL DEFAULT TRUE
);

-- Cluster-wide subscription index (node → queue → subscriber count)
CREATE TABLE cluster_subscriptions (
    node_id          TEXT NOT NULL,
    queue_name       TEXT NOT NULL,
    subscriber_count INT  NOT NULL DEFAULT 0,
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (node_id, queue_name)
);

-- RoundRobin: atomic message claims
CREATE TABLE message_claims (
    message_id       TEXT PRIMARY KEY,
    claimed_by       TEXT NOT NULL,     -- node_id
    claimed_at       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- FanOut ACK tracking: how many ACKs still expected
CREATE TABLE fanout_ack_counters (
    message_id       TEXT PRIMARY KEY,
    remaining        INT  NOT NULL,
    total            INT  NOT NULL,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

For the message claim, the atomic upsert is:
```sql
INSERT INTO message_claims (message_id, claimed_by)
VALUES (@messageId, @nodeId)
ON CONFLICT DO NOTHING
RETURNING claimed_by;
-- If returned claimed_by = @nodeId → this node won the claim
-- Otherwise → another node claimed it first
```

---

## 6. Node Lifecycle

### 6.1 Startup

```
1. Generate or load persistent NodeId (from config or stored file)
2. Start cluster port TCP listener (IClusterTransport.StartAsync)
3. Register self in cluster_nodes via IClusterCoordinator.RegisterNodeAsync
4. Query GetActiveNodesAsync → get list of existing peers
5. Initiate TCP connections to all known peers
6. Send Hello to each peer (exchange node metadata)
7. Start heartbeat loop (update last_heartbeat in DB every HeartbeatInterval)
8. Start failure detection loop (scan cluster_nodes for stale last_heartbeat)
```

### 6.2 Peer Connection

When a new peer connects or is discovered:
```
1. Receive Hello → store peer's NodeInfo
2. Sync subscription state: query cluster_subscriptions for this peer's queues
3. Keep TCP connection alive (application-level Heartbeat every 5s)
4. On TCP disconnect: mark peer as tentatively down, retry connection
5. If DB last_heartbeat also stale → mark as dead, trigger failure handling
```

### 6.3 Graceful Shutdown

```
1. Send Bye to all peers
2. Call IClusterCoordinator.UnregisterNodeAsync (set is_active = false)
3. Wait for in-flight messages to drain (existing 30s grace period)
4. Close cluster TCP listener and all peer connections
```

### 6.4 Failure Detection

Background loop on each node (runs every `HeartbeatInterval`):
```
1. Fetch all cluster_nodes where is_active = true
2. For each node where (now - last_heartbeat) > NodeTimeout:
   a. Try to TCP-connect to that node → if connection refused → node is dead
   b. Mark node as inactive (optimistic UPDATE with CHECK on last_heartbeat)
   c. Trigger ReclaimDeadNodeMessages(deadNodeId)
```

> **Note:** Multiple nodes may try to declare a dead node simultaneously. The atomic UPDATE with `WHERE last_heartbeat = <stale value>` ensures only one node "wins" the cleanup.

---

## 7. Message Flow in Cluster

### 7.1 Publish

```
Client → [any Node X]
    1. Node X receives Publish command
    2. Node X saves message to shared storage (WAL, existing behavior)
    3. Node X enqueues message in local QueueManager
    4. Node X calls DeliverAsync (existing logic)
    5. Based on DeliveryMode → see sections 7.2 / 7.3 / 7.4
```

### 7.2 RoundRobin Delivery (Exactly-Once Across Cluster)

The key insight: **any node that has a subscriber can compete to deliver a message**.

```
Node X (publisher) delivers locally first:
    1. Check local subscribers for this queue
    2. If local subscriber available:
       a. TryClaimMessage(messageId, nodeX) → must succeed (atomic)
       b. If claim won → deliver to local subscriber → done
    3. If no local subscriber OR claim lost:
       a. Broadcast ClusterMessageType.MessageAvailable{queueName, messageId}

Peer nodes (Y, Z) receive MessageAvailable:
    1. Check: do I have local subscribers for this queue?
    2. If yes: TryClaimMessage(messageId, nodeId)
    3. If claim won:
       a. Fetch message from shared storage
       b. Deliver to next local round-robin subscriber
       c. Track in local AckTracker
    4. If claim lost: ignore (another node already handling it)

On subscriber ACK:
    1. Local node removes from AckTracker
    2. Removes message from shared storage
    3. Broadcasts AckForward if message was published by a different node (optional, for metrics)
```

**Claim expiry for failed nodes:**
- Heartbeat failure detection calls `ReleaseClaimAsync(messageId)` for all messages claimed by the dead node
- Those messages become available for re-delivery (normal retry flow resumes)

### 7.3 FanOut Without ACK

```
Node X (publisher):
    1. Save message to shared storage
    2. Get cluster_subscriptions for this queue → list of (nodeId, subscriberCount)
    3. Deliver to own local subscribers (local ConnectionManager)
    4. For each peer node with subscribers → send DeliverMessage{messageId, payload}

Peer nodes:
    1. Receive DeliverMessage
    2. Deliver to all local subscribers for this queue
    3. No ACK coordination needed

Cleanup:
    Node X deletes message from storage after own local delivery
    (FanOut without ACK = fire and forget anyway)
```

### 7.4 FanOut With ACK

```
Node X (publisher):
    1. Save message to shared storage
    2. Get total expected ACK count = sum of subscriber_count across all nodes for this queue
    3. Call InitFanOutTrackingAsync(messageId, totalAcks) in coordinator
    4. Deliver to own local subscribers, track in local AckTracker
    5. Send DeliverMessage to peer nodes

Peer nodes:
    1. Receive DeliverMessage, deliver to local subscribers, track in local AckTracker
    2. When subscriber sends ACK:
       a. Remove from local AckTracker
       b. Call RecordFanOutAckAsync(messageId) on coordinator
       c. If remaining == 0 → broadcast ClusterMessageType.AckForward{messageId, allComplete=true}

Node X on receiving AckForward{allComplete=true}:
    1. Remove message from shared storage
    2. Clean up fanout_ack_counters entry
```

### 7.5 Subscription Changes

When a client subscribes on Node Y:
```
1. Existing SubscribeHandler runs (registers in local ConnectionManager)
2. ClusterCoordinator.IncrementSubscriberCountAsync(nodeY, queueName)
3. IClusterTransport.BroadcastAsync(SubscriptionAdded{nodeY, queueName, newCount})
```

Peers receive the event and update their in-memory cache of cluster subscriptions.

This cache avoids hitting the DB on every publish to determine which peers to notify.

---

## 8. Cross-Node Permission Cache Invalidation

When an admin operation changes user permissions (GrantPermission, RevokePermission, DeleteUser, ChangePassword):
```
1. Handler updates the auth database (existing behavior)
2. Broadcast PermissionInvalidated{username}
3. Each node receiving the event:
   a. Find all ClientConnections with that username
   b. Reload their permissions from the auth database
   c. Update CachedPermissions on the connection
```

This ensures that permission changes propagate cluster-wide within one heartbeat interval.

---

## 9. Client Connection Handling

Clients connect to any node (no sticky sessions required). When a client publishes or subscribes, the local node handles everything through the cluster layer transparently.

**Client Hints (optional, future):**
- On `ConnectAck`, include a list of known cluster node addresses
- Client can use this to reconnect to a different node if current node goes down
- Requires client-side change: `ClientOptions.ClusterNodes` list for initial bootstrap

**Reconnect behavior (existing):**
- Auto-reconnect with exponential backoff already implemented in `VibeMQClient`
- In clustered mode, the reconnect list should include all cluster node addresses
- Add `ClientOptions.AdditionalHosts` list: client tries each host on reconnect

---

## 10. Failure Scenarios

### Node crash (no graceful shutdown)
1. Peers detect via TCP disconnect (immediate) AND heartbeat timeout in DB
2. First node to confirm dead state calls `ReleaseClaimAsync` for all active RoundRobin claims by dead node
3. FanOut ACK counters: subtract dead node's subscriber count from remaining expected ACKs
4. Messages become re-deliverable via normal retry flow

### Network partition (split-brain)
- Two groups of nodes can't reach each other
- Both sides may try to deliver the same RoundRobin message
- The DB claim acts as the tie-breaker: only one partition can write `message_claims`
- If the DB is in the unreachable partition → nodes fall back to local-only delivery (degraded mode)
- **Degraded mode**: node operates independently, logs a warning, messages are eventually consistent

### Shared storage failure
- Nodes keep serving from in-memory state
- Publishes fail with a configurable strategy: `Block`, `LocalOnly`, or `RejectWithError`
- Add `ClusterOptions.StorageFailureStrategy`

### New node joining mid-operation
- New node registers, peers connect
- New node starts receiving cluster events immediately
- In-flight messages on other nodes are not affected (claims and FanOut counters are already in storage)
- New node starts receiving new subscriptions from its clients right away

---

## 11. New Packages

| Package | Description |
|---------|-------------|
| `VibeMQ.Server.Clustering` | Core clustering: `IClusterCoordinator`, `IClusterTransport`, TCP mesh, ClusterManager |
| `VibeMQ.Server.Clustering.Postgres` | PostgreSQL `IClusterCoordinator` using LISTEN/NOTIFY for instant event propagation |
| `VibeMQ.Server.Clustering.Redis` | Redis `IClusterCoordinator` using pub/sub (separate NuGet, optional dep) |

`VibeMQ.Server.Clustering` depends on:
- `VibeMQ.Server` (existing)
- A pluggable `IClusterCoordinator` implementation (PostgreSQL by default for multi-machine)

For single-machine multi-process clustering (dev/test), a `SqliteClusterCoordinator` can be provided in `VibeMQ.Server.Clustering` itself using SQLite polling.

---

## 12. BrokerBuilder API

```csharp
// Minimal cluster setup
BrokerBuilder.Create()
    .UsePort(2925)
    .UseStorageProvider(/* shared PostgreSQL */)
    .UseClustering(cluster => {
        cluster.NodeId = "node-1";              // stable identity
        cluster.ClusterHost = "10.0.0.1";       // this node's LAN address
        cluster.ClusterPort = 12925;           // cluster mesh port
        cluster.HeartbeatInterval = TimeSpan.FromSeconds(5);
        cluster.NodeTimeout = TimeSpan.FromSeconds(15);
    })
    .Build();

// DI variant (VibeMQ.Server.DI)
services.AddVibeMQBroker(options => {
    options.Port = 2925;
})
.AddPostgresStorage("Host=db;Database=vibemq")
.AddClustering(cluster => {
    cluster.NodeId = Environment.GetEnvironmentVariable("NODE_ID");
    cluster.ClusterHost = Environment.GetEnvironmentVariable("POD_IP");
});
```

---

## 13. In-Memory Subscription Cache

Each node maintains a local in-memory cache of cluster subscriptions to avoid hitting the DB on every publish:

```csharp
// In ClusterManager (new internal class)
private ConcurrentDictionary<string, List<NodeSubscriberEntry>> _clusterSubscriptions;
// nodeId → [(queueName, count)]

// Refreshed by:
// 1. SubscriptionAdded/Removed events from peers
// 2. Periodic full sync from DB (every 30s, as reconciliation)
```

This cache is the authoritative source for "which peer nodes have subscribers for queue X" when deciding who to forward DeliverMessage to.

---

## 14. Integration with Existing Components

### QueueManager changes
- `PublishAsync`: after local enqueue, call `ClusterManager.OnMessagePublished(message, queue)`
- `DeliverAsync`: for RoundRobin, call `ClusterCoordinator.TryClaimMessage` before delivering
- `AcknowledgeAsync`: for FanOut, call `ClusterCoordinator.RecordFanOutAck`

### ConnectionManager changes
- On subscribe: notify `ClusterManager.OnSubscriptionAdded(queueName)`
- On unsubscribe/disconnect: notify `ClusterManager.OnSubscriptionRemoved(queueName)`

### BrokerServer changes
- Initialize `IClusterTransport` and `IClusterCoordinator` during startup
- Pass `ClusterManager` to `QueueManager` and `ConnectionManager`
- Graceful shutdown: call `ClusterManager.StopAsync` before `QueueManager` shutdown

### CommandDispatcher changes
- Register cluster-internal message handlers (Hello, Heartbeat, DeliverMessage, etc.)
- These handlers run on cluster port connections, not client connections

### New: `ClusterManager` (internal orchestrator)
- Owns `IClusterTransport` + `IClusterCoordinator`
- Processes incoming cluster envelopes (routes to correct handler)
- Exposes events that `QueueManager` and `ConnectionManager` subscribe to
- Handles peer connect/disconnect events

---

## 15. Implementation Phases

### Phase 1 — Foundation (Week 1-2)
- [ ] Define `ClusterOptions`, `NodeInfo`, `ClusterEnvelope`, `ClusterMessageType`
- [ ] Define `IClusterCoordinator` and `IClusterTransport` interfaces
- [ ] `NullClusterCoordinator` (no-op, for single-node mode — zero overhead)
- [ ] TCP mesh: `TcpClusterTransport` — listener, peer connections, framed protocol
- [ ] Hello/Heartbeat/Bye cluster messages
- [ ] `BrokerBuilder.UseClustering(...)` API wiring

### Phase 2 — Node Registry & Subscription Tracking (Week 2-3)
- [ ] PostgreSQL `ClusterCoordinator` — schema, migrations
- [ ] Node registration + heartbeat loop
- [ ] Failure detection loop
- [ ] Cluster subscription table (`IncrementSubscriberCountAsync`, etc.)
- [ ] In-memory subscription cache in `ClusterManager`
- [ ] SubscriptionAdded/Removed events forwarded over mesh

### Phase 3 — Clustered Delivery (Week 3-5)
- [ ] RoundRobin: `TryClaimMessage` + delivery coordination
- [ ] FanOut without ACK: `DeliverMessage` forwarding to peers
- [ ] FanOut with ACK: `InitFanOutTracking` + `RecordFanOutAck` + counter table
- [ ] Cross-node ACK forwarding
- [ ] Claim release on node failure (reclaim dead node's messages)

### Phase 4 — Resilience (Week 5-6)
- [ ] Degraded mode when DB is unreachable (local-only, warn)
- [ ] Re-delivery of messages after node failure
- [ ] Split-brain detection (log warning, don't attempt auto-merge)
- [ ] Permission cache invalidation across nodes

### Phase 5 — Client Improvements (Week 6-7)
- [ ] `ClientOptions.AdditionalHosts` — list of cluster node addresses for reconnect
- [ ] `ConnectAck` includes cluster node hints (optional, behind a flag)
- [ ] VibeMQ.Client.DI: register multiple host addresses

### Phase 6 — Storage Backends & Polish (Week 7-8)
- [ ] `SqliteClusterCoordinator` (polling-based, single-machine multi-process)
- [ ] Redis `IClusterCoordinator` in `VibeMQ.Server.Clustering.Redis`
- [ ] Integration tests: 3-node cluster, publish from one, subscribe from another
- [ ] Documentation: clustering guide, deployment examples (Docker Compose, K8s)
- [ ] Russian translation for clustering docs

---

## 16. Open Questions

1. **NodeId persistence**: Should NodeId be auto-generated on each start (ephemeral) or stored to disk (stable)? Stable NodeId makes it easier to track subscriptions and reclaim messages after restarts. Recommend: store in a small `node.id` file next to the SQLite DB or as a config value.

2. **Queue creation in cluster**: What if two nodes receive `CreateQueue` for the same queue simultaneously? Need idempotent `INSERT OR IGNORE` behavior in storage — already mostly handled by `QueueManager`, but needs verification.

3. **PriorityBased delivery mode in cluster**: Requires cluster-wide sorting, which means fetching all messages to compare priorities. This is expensive. Possible approach: node that claims a message must check if a higher-priority message exists on other nodes. For Phase 1, document PriorityBased as "cluster-unaware" (per-node priority only). Full cluster-aware priority is deferred.

4. **Admin operations in cluster**: Creating/deleting users and queues must be replicated. Queue operations are already storage-backed (any node re-reads from storage). User operations need PermissionInvalidated broadcast.

5. **Max cluster size**: The TCP mesh is O(n²) connections. For >10 nodes, consider a hub-and-spoke topology (elected hub) or move to gossip. For the initial implementation, document a recommended maximum of 5-7 nodes.

6. **Compression negotiation in cluster**: Cluster-to-cluster traffic can use a fixed compression (e.g., always Brotli) since both sides are VibeMQ nodes. No need for negotiation.

7. **Metrics**: Add cluster metrics — peer connection count, cross-node deliveries/sec, claim contention rate, cluster event lag.
