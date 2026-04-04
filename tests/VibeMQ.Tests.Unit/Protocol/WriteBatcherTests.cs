using System.Buffers.Binary;
using VibeMQ.Protocol;
using VibeMQ.Protocol.Framing;

namespace VibeMQ.Tests.Unit.Protocol;

public class WriteBatcherTests : IDisposable {
    private readonly MemoryStream _stream = new();
    private WriteBatcher? _batcher;

    public void Dispose() {
        _batcher?.Dispose();
        _stream.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void PendingCount_Initially_IsZero() {
        _batcher = new WriteBatcher(_stream);
        Assert.Equal(0, _batcher.PendingCount);
    }

    [Fact]
    public async Task AddAsync_ThenFlush_WritesLengthPrefixAndBody() {
        _batcher = new WriteBatcher(_stream);
        var message = new ProtocolMessage { Type = CommandType.Ping };

        await _batcher.AddAsync(message);
        Assert.Equal(1, _batcher.PendingCount);

        await _batcher.FlushAsync();

        Assert.Equal(0, _batcher.PendingCount);
        Assert.True(_stream.Length >= 5, "Stream should contain 4-byte length + at least 1 byte body");

        _stream.Position = 0;
        var lengthBytes = new byte[4];
        _stream.ReadExactly(lengthBytes);
        var frameLength = BinaryPrimitives.ReadUInt32BigEndian(lengthBytes);
        Assert.True(frameLength > 0);
        Assert.Equal(_stream.Length - 4, frameLength);
    }

    [Fact]
    public async Task FlushAsync_WhenNothingBuffered_DoesNotWrite() {
        _batcher = new WriteBatcher(_stream);
        await _batcher.FlushAsync();
        Assert.Equal(0, _stream.Length);
    }

    [Fact]
    public async Task AddAsync_WhenMaxBatchSizeReached_AutoFlushes() {
        _batcher = new WriteBatcher(_stream, initialBufferSize: 8192, maxBatchSize: 3);
        var message = new ProtocolMessage { Type = CommandType.Ping };

        await _batcher.AddAsync(message);
        await _batcher.AddAsync(message);
        Assert.Equal(2, _batcher.PendingCount);

        await _batcher.AddAsync(message);
        Assert.Equal(0, _batcher.PendingCount);
    }

    [Fact]
    public async Task AddAsync_MultipleMessages_AllWrittenAfterFlush() {
        _batcher = new WriteBatcher(_stream, maxBatchSize: 10);
        var msg = new ProtocolMessage { Type = CommandType.Ping };

        for (var i = 0; i < 5; i++) {
            await _batcher.AddAsync(msg);
        }
        await _batcher.FlushAsync();

        _stream.Position = 0;
        var totalRead = 0L;
        var frames = 0;
        while (_stream.Position < _stream.Length) {
            var lenBytes = new byte[4];
            _stream.ReadExactly(lenBytes);
            var len = BinaryPrimitives.ReadUInt32BigEndian(lenBytes);
            _stream.Seek(len, SeekOrigin.Current);
            totalRead += 4 + len;
            frames++;
        }
        Assert.Equal(5, frames);
        Assert.Equal(_stream.Length, totalRead);
    }

    [Fact]
    public void Dispose_DoesNotThrow() {
        _batcher = new WriteBatcher(_stream);
        _batcher.Dispose();
        _batcher.Dispose();
    }

    [Fact]
    public async Task AddAsync_WithSmallInitialBuffer_GrowsBuffer() {
        _batcher = new WriteBatcher(_stream, initialBufferSize: 8, maxBatchSize: 64);
        var message = new ProtocolMessage { Type = CommandType.Ping };

        await _batcher.AddAsync(message);
        await _batcher.FlushAsync();

        Assert.True(_stream.Length >= 5);
    }
}
