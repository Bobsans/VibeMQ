using VibeMQ.Models;

namespace VibeMQ.Interfaces;

/// <summary>
/// Optional storage extension for persisting delivery in-flight state.
/// </summary>
public interface IInFlightMessageStorage {
    /// <summary>
    /// Marks a message as delivered and waiting for ACK.
    /// </summary>
    Task MarkMessageInFlightAsync(
        BrokerMessage message,
        string clientId,
        int maxRetryAttempts,
        DateTime nextRetryAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates in-flight state after a retry schedule change.
    /// </summary>
    Task UpdateInFlightRetryAsync(
        string messageId,
        int deliveryAttempts,
        DateTime nextRetryAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a message back to pending delivery state.
    /// </summary>
    Task RequeueInFlightMessageAsync(
        string messageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes in-flight state when delivery completes or expires.
    /// </summary>
    Task RemoveInFlightStateAsync(
        string messageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Recovers in-flight messages and returns them for re-enqueue.
    /// Implementations should clear the recovered in-flight state.
    /// </summary>
    Task<IReadOnlyList<BrokerMessage>> RecoverInFlightMessagesAsync(
        CancellationToken cancellationToken = default);
}
