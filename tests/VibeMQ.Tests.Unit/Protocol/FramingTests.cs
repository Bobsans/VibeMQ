using System.Buffers.Binary;
using VibeMQ.Protocol;
using VibeMQ.Protocol.Compression;
using VibeMQ.Protocol.Framing;

namespace VibeMQ.Tests.Unit.Protocol;

public class FramingTests {
    [Fact]
    public async Task WriteAndRead_RoundTrips() {
        var original = new ProtocolMessage {
            Type = CommandType.Publish,
            Queue = "test-queue"
        };

        using var stream = new MemoryStream();

        await new FrameWriter().WriteFrameAsync(stream, original);

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
        await new FrameWriter().WriteFrameAsync(stream, message);

        stream.Position = 0;

        // Set max size to 1 byte — way too small
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => FrameReader.ReadFrameAsync(stream, maxMessageSize: 1)
        );
    }

    [Fact]
    public async Task Read_TruncatedBody_ThrowsIOException() {
        // Write a length prefix (1000) + flags byte + 4 bytes of body — frame is incomplete
        using var stream = new MemoryStream();
        var lengthPrefix = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lengthPrefix, 1000);
        stream.Write(lengthPrefix);
        stream.Write("hello"u8); // 1 byte flags + 4 bytes body (not enough)
        stream.Position = 0;

        await Assert.ThrowsAsync<IOException>(
            () => FrameReader.ReadFrameAsync(stream, ProtocolConstants.DEFAULT_MAX_MESSAGE_SIZE)
        );
    }

    [Fact]
    public async Task Write_ProducesCorrectLengthPrefix() {
        var message = new ProtocolMessage { Type = CommandType.Ping };

        using var stream = new MemoryStream();
        await new FrameWriter().WriteFrameAsync(stream, message);

        stream.Position = 0;
        var prefix = new byte[4];
        await stream.ReadExactlyAsync(prefix);

        var bodyLength = BinaryPrimitives.ReadUInt32BigEndian(prefix);
        // Frame layout: [4B length][1B flags][N bytes body]
        Assert.Equal((uint)(stream.Length - 5), bodyLength);
    }

    [Fact]
    public async Task Write_ProducesNoCompressionFlagByDefault() {
        var message = new ProtocolMessage { Type = CommandType.Ping };

        using var stream = new MemoryStream();
        await new FrameWriter().WriteFrameAsync(stream, message);

        stream.Position = 4; // skip length prefix
        var flags = stream.ReadByte();

        Assert.Equal(0x00, flags);
    }

    [Fact]
    public async Task WriteAndRead_MultipleMessages_InSequence() {
        var messages = new[] {
            new ProtocolMessage { Type = CommandType.Connect },
            new ProtocolMessage { Type = CommandType.Publish, Queue = "q1" },
            new ProtocolMessage { Type = CommandType.Ping }
        };

        using var stream = new MemoryStream();
        var writer = new FrameWriter();

        foreach (var msg in messages) {
            await writer.WriteFrameAsync(stream, msg);
        }

        stream.Position = 0;

        foreach (var msg in messages) {
            var result = await FrameReader.ReadFrameAsync(stream, ProtocolConstants.DEFAULT_MAX_MESSAGE_SIZE);
            Assert.NotNull(result);
            Assert.Equal(msg.Type, result.Type);
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

    [Theory]
    [InlineData(CompressionAlgorithm.GZip)]
    [InlineData(CompressionAlgorithm.Brotli)]
    public async Task WriteAndRead_WithCompression_RoundTrips(CompressionAlgorithm algorithm) {
        var original = new ProtocolMessage {
            Type = CommandType.Publish,
            Queue = "compressed-queue",
            Headers = new Dictionary<string, string> { ["key"] = "value" }
        };

        var writer = new FrameWriter();
        writer.SetCompression(algorithm, threshold: 0); // force compression for any size

        using var stream = new MemoryStream();
        await writer.WriteFrameAsync(stream, original);

        stream.Position = 0;

        // Verify the flags byte reflects the algorithm
        var prefix = new byte[4];
        await stream.ReadExactlyAsync(prefix);
        var flagsByte = stream.ReadByte();
        Assert.Equal((byte)algorithm, flagsByte);

        stream.Position = 0;
        var result = await FrameReader.ReadFrameAsync(stream, ProtocolConstants.DEFAULT_MAX_MESSAGE_SIZE);

        Assert.NotNull(result);
        Assert.Equal(original.Id, result.Id);
        Assert.Equal(CommandType.Publish, result.Type);
        Assert.Equal("compressed-queue", result.Queue);
    }

    [Theory]
    [InlineData(CompressionAlgorithm.GZip)]
    [InlineData(CompressionAlgorithm.Brotli)]
    public async Task Write_BelowThreshold_SkipsCompression(CompressionAlgorithm algorithm) {
        var message = new ProtocolMessage { Type = CommandType.Ping };

        var writer = new FrameWriter();
        writer.SetCompression(algorithm, threshold: int.MaxValue); // threshold so high it's never reached

        using var stream = new MemoryStream();
        await writer.WriteFrameAsync(stream, message);

        stream.Position = 4; // skip length prefix
        var flags = stream.ReadByte();

        Assert.Equal(0x00, flags); // no compression applied
    }

    [Fact]
    public async Task Read_UnknownCompressionFlag_ThrowsInvalidOperation() {
        // Manually craft a frame with an unknown compression flag (0xFF)
        var body = new byte[] { 0x01, 0x02, 0x03 };
        var lengthPrefix = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lengthPrefix, (uint)body.Length);

        using var stream = new MemoryStream();
        stream.Write(lengthPrefix);
        stream.WriteByte(0xFF); // unknown algorithm
        stream.Write(body);
        stream.Position = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => FrameReader.ReadFrameAsync(stream, ProtocolConstants.DEFAULT_MAX_MESSAGE_SIZE)
        );
    }
}
