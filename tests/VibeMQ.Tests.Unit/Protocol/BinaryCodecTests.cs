using System.Text.Json;
using VibeMQ.Protocol;
using VibeMQ.Protocol.Binary;

namespace VibeMQ.Tests.Unit.Protocol;

public class BinaryCodecTests {
    private static readonly VibeMQBinaryCodec Codec = new();

    [Fact]
    public void EncodeDecode_BasicMessage_RoundTripsCorrectly() {
        var original = new ProtocolMessage {
            Type = CommandType.Publish,
            Queue = "test-queue",
            Headers = new Dictionary<string, string> { ["key"] = "value" },
        };

        var encoded = Codec.Encode(original);
        var decoded = Codec.Decode(encoded);

        Assert.Equal(original.Id, decoded.Id);
        Assert.Equal(CommandType.Publish, decoded.Type);
        Assert.Equal("test-queue", decoded.Queue);
        Assert.NotNull(decoded.Headers);
        Assert.Equal("value", decoded.Headers!["key"]);
        Assert.Equal(original.Version, decoded.Version);
    }

    [Fact]
    public void EncodeDecode_AllFields_RoundTripsCorrectly() {
        var payloadJson = JsonSerializer.SerializeToElement(new { Name = "test", Value = 42, Nested = new { X = 1 } });
        var original = new ProtocolMessage {
            Version = 1,
            Type = CommandType.Publish,
            Id = "custom-id-123",
            Queue = "my-queue",
            Payload = payloadJson,
            Headers = new Dictionary<string, string> {
                ["header1"] = "value1",
                ["header2"] = "value2",
            },
        };

        var encoded = Codec.Encode(original);
        var decoded = Codec.Decode(encoded);

        Assert.Equal(original.Version, decoded.Version);
        Assert.Equal(original.Type, decoded.Type);
        Assert.Equal(original.Id, decoded.Id);
        Assert.Equal(original.Queue, decoded.Queue);
        Assert.NotNull(decoded.Payload);
        Assert.Equal("test", decoded.Payload.Value.GetProperty("Name").GetString());
        Assert.Equal(42, decoded.Payload.Value.GetProperty("Value").GetInt32());
        Assert.Equal(1, decoded.Payload.Value.GetProperty("Nested").GetProperty("X").GetInt32());
        Assert.Equal(original.Headers!.Count, decoded.Headers!.Count);
        Assert.Equal("value1", decoded.Headers["header1"]);
        Assert.Equal("value2", decoded.Headers["header2"]);
    }

    [Fact]
    public void EncodeDecode_NullPayload_RoundTripsCorrectly() {
        var original = new ProtocolMessage {
            Type = CommandType.Ping,
            Payload = null,
        };

        var encoded = Codec.Encode(original);
        var decoded = Codec.Decode(encoded);

        Assert.Equal(CommandType.Ping, decoded.Type);
        Assert.Null(decoded.Payload);
    }

    [Fact]
    public void EncodeDecode_EmptyPayload_RoundTripsCorrectly() {
        var payloadJson = JsonSerializer.SerializeToElement(new { });
        var original = new ProtocolMessage {
            Type = CommandType.Publish,
            Payload = payloadJson,
        };

        var encoded = Codec.Encode(original);
        var decoded = Codec.Decode(encoded);

        Assert.NotNull(decoded.Payload);
        Assert.Equal(JsonValueKind.Object, decoded.Payload.Value.ValueKind);
    }

    [Fact]
    public void EncodeDecode_PayloadWithPrimitives_RoundTripsCorrectly() {
        var payloadJson = JsonSerializer.SerializeToElement(new {
            StringValue = "hello",
            IntValue = 42,
            DoubleValue = 3.14,
            BoolValue = true,
            NullValue = (object?)null,
        });

        var original = new ProtocolMessage {
            Type = CommandType.Publish,
            Payload = payloadJson,
        };

        var encoded = Codec.Encode(original);
        var decoded = Codec.Decode(encoded);

        Assert.NotNull(decoded.Payload);
        Assert.Equal("hello", decoded.Payload.Value.GetProperty("StringValue").GetString());
        Assert.Equal(42, decoded.Payload.Value.GetProperty("IntValue").GetInt32());
        Assert.Equal(3.14, decoded.Payload.Value.GetProperty("DoubleValue").GetDouble());
        Assert.True(decoded.Payload.Value.GetProperty("BoolValue").GetBoolean());
    }

    [Fact]
    public void EncodeDecode_PayloadWithArray_RoundTripsCorrectly() {
        var payloadJson = JsonSerializer.SerializeToElement(new {
            Items = new[] { 1, 2, 3 },
            Names = new[] { "a", "b", "c" },
        });

        var original = new ProtocolMessage {
            Type = CommandType.Publish,
            Payload = payloadJson,
        };

        var encoded = Codec.Encode(original);
        var decoded = Codec.Decode(encoded);

        Assert.NotNull(decoded.Payload);
        var itemsArray = decoded.Payload.Value.GetProperty("Items");
        Assert.Equal(3, itemsArray.GetArrayLength());
        Assert.Equal(1, itemsArray[0].GetInt32());
        Assert.Equal(2, itemsArray[1].GetInt32());
        Assert.Equal(3, itemsArray[2].GetInt32());
    }

