using System.Text.Json;
using System.Text.Json.Serialization;

namespace VibeMQ.Protocol;

/// <summary>
/// Shared JSON serializer options for the VibeMQ protocol.
/// </summary>
public static class ProtocolSerializer {
    /// <summary>
    /// Default serializer options used across the protocol layer.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Converters = {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
        },
    };
}
