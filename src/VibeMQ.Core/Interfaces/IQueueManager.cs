using VibeMQ.Configuration;
using VibeMQ.Models;

namespace VibeMQ.Interfaces;

/// <summary>
/// Manages queues, subscriptions, and message routing.
/// </summary>
public interface IQueueManager {
    /// <summary>
    /// Creates a new queue with the given name and options.
    /// </summary>
    Task CreateQueueAsync(string name, QueueOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a queue by name.
    /// </summary>
    Task DeleteQueueAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns metadata about a specific queue.
    /// </summary>
    Task<QueueInfo?> GetQueueInfoAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all known queue names.
    /// </summary>
    Task<IReadOnlyList<string>> ListQueuesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a message to the specified queue.
    /// </summary>
    Task PublishAsync(BrokerMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acknowledges successful processing of a message.
    /// </summary>
    Task<bool> AcknowledgeAsync(string messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Current number of active queues.
    /// </summary>
    int QueueCount { get; }
}
