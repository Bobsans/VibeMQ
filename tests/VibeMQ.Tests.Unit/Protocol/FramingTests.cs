using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using VibeMQ.Protocol;
using VibeMQ.Protocol.Framing;

namespace VibeMQ.Tests.Unit.Protocol;

public class FramingTests {
    [Fact]
    public async Task WriteAndRead_RoundTrips() {
        var original = new ProtocolMessage {
            Type = CommandType.Publish,
            Queue = "test-queue",
        };

        using var stream = new MemoryStream();

        await FrameWriter.WriteFrameAsync(stream, original);

        stream.Position = 0;

        var result = await FrameReader.ReadFrameAsync(stream, ProtocolConstants.DEFAULT_MAX_MESSAGE_SIZE);

        Assert.NotNull(result);
        Assert.Equal(original.Id, result.Id);
        Assert.Equal(CommandType.Publish, result.Type);
        Assert.Equal("test-queue", result.Queue);
    }

    [Fact]
    public async Task Read_EmptyStream_ReturnsNull() {
        using var stream = new MemoryStream();

        var result = await FrameReader.ReadFrameAsync(stream, ProtocolConstants.DEFAULT_MAX_MESSAGE_SIZE);

        Assert.Null(result);
    }

    [Fact]
    public async Task Read_OversizedFrame_ThrowsInvalidOperation() {
        var message = new ProtocolMessage { Type = CommandType.Ping };

        using var stream = new MemoryStream();
        await FrameWriter.WriteFrameAsync(stream, message);

        stream.Position = 0;

        // Set max size to 1 byte — way too small
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => FrameReader.ReadFrameAsync(stream, maxMessageSize: 1)
        );
    }

    [Fact]
    public async Task Read_TruncatedBody_ThrowsIOException() {
        // Write a length prefix that says 1000 bytes, but only write 5
        using var stream = new MemoryStream();
        var lengthPrefix = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lengthPrefix, 1000);
        stream.Write(lengthPrefix);
        stream.Write(Encoding.UTF8.GetBytes("hello"));
        stream.Position = 0;

        await Assert.ThrowsAsync<IOException>(
            () => FrameReader.ReadFrameAsync(stream, ProtocolConstants.DEFAULT_MAX_MESSAGE_SIZE)
        );
    }

    [Fact]
    public async Task Write_ProducesCorrectLengthPrefix() {
        var message = new ProtocolMessage { Type = CommandType.Ping };

        using var stream = new MemoryStream();
        await FrameWriter.WriteFrameAsync(stream, message);

        stream.Position = 0;
        var prefix = new byte[4];
        await stream.ReadExactlyAsync(prefix);

        var bodyLength = BinaryPrimitives.ReadUInt32BigEndian(prefix);
        Assert.Equal((uint)(stream.Length - 4), bodyLength);
    }

    [Fact]
    public async Task WriteAndRead_MultipleMessages_InSequence() {
        var messages = new[] {
            new ProtocolMessage { Type = CommandType.Connect },
            new ProtocolMessage { Type = CommandType.Publish, Queue = "q1" },
            new ProtocolMessage { Type = CommandType.Ping },
        };

        using var stream = new MemoryStream();

        foreach (var msg in messages) {
            await FrameWriter.WriteFrameAsync(stream, msg);
        }

        stream.Position = 0;

        for (var i = 0; i < messages.Length; i++) {
            var result = await FrameReader.ReadFrameAsync(stream, ProtocolConstants.DEFAULT_MAX_MESSAGE_SIZE);
            Assert.NotNull(result);
            Assert.Equal(messages[i].Type, result.Type);
        }
    }

    [Fact]
    public async Task Read_ZeroLengthBody_ThrowsInvalidOperation() {
        using var stream = new MemoryStream();
        var lengthPrefix = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lengthPrefix, 0);
        stream.Write(lengthPrefix);
        stream.Position = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => FrameReader.ReadFrameAsync(stream, ProtocolConstants.DEFAULT_MAX_MESSAGE_SIZE)
        );
    }
}
