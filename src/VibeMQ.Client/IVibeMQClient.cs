using VibeMQ.Configuration;
using VibeMQ.Interfaces;
using VibeMQ.Models;

namespace VibeMQ.Client;

/// <summary>
/// Abstraction for a VibeMQ client that can publish and subscribe.
/// Implemented by <see cref="VibeMQClient"/> and by the DI-managed client when using AddVibeMQClient (VibeMQ.Client.DependencyInjection).
/// </summary>
public interface IVibeMQClient {
    /// <summary>
    /// Publishes a message to the specified queue.
    /// </summary>
    /// <param name="queueName">Target queue name.</param>
    /// <param name="payload">Message payload (serialized as JSON).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync<T>(string queueName, T payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a message to the specified queue with custom headers.
    /// </summary>
    /// <param name="queueName">Target queue name.</param>
    /// <param name="payload">Message payload (serialized as JSON).</param>
    /// <param name="headers">Custom headers (e.g., correlationId, priority). Priority can be set via "priority" header with values: "Low", "Normal", "High", "Critical".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync<T>(string queueName, T payload, Dictionary<string, string>? headers, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to a queue and invokes the handler for each received message.
    /// Returns a disposable that unsubscribes when disposed.
    /// </summary>
    /// <param name="queueName">Queue to subscribe to.</param>
    /// <param name="handler">Handler invoked for each message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="IAsyncDisposable"/> that unsubscribes when disposed.</returns>
    Task<IAsyncDisposable> SubscribeAsync<T>(string queueName, Func<T, Task> handler, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to a queue using a class-based message handler.
    /// Returns a disposable that unsubscribes when disposed.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="THandler">The handler type implementing <see cref="IMessageHandler{TMessage}"/>.</typeparam>
    /// <param name="queueName">Queue to subscribe to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="IAsyncDisposable"/> that unsubscribes when disposed.</returns>
    Task<IAsyncDisposable> SubscribeAsync<TMessage, THandler>(string queueName, CancellationToken cancellationToken = default)
        where THandler : IMessageHandler<TMessage>;

    /// <summary>
    /// Creates a queue with the specified name and options.
    /// </summary>
    /// <param name="queueName">Name of the queue to create.</param>
    /// <param name="options">Optional queue configuration options. If null, default options are used.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CreateQueueAsync(string queueName, QueueOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a queue by name.
    /// </summary>
    /// <param name="queueName">Name of the queue to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteQueueAsync(string queueName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns metadata about a specific queue, or null if the queue does not exist.
    /// </summary>
    /// <param name="queueName">Name of the queue.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<QueueInfo?> GetQueueInfoAsync(string queueName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all queue names on the broker.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<string>> ListQueuesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the client is currently connected to the broker.
    /// </summary>
    bool IsConnected { get; }
}
