using System.Text.Json;
using VibeMQ.Protocol;

namespace VibeMQ.Tests.Unit.Protocol;

public class ProtocolSerializationTests {
    [Fact]
    public void Serialize_BasicMessage_RoundTripsCorrectly() {
        var original = new ProtocolMessage {
            Type = CommandType.Publish,
            Queue = "test-queue",
            Headers = new Dictionary<string, string> { ["key"] = "value" },
        };

        var json = JsonSerializer.Serialize(original, ProtocolSerializer.Options);
        var deserialized = JsonSerializer.Deserialize<ProtocolMessage>(json, ProtocolSerializer.Options)!;

        Assert.Equal(original.Id, deserialized.Id);
        Assert.Equal(CommandType.Publish, deserialized.Type);
        Assert.Equal("test-queue", deserialized.Queue);
        Assert.Equal("value", deserialized.Headers!["key"]);
    }

    [Fact]
    public void Serialize_CommandType_SerializesAsString() {
        var message = new ProtocolMessage { Type = CommandType.Connect };
        var json = JsonSerializer.Serialize(message, ProtocolSerializer.Options);

        // The [JsonConverter(typeof(JsonStringEnumConverter))] on the property
        // serializes as PascalCase string (attribute takes priority over options)
        Assert.Contains("\"Connect\"", json);
    }

    [Fact]
    public void Serialize_NullPayload_OmittedInJson() {
        var message = new ProtocolMessage { Type = CommandType.Ping, Payload = null };
        var json = JsonSerializer.Serialize(message, ProtocolSerializer.Options);

        Assert.DoesNotContain("payload", json);
    }

    [Fact]
    public void Serialize_WithPayload_PreservesJsonElement() {
        var payloadJson = JsonSerializer.SerializeToElement(new { Name = "test", Value = 42 });
        var message = new ProtocolMessage {
            Type = CommandType.Publish,
            Queue = "q",
            Payload = payloadJson,
        };

        var json = JsonSerializer.Serialize(message, ProtocolSerializer.Options);
        var deserialized = JsonSerializer.Deserialize<ProtocolMessage>(json, ProtocolSerializer.Options)!;

        Assert.NotNull(deserialized.Payload);
        Assert.Equal("test", deserialized.Payload.Value.GetProperty("Name").GetString());
        Assert.Equal(42, deserialized.Payload.Value.GetProperty("Value").GetInt32());
    }

    [Fact]
    public void Serialize_ErrorMessage_IncludesErrorFields() {
        var message = new ProtocolMessage {
            Type = CommandType.Error,
            ErrorCode = "AUTH_FAILED",
            ErrorMessage = "Invalid token",
        };

        var json = JsonSerializer.Serialize(message, ProtocolSerializer.Options);

        Assert.Contains("AUTH_FAILED", json);
        Assert.Contains("Invalid token", json);
    }

    [Fact]
    public void Serialize_Version_DefaultsTo1() {
        var message = new ProtocolMessage { Type = CommandType.Ping };

        Assert.Equal(1, message.Version);
    }
}
