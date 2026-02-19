using System.Text.Json.Serialization;

namespace VibeMQ.Protocol;

/// <summary>
/// Source-generated JSON serialization context for the VibeMQ protocol.
/// Eliminates reflection-based serialization overhead for hot paths.
/// </summary>
[JsonSerializable(typeof(ProtocolMessage))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false
)]
public partial class ProtocolJsonContext : JsonSerializerContext;
