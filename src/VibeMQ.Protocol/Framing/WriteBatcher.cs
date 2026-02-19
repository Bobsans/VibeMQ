using System.Buffers;
using System.Buffers.Binary;
using VibeMQ.Protocol.Binary;

namespace VibeMQ.Protocol.Framing;

/// <summary>
/// Batches multiple protocol messages into a single write operation.
/// Reduces the number of syscalls and TCP packets for high-throughput scenarios.
/// </summary>
public sealed class WriteBatcher : IDisposable {
    private static readonly VibeMQBinaryCodec Codec = new();
    private readonly Stream _stream;
    private readonly int _maxBatchSize;
    private byte[] _buffer;
    private int _position;
    private bool _disposed;

    /// <summary>
    /// Creates a new write batcher.
    /// </summary>
    /// <param name="stream">The underlying stream to write to.</param>
    /// <param name="initialBufferSize">Initial buffer size. Default: 8 KB.</param>
    /// <param name="maxBatchSize">Maximum number of messages to batch before auto-flush. Default: 64.</param>
    public WriteBatcher(Stream stream, int initialBufferSize = 8192, int maxBatchSize = 64) {
        _stream = stream;
        _maxBatchSize = maxBatchSize;
        _buffer = ArrayPool<byte>.Shared.Rent(initialBufferSize);
    }

    /// <summary>
    /// Number of messages currently in the batch.
    /// </summary>
    public int PendingCount { get; private set; }

    /// <summary>
    /// Adds a message to the batch. Auto-flushes when <see cref="_maxBatchSize"/> is reached.
    /// </summary>
    public async Task AddAsync(ProtocolMessage message, CancellationToken cancellationToken = default) {
        var binaryBody = Codec.Encode(message);
        var frameSize = 4 + binaryBody.Length;

        EnsureCapacity(frameSize);

        BinaryPrimitives.WriteUInt32BigEndian(_buffer.AsSpan(_position, 4), (uint)binaryBody.Length);
        _position += 4;

        binaryBody.CopyTo(_buffer, _position);
        _position += binaryBody.Length;

        PendingCount++;

        if (PendingCount >= _maxBatchSize) {
            await FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Writes all buffered frames to the underlying stream in a single operation.
    /// </summary>
    public async Task FlushAsync(CancellationToken cancellationToken = default) {
        if (_position == 0) {
            return;
        }

        await _stream.WriteAsync(_buffer.AsMemory(0, _position), cancellationToken).ConfigureAwait(false);
        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);

        _position = 0;
        PendingCount = 0;
    }

    private void EnsureCapacity(int additionalBytes) {
        var required = _position + additionalBytes;

        if (required <= _buffer.Length) {
            return;
        }

        // Double the buffer size until it fits
        var newSize = _buffer.Length;

        while (newSize < required) {
            newSize *= 2;
        }

        var newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
        _buffer.AsSpan(0, _position).CopyTo(newBuffer);
        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = newBuffer;
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        _disposed = true;
        ArrayPool<byte>.Shared.Return(_buffer);
    }
}
