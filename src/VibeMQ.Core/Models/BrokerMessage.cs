using System.Text.Json;
using VibeMQ.Core.Enums;

namespace VibeMQ.Core.Models;

/// <summary>
/// Represents a message in the broker system.
/// On the transport level, <see cref="Payload"/> is a raw <see cref="JsonElement"/>.
/// Typed deserialization is performed on the consumer side.
/// </summary>
public sealed class BrokerMessage {
    /// <summary>
    /// Unique message identifier.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Name of the target queue.
    /// </summary>
    public required string QueueName { get; set; }

    /// <summary>
    /// Message payload as a raw JSON element.
    /// </summary>
    public JsonElement Payload { get; set; }

    /// <summary>
    /// UTC timestamp when the message was created.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Custom headers attached to the message (e.g. correlationId).
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new();

    /// <summary>
    /// Protocol version for backward compatibility.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Message priority level.
    /// </summary>
    public MessagePriority Priority { get; set; } = MessagePriority.Normal;

    /// <summary>
    /// Number of delivery attempts made for this message.
    /// </summary>
    public int DeliveryAttempts { get; set; }
}
