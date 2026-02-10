using VibeMQ.Core.Models;

namespace VibeMQ.Server.Delivery;

/// <summary>
/// Tracks a message that has been delivered but not yet acknowledged.
/// </summary>
public sealed class PendingDelivery {
    public PendingDelivery(BrokerMessage message, string clientId) {
        Message = message;
        ClientId = clientId;
        DeliveredAt = DateTime.UtcNow;
        NextRetryAt = DateTime.UtcNow;
    }

    /// <summary>
    /// The delivered message.
    /// </summary>
    public BrokerMessage Message { get; }

    /// <summary>
    /// ID of the client this message was delivered to.
    /// </summary>
    public string ClientId { get; }

    /// <summary>
    /// UTC timestamp when the message was first delivered.
    /// </summary>
    public DateTime DeliveredAt { get; }

    /// <summary>
    /// Number of delivery attempts made so far.
    /// </summary>
    public int Attempts { get; set; } = 1;

    /// <summary>
    /// UTC timestamp when the next retry should be attempted.
    /// </summary>
    public DateTime NextRetryAt { get; set; }
}
