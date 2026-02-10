using System.Text.Json;
using BenchmarkDotNet.Attributes;
using VibeMQ.Protocol;

namespace VibeMQ.Benchmarks;

/// <summary>
/// Benchmarks for JSON serialization/deserialization of protocol messages.
/// Compares reflection vs source-generated serialization.
/// </summary>
[MemoryDiagnoser]
public class SerializationBenchmarks {
    private ProtocolMessage _message = null!;
    private byte[] _serializedBytes = null!;

    private static readonly JsonSerializerOptions _reflectionOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter<CommandType>(JsonNamingPolicy.CamelCase) },
    };

    [GlobalSetup]
    public void Setup() {
        _message = new ProtocolMessage {
            Type = CommandType.Publish,
            Queue = "benchmark-queue",
            Payload = JsonSerializer.SerializeToElement(new { Name = "test", Value = 42 }),
            Headers = new Dictionary<string, string> {
                ["correlationId"] = "abc-123",
                ["priority"] = "high",
            },
        };

        _serializedBytes = JsonSerializer.SerializeToUtf8Bytes(_message, ProtocolSerializer.Options);
    }

    [Benchmark(Baseline = true)]
    public byte[] Serialize_Reflection() {
        return JsonSerializer.SerializeToUtf8Bytes(_message, _reflectionOptions);
    }

    [Benchmark]
    public byte[] Serialize_SourceGen() {
        return JsonSerializer.SerializeToUtf8Bytes(_message, ProtocolSerializer.Options);
    }

    [Benchmark]
    public ProtocolMessage? Deserialize_Reflection() {
        return JsonSerializer.Deserialize<ProtocolMessage>(_serializedBytes, _reflectionOptions);
    }

    [Benchmark]
    public ProtocolMessage? Deserialize_SourceGen() {
        return JsonSerializer.Deserialize<ProtocolMessage>(_serializedBytes, ProtocolSerializer.Options);
    }
}
