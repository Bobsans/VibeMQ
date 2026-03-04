using VibeMQ.Models;

namespace VibeMQ.Server.Delivery;

/// <summary>
/// Tracks a message that has been delivered but not yet acknowledged.
/// </summary>
public sealed class PendingDelivery(BrokerMessage message, string clientId) {
    /// <summary>
    /// The delivered message.
    /// </summary>
    public BrokerMessage Message { get; } = message;

    /// <summary>
    /// ID of the client this message was delivered to.
    /// </summary>
    public string ClientId { get; } = clientId;

    /// <summary>
    /// UTC timestamp when the message was first delivered.
    /// </summary>
    public DateTime DeliveredAt { get; } = DateTime.UtcNow;

    /// <summary>
    /// Number of delivery attempts made so far.
    /// </summary>
    public int Attempts { get; set; } = 1;

    /// <summary>
    /// Maximum number of retry attempts allowed before moving to DLQ.
    /// </summary>
    public int MaxRetryAttempts { get; init; } = 3;

    /// <summary>
    /// UTC timestamp when the next retry should be attempted.
    /// </summary>
    public DateTime NextRetryAt { get; set; } = DateTime.UtcNow;
}
