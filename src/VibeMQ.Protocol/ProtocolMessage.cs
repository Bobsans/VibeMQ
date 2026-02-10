using System.Text.Json;
using System.Text.Json.Serialization;

namespace VibeMQ.Protocol;

/// <summary>
/// Wire-level message that wraps all commands in the VibeMQ protocol.
/// Serialized as JSON and framed with a 4-byte length prefix over TCP.
/// </summary>
public sealed class ProtocolMessage {
    /// <summary>
    /// Unique message identifier for correlation and deduplication.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Command type that determines how this message is processed.
    /// </summary>
    [JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter<CommandType>))]
    public required CommandType Type { get; set; }

    /// <summary>
    /// Target queue name (used by publish, subscribe, queue management commands).
    /// </summary>
    [JsonPropertyName("queue")]
    public string? Queue { get; set; }

    /// <summary>
    /// Message payload as a raw JSON element. Typed deserialization is on the consumer side.
    /// </summary>
    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; set; }

    /// <summary>
    /// Custom headers (correlationId, priority, etc.).
    /// </summary>
    [JsonPropertyName("headers")]
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// Protocol schema version for backward compatibility.
    /// </summary>
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; set; } = "1.0";

    /// <summary>
    /// Error code (only used with <see cref="CommandType.Error"/>).
    /// </summary>
    [JsonPropertyName("errorCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Human-readable error message (only used with <see cref="CommandType.Error"/>).
    /// </summary>
    [JsonPropertyName("errorMessage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorMessage { get; set; }
}
