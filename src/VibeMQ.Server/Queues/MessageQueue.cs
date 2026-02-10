using System.Collections.Concurrent;
using VibeMQ.Core.Configuration;
using VibeMQ.Core.Enums;
using VibeMQ.Core.Models;

namespace VibeMQ.Server.Queues;

/// <summary>
/// In-memory message queue with support for different delivery modes and overflow strategies.
/// </summary>
public sealed class MessageQueue {
    private readonly ConcurrentQueue<BrokerMessage> _messages = new();
    private readonly ConcurrentDictionary<string, BrokerMessage> _unacknowledged = new();
    private int _roundRobinIndex;

    public MessageQueue(string name, QueueOptions options) {
        Name = name;
        Options = options;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Queue name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Configuration options for this queue.
    /// </summary>
    public QueueOptions Options { get; }

    /// <summary>
    /// UTC timestamp when the queue was created.
    /// </summary>
    public DateTime CreatedAt { get; }

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
        if (_messages.Count >= Options.MaxQueueSize) {
            return ApplyOverflowStrategy(message);
        }

        _messages.Enqueue(message);
        return true;
    }

    /// <summary>
    /// Tries to dequeue the next message for delivery.
    /// For PriorityBased mode, returns the highest priority message.
    /// </summary>
    public BrokerMessage? Dequeue() {
        if (Options.Mode == DeliveryMode.PriorityBased) {
            return DequeuePriority();
        }

        return _messages.TryDequeue(out var message) ? message : null;
    }

    /// <summary>
    /// Peeks at all messages without removing them (for fan-out modes).
    /// </summary>
    public IReadOnlyList<BrokerMessage> PeekAll() {
        return _messages.ToArray();
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
        return Math.Abs(index) % subscriberCount;
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
        switch (Options.OverflowStrategy) {
            case OverflowStrategy.DropOldest:
                _messages.TryDequeue(out _);
                _messages.Enqueue(message);
                return true;

            case OverflowStrategy.DropNewest:
                return false;

            case OverflowStrategy.BlockPublisher:
                // In async context, caller should handle backpressure
                return false;

            case OverflowStrategy.RedirectToDlq:
                // Caller (QueueManager) handles DLQ redirect
                return false;

            default:
                return false;
        }
    }
}