    [Fact]
    public void EncodeDecode_NullQueue_RoundTripsCorrectly() {
        var original = new ProtocolMessage {
            Type = CommandType.Ping,
            Queue = null,
        };

        var encoded = Codec.Encode(original);
        var decoded = Codec.Decode(encoded);

        Assert.Equal(CommandType.Ping, decoded.Type);
        Assert.Null(decoded.Queue);
    }

    [Fact]
    public void EncodeDecode_EmptyQueue_RoundTripsCorrectly() {
        var original = new ProtocolMessage {
            Type = CommandType.Publish,
            Queue = "",
        };

        var encoded = Codec.Encode(original);
        var decoded = Codec.Decode(encoded);

        Assert.Equal("", decoded.Queue);
    }

    [Fact]
    public void EncodeDecode_NullHeaders_RoundTripsCorrectly() {
        var original = new ProtocolMessage {
            Type = CommandType.Ping,
            Headers = null,
        };

        var encoded = Codec.Encode(original);
        var decoded = Codec.Decode(encoded);

        Assert.Equal(CommandType.Ping, decoded.Type);
        Assert.Null(decoded.Headers);
    }

    [Fact]
    public void EncodeDecode_EmptyHeaders_RoundTripsCorrectly() {
        var original = new ProtocolMessage {
            Type = CommandType.Ping,
            Headers = new Dictionary<string, string>(),
        };

        var encoded = Codec.Encode(original);
        var decoded = Codec.Decode(encoded);

        Assert.Equal(CommandType.Ping, decoded.Type);
        Assert.Null(decoded.Headers);
    }

    [Fact]
    public void EncodeDecode_ErrorMessage_RoundTripsCorrectly() {
        var original = new ProtocolMessage {
            Type = CommandType.Error,
            ErrorCode = "AUTH_FAILED",
            ErrorMessage = "Invalid token",
        };

        var encoded = Codec.Encode(original);
        var decoded = Codec.Decode(encoded);

        Assert.Equal(CommandType.Error, decoded.Type);
        Assert.Equal("AUTH_FAILED", decoded.ErrorCode);
        Assert.Equal("Invalid token", decoded.ErrorMessage);
    }

    [Fact]
    public void EncodeDecode_ErrorMessageWithNullFields_RoundTripsCorrectly() {
        var original = new ProtocolMessage {
            Type = CommandType.Error,
            ErrorCode = null,
            ErrorMessage = null,
        };

        var encoded = Codec.Encode(original);
        var decoded = Codec.Decode(encoded);

        Assert.Equal(CommandType.Error, decoded.Type);
        Assert.Null(decoded.ErrorCode);
        Assert.Null(decoded.ErrorMessage);
    }

    [Fact]
    public void EncodeDecode_AllCommandTypes_RoundTripsCorrectly() {
        var commandTypes = Enum.GetValues<CommandType>();

        foreach (var commandType in commandTypes) {
            var original = new ProtocolMessage {
                Type = commandType,
                Queue = commandType.ToString(),
            };

            var encoded = Codec.Encode(original);
            var decoded = Codec.Decode(encoded);

            Assert.Equal(commandType, decoded.Type);
            Assert.Equal(commandType.ToString(), decoded.Queue);
        }
    }

    [Fact]
    public void EncodeDecode_VersionField_RoundTripsCorrectly() {
        var original = new ProtocolMessage {
            Version = 2,
            Type = CommandType.Ping,
        };

        var encoded = Codec.Encode(original);
        var decoded = Codec.Decode(encoded);

        Assert.Equal(2, decoded.Version);
    }

    [Fact]
    public void EncodeDecode_LongString_RoundTripsCorrectly() {
        var longString = new string('a', 10000);
        var original = new ProtocolMessage {
            Type = CommandType.Publish,
            Queue = longString,
        };

        var encoded = Codec.Encode(original);
        var decoded = Codec.Decode(encoded);

        Assert.Equal(longString, decoded.Queue);
    }

    [Fact]
    public void EncodeDecode_UnicodeString_RoundTripsCorrectly() {
        var unicodeString = "Привет 🌍 你好 こんにちは";
        var original = new ProtocolMessage {
            Type = CommandType.Publish,
            Queue = unicodeString,
        };

        var encoded = Codec.Encode(original);
        var decoded = Codec.Decode(encoded);

        Assert.Equal(unicodeString, decoded.Queue);
    }

    [Fact]
    public void Decode_InsufficientData_ThrowsException() {
        byte[] data = new byte[] { 1, 2 }; // Too short

        Assert.Throws<InvalidOperationException>(() => Codec.Decode(data));
    }

    [Fact]
    public void EncodeDecode_MultipleHeaders_RoundTripsCorrectly() {
        var headers = new Dictionary<string, string>();
        for (var i = 0; i < 100; i++) {
            headers[$"key{i}"] = $"value{i}";
        }

        var original = new ProtocolMessage {
            Type = CommandType.Publish,
            Headers = headers,
        };

        var encoded = Codec.Encode(original);
        var decoded = Codec.Decode(encoded);

        Assert.NotNull(decoded.Headers);
        Assert.Equal(100, decoded.Headers!.Count);
        for (var i = 0; i < 100; i++) {
            Assert.Equal($"value{i}", decoded.Headers[$"key{i}"]);
        }
    }
}
