using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace VibeMQ.Protocol;

/// <summary>
/// Shared JSON serializer options for the VibeMQ protocol.
/// </summary>
public static class ProtocolSerializer {
    /// <summary>
    /// Default serializer options used across the protocol layer.
    /// Uses the source-generated <see cref="ProtocolJsonContext"/> for protocol types (fast path),
    /// with a reflection-based fallback for user payload types.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        TypeInfoResolverChain = {
            ProtocolJsonContext.Default,
            new DefaultJsonTypeInfoResolver(), // Fallback for user-defined payload types
        },
        Converters = {
            new JsonStringEnumConverter<CommandType>(JsonNamingPolicy.CamelCase),
        },
    };
}
