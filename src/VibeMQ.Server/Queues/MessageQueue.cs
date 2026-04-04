using System.Collections.Concurrent;
using VibeMQ.Configuration;
using VibeMQ.Enums;
using VibeMQ.Models;

namespace VibeMQ.Server.Queues;

/// <summary>
/// In-memory message queue with support for different delivery modes and overflow strategies.
/// </summary>
/// <remarks>
/// Creates a queue with an explicit creation timestamp (used during recovery from storage).
/// </remarks>
public sealed class MessageQueue(string name, QueueOptions options, DateTime createdAt) {
    private readonly ConcurrentQueue<BrokerMessage> _messages = new();
    private readonly ConcurrentDictionary<string, BrokerMessage> _unacknowledged = new();
#if NET10_0_OR_GREATER
    private readonly Lock _sync = new();
#else
    private readonly object _sync = new();
#endif
    private int _roundRobinIndex;

    public MessageQueue(string name, QueueOptions options) : this(name, options, DateTime.UtcNow) { }

    /// <summary>
    /// Queue name.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Configuration options for this queue.
    /// </summary>
    public QueueOptions Options { get; } = options;

    /// <summary>
    /// UTC timestamp when the queue was created.
    /// </summary>
    public DateTime CreatedAt { get; } = createdAt;

    /// <summary>
    /// Current number of messages waiting in the queue.
    /// </summary>
    public int MessageCount => _messages.Count;

    /// <summary>
    /// Number of messages waiting for acknowledgment.
    /// </summary>
    public int UnacknowledgedCount => _unacknowledged.Count;

    /// <summary>
    /// Enqueues a message, applying the overflow strategy if the queue is full.
    /// Returns true if the message was accepted.
    /// </summary>
    public bool Enqueue(BrokerMessage message) {
        lock (_sync) {
            if (_messages.Count >= Options.MaxQueueSize) {
                return ApplyOverflowStrategy(message);
            }

            _messages.Enqueue(message);
            return true;
        }
    }

    /// <summary>
    /// Tries to dequeue the next message for delivery.
    /// For PriorityBased mode, returns the highest priority message.
    /// </summary>
    public BrokerMessage? Dequeue() {
        lock (_sync) {
            if (Options.Mode == DeliveryMode.PriorityBased) {
                return DequeuePriority();
            }

            return _messages.TryDequeue(out var message) ? message : null;
        }
    }

    /// <summary>
    /// Peeks at all messages without removing them (for fan-out modes).
    /// </summary>
    public IReadOnlyList<BrokerMessage> PeekAll() {
        lock (_sync) {
            return _messages.ToArray();
        }
    }

    /// <summary>
    /// Removes a single message by id from the pending queue (not from unacknowledged).
    /// Returns true if the message was found and removed.
    /// </summary>
    public bool RemoveMessageById(string messageId) {
        lock (_sync) {
            var list = new List<BrokerMessage>();
            while (_messages.TryDequeue(out var m)) {
                list.Add(m);
            }

            var removed = list.RemoveAll(m => m.Id == messageId) > 0;
            foreach (var m in list) {
                _messages.Enqueue(m);
            }

            return removed;
        }
    }

    /// <summary>
    /// Clears all pending messages from the queue (used for purge). Does not touch unacknowledged.
    /// </summary>
    public void ClearPending() {
        lock (_sync) {
            while (_messages.TryDequeue(out _)) {
                // drain
            }
        }
    }

    /// <summary>
    /// Atomically drains all pending messages, returning them and clearing the queue in one operation.
    /// </summary>
    public IReadOnlyList<BrokerMessage> DrainPending() {
        lock (_sync) {
            var drained = new List<BrokerMessage>(_messages.Count);
            while (_messages.TryDequeue(out var msg)) {
                drained.Add(msg);
            }

            return drained;
        }
    }

    /// <summary>
    /// Tracks a message as delivered but awaiting acknowledgment.
    /// </summary>
    public void TrackUnacknowledged(BrokerMessage message) {
        _unacknowledged.TryAdd(message.Id, message);
    }

    /// <summary>
    /// Acknowledges a message, removing it from the unacknowledged set.
    /// Returns true if the message was found.
    /// </summary>
    public bool Acknowledge(string messageId) {
        return _unacknowledged.TryRemove(messageId, out _);
    }

    /// <summary>
    /// Returns the next round-robin index and advances the counter.
    /// </summary>
    public int GetNextRoundRobinIndex(int subscriberCount) {
        if (subscriberCount <= 0) {
            return 0;
        }

        var index = Interlocked.Increment(ref _roundRobinIndex);
        return (index & 0x7FFFFFFF) % subscriberCount;
    }

    /// <summary>
    /// Returns all unacknowledged messages (for retry logic).
    /// </summary>
    public IReadOnlyList<BrokerMessage> GetUnacknowledged() {
        return _unacknowledged.Values.ToArray();
    }

    /// <summary>
    /// Creates a read-only snapshot of queue state.
    /// </summary>
    public QueueInfo ToQueueInfo(int subscriberCount) {
        return new QueueInfo {
            Name = Name,
            MessageCount = MessageCount,
            SubscriberCount = subscriberCount,
            DeliveryMode = Options.Mode,
            MaxSize = Options.MaxQueueSize,
            CreatedAt = CreatedAt,
            MessageTtl = Options.MessageTtl,
            EnableDeadLetterQueue = Options.EnableDeadLetterQueue,
            DeadLetterQueueName = Options.DeadLetterQueueName,
            OverflowStrategy = Options.OverflowStrategy,
            MaxRetryAttempts = Options.MaxRetryAttempts
        };
    }

    private BrokerMessage? DequeuePriority() {
        // Snapshot the queue, find highest priority, re-enqueue the rest
        var snapshot = new List<BrokerMessage>();

        while (_messages.TryDequeue(out var msg)) {
            snapshot.Add(msg);
        }

        if (snapshot.Count == 0) {
            return null;
        }

        snapshot.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        var result = snapshot[0];

        for (var i = 1; i < snapshot.Count; i++) {
            _messages.Enqueue(snapshot[i]);
        }

        return result;
    }

    private bool ApplyOverflowStrategy(BrokerMessage message) {
        return Options.OverflowStrategy switch {
            OverflowStrategy.DropOldest => DropOldestAndEnqueue(),
            OverflowStrategy.DropNewest => false,
            OverflowStrategy.BlockPublisher => false, // In an async context, the caller should handle backpressure
            OverflowStrategy.RedirectToDlq => false, // Caller (QueueManager) handles DLQ redirect
            _ => false
        };

        // Called from Enqueue, already inside _sync
        bool DropOldestAndEnqueue() {
            _messages.TryDequeue(out _);
            _messages.Enqueue(message);
            return true;
        }
    }
}
