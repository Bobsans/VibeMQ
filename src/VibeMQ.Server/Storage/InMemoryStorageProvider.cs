using System.Collections.Concurrent;
using VibeMQ.Configuration;
using VibeMQ.Interfaces;
using VibeMQ.Models;

namespace VibeMQ.Server.Storage;

/// <summary>
/// In-memory implementation of <see cref="IStorageProvider"/>.
/// Fast but not durable — all data is lost on restart.
/// Used as the default storage when no persistence is configured.
/// </summary>
public sealed class InMemoryStorageProvider : IStorageProvider {
    private readonly ConcurrentDictionary<string, BrokerMessage> _messages = new();
    private readonly ConcurrentDictionary<string, StoredQueue> _queues = new();
    private readonly ConcurrentQueue<DeadLetteredMessage> _deadLetters = new();

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken cancellationToken = default) {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) {
        return Task.FromResult(true);
    }

    // --- Messages ---

    /// <inheritdoc />
    public Task SaveMessageAsync(BrokerMessage message, CancellationToken cancellationToken = default) {
        _messages[message.Id] = message;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SaveMessagesAsync(IReadOnlyList<BrokerMessage> messages, CancellationToken cancellationToken = default) {
        foreach (var message in messages) {
            _messages[message.Id] = message;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<BrokerMessage?> GetMessageAsync(string id, CancellationToken cancellationToken = default) {
        _messages.TryGetValue(id, out var message);
        return Task.FromResult(message);
    }

    /// <inheritdoc />
    public Task<bool> RemoveMessageAsync(string id, CancellationToken cancellationToken = default) {
        return Task.FromResult(_messages.TryRemove(id, out _));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<BrokerMessage>> GetPendingMessagesAsync(string queueName, CancellationToken cancellationToken = default) {
        IReadOnlyList<BrokerMessage> pending = _messages.Values
            .Where(m => m.QueueName == queueName)
            .OrderBy(m => m.Timestamp)
            .ToArray();

        return Task.FromResult(pending);
    }

    // --- Queues ---

    /// <inheritdoc />
    public Task SaveQueueAsync(string name, QueueOptions queueOptions, CancellationToken cancellationToken = default) {
        _queues.AddOrUpdate(
            name,
            _ => new StoredQueue { Name = name, Options = queueOptions, CreatedAt = DateTime.UtcNow },
            (_, existing) => new StoredQueue { Name = name, Options = queueOptions, CreatedAt = existing.CreatedAt }
        );

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveQueueAsync(string name, CancellationToken cancellationToken = default) {
        _queues.TryRemove(name, out _);

        // Cascade delete: remove all messages belonging to this queue
        var messageIds = _messages
            .Where(kvp => kvp.Value.QueueName == name)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var id in messageIds) {
            _messages.TryRemove(id, out _);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<StoredQueue>> GetAllQueuesAsync(CancellationToken cancellationToken = default) {
        IReadOnlyList<StoredQueue> queues = _queues.Values.ToArray();
        return Task.FromResult(queues);
    }

    // --- Dead Letter Queue ---

    /// <inheritdoc />
    public Task SaveDeadLetteredMessageAsync(DeadLetteredMessage message, CancellationToken cancellationToken = default) {
        _deadLetters.Enqueue(message);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DeadLetteredMessage>> GetDeadLetteredMessagesAsync(int count, CancellationToken cancellationToken = default) {
        IReadOnlyList<DeadLetteredMessage> result = _deadLetters
            .Take(count)
            .ToArray();

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<bool> RemoveDeadLetteredMessageAsync(string messageId, CancellationToken cancellationToken = default) {
        // ConcurrentQueue does not support removal by key.
        // For in-memory provider this is acceptable — DLQ entries are rare
        // and typically consumed via DequeueAsync in the DeadLetterQueue class.
        // Persistent providers (SQLite, PostgreSQL) handle this with DELETE.
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() {
        _messages.Clear();
        _queues.Clear();

        #if NET10_0_OR_GREATER
        _deadLetters.Clear();
        #endif

        return ValueTask.CompletedTask;
    }
}
