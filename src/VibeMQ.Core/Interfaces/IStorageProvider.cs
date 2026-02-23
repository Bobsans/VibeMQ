using VibeMQ.Configuration;
using VibeMQ.Models;

namespace VibeMQ.Interfaces;

/// <summary>
/// Unified storage abstraction for messages, queues, and dead-lettered messages.
/// <para>
/// Implementations MUST follow these contracts:
/// <list type="bullet">
///   <item><see cref="InitializeAsync"/> MUST be called before any other operations.</item>
///   <item><see cref="RemoveQueueAsync"/> MUST cascade-delete all messages belonging to the queue.</item>
///   <item>All methods MUST be thread-safe for concurrent access.</item>
/// </list>
/// </para>
/// </summary>
public interface IStorageProvider : IAsyncDisposable {
    // --- Lifecycle ---

    /// <summary>
    /// Initializes the storage backend (creates schema, opens connections, etc.).
    /// MUST be called before any other operations.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <c>true</c> if the storage is available and ready for operations.
    /// Embedded providers (InMemory, SQLite) always return <c>true</c>.
    /// Network providers (PostgreSQL, Redis) should check connectivity.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    // --- Messages ---

    /// <summary>
    /// Persists a single message to storage.
    /// </summary>
    Task SaveMessageAsync(BrokerMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists multiple messages in a single operation.
    /// Providers SHOULD override for batch optimization (e.g. transactions, pipelining).
    /// Default implementation calls <see cref="SaveMessageAsync"/> for each message sequentially.
    /// </summary>
    async Task SaveMessagesAsync(IReadOnlyList<BrokerMessage> messages, CancellationToken cancellationToken = default) {
        foreach (var message in messages) {
            await SaveMessageAsync(message, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Retrieves a message by its unique identifier.
    /// </summary>
    Task<BrokerMessage?> GetMessageAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a message from storage. Returns <c>true</c> if the message was found and removed.
    /// </summary>
    Task<bool> RemoveMessageAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all pending (undelivered) messages for a given queue, ordered by timestamp ascending.
    /// </summary>
    Task<IReadOnlyList<BrokerMessage>> GetPendingMessagesAsync(string queueName, CancellationToken cancellationToken = default);

    // --- Queues ---

    /// <summary>
    /// Persists queue metadata (name and options). If the queue already exists, its options are updated.
    /// </summary>
    Task SaveQueueAsync(string name, QueueOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a queue and ALL its associated messages (cascade delete).
    /// </summary>
    Task RemoveQueueAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all stored queues with their options and creation timestamps.
    /// </summary>
    Task<IReadOnlyList<StoredQueue>> GetAllQueuesAsync(CancellationToken cancellationToken = default);

    // --- Dead Letter Queue ---

    /// <summary>
    /// Persists a dead-lettered message to storage.
    /// </summary>
    Task SaveDeadLetteredMessageAsync(DeadLetteredMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns up to <paramref name="count"/> dead-lettered messages, ordered by failure time ascending.
    /// </summary>
    Task<IReadOnlyList<DeadLetteredMessage>> GetDeadLetteredMessagesAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a dead-lettered message by its original message ID.
    /// Returns <c>true</c> if the message was found and removed.
    /// </summary>
    Task<bool> RemoveDeadLetteredMessageAsync(string messageId, CancellationToken cancellationToken = default);
}
