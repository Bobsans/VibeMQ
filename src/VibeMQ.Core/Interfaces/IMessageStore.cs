using VibeMQ.Models;

namespace VibeMQ.Interfaces;

/// <summary>
/// Storage abstraction for broker messages (in-memory implementation for now).
/// </summary>
public interface IMessageStore {
    /// <summary>
    /// Adds a message to the store and returns its ID.
    /// </summary>
    Task<string> AddAsync(BrokerMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a message by its ID.
    /// </summary>
    Task<BrokerMessage?> GetAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a message from the store. Returns true if the message was found and removed.
    /// </summary>
    Task<bool> RemoveAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all pending (undelivered) messages for a given queue.
    /// </summary>
    Task<IReadOnlyList<BrokerMessage>> GetPendingAsync(string queueName, CancellationToken cancellationToken = default);
}
