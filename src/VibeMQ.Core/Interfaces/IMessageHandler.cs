namespace VibeMQ.Interfaces;

/// <summary>
/// Handler for processing messages of type T.
/// Implement this interface to create class-based message handlers for VibeMQ client subscriptions.
/// </summary>
/// <typeparam name="T">The message type to handle.</typeparam>
/// <example>
/// <code>
/// public class OrderHandler : IMessageHandler&lt;OrderCreated&gt; {
///     public async Task HandleAsync(OrderCreated message, CancellationToken cancellationToken) {
///         // Process the order
///     }
/// }
/// </code>
/// </example>
public interface IMessageHandler<in T> {
    /// <summary>
    /// Handles a message asynchronously.
    /// </summary>
    /// <param name="message">The message to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleAsync(T message, CancellationToken cancellationToken);
}
