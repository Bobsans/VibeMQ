namespace VibeMQ.Attributes;

/// <summary>
/// Specifies the queue name for a message handler.
/// Used for automatic subscription when integrating with the VibeMQ client hosting pipeline.
/// </summary>
/// <example>
/// <code>
/// [Queue("orders")]
/// public class OrderHandler : IMessageHandler&lt;OrderCreated&gt; {
///     public async Task HandleAsync(OrderCreated message, CancellationToken cancellationToken) {
///         // Process the order
///     }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class QueueAttribute : Attribute {
    /// <summary>
    /// Gets the queue name.
    /// </summary>
    public string QueueName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueueAttribute"/> class.
    /// </summary>
    /// <param name="queueName">The queue name to subscribe to.</param>
    public QueueAttribute(string queueName) {
        if (string.IsNullOrWhiteSpace(queueName)) {
            throw new ArgumentException("Queue name cannot be null or whitespace.", nameof(queueName));
        }
        QueueName = queueName;
    }
}
