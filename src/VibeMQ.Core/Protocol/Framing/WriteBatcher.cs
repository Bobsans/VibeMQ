using System.Buffers;
using System.Buffers.Binary;
using VibeMQ.Protocol.Binary;

namespace VibeMQ.Protocol.Framing;

/// <summary>
/// Batches multiple protocol messages into a single write operation.
/// Reduces the number of syscalls and TCP packets for high-throughput scenarios.
/// </summary>
/// <remarks>
/// Creates a new write batcher.
/// </remarks>
/// <param name="stream">The underlying stream to write to.</param>
/// <param name="initialBufferSize">Initial buffer size. Default: 8 KB.</param>
/// <param name="maxBatchSize">Maximum number of messages to batch before auto-flush. Default: 64.</param>
public sealed class WriteBatcher(Stream stream, int initialBufferSize = 8192, int maxBatchSize = 64) : IDisposable {
    private byte[] _buffer = ArrayPool<byte>.Shared.Rent(initialBufferSize);
    private int _position;
    private bool _disposed;
    private int _pendingCount;
#if NET10_0_OR_GREATER
    private readonly Lock _batchLock = new();
#else
    private readonly object _batchLock = new();
#endif

    /// <summary>
    /// Number of messages currently in the batch.
    /// </summary>
    public int PendingCount => Volatile.Read(ref _pendingCount);

    /// <summary>
    /// Adds a message to the batch. Auto-flushes when <see cref="maxBatchSize"/> is reached.
    /// </summary>
    /// <summary>
    /// Reusable buffer writer for encoding — used inside the lock, so no concurrent access.
    /// </summary>
    private readonly ArrayBufferWriter<byte> _encodeBuffer = new();

    public async Task AddAsync(ProtocolMessage message, CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);

        bool shouldFlush;

        lock (_batchLock) {
            _encodeBuffer.ResetWrittenCount();
            VibeMQBinaryCodec.EncodeTo(message, _encodeBuffer);
            var encoded = _encodeBuffer.WrittenSpan;
            var frameSize = 4 + encoded.Length;

            EnsureCapacity(frameSize);

            BinaryPrimitives.WriteUInt32BigEndian(_buffer.AsSpan(_position, 4), (uint)encoded.Length);
            _position += 4;

            encoded.CopyTo(_buffer.AsSpan(_position));
            _position += encoded.Length;

            _pendingCount++;
            shouldFlush = _pendingCount >= maxBatchSize;
        }

        if (shouldFlush) {
            await FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Writes all buffered frames to the underlying stream in a single operation.
    /// </summary>
    public async Task FlushAsync(CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);

        byte[] bufferToWrite;
        int length;

        lock (_batchLock) {
            if (_position == 0) {
                return;
            }

            // Swap the buffer so AddAsync can continue writing into a fresh one
            // while we flush the snapshot outside the lock.
            bufferToWrite = _buffer;
            length = _position;
            _buffer = ArrayPool<byte>.Shared.Rent(bufferToWrite.Length);
            _position = 0;
            _pendingCount = 0;
        }

        try {
            await stream.WriteAsync(bufferToWrite.AsMemory(0, length), cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        } finally {
            ArrayPool<byte>.Shared.Return(bufferToWrite);
        }
    }

    private void EnsureCapacity(int additionalBytes) {
        var required = _position + additionalBytes;

        if (required <= _buffer.Length) {
            return;
        }

        // Double the buffer size until it fits, with overflow protection
        var newSize = _buffer.Length;

        while (newSize < required) {
            checked { newSize *= 2; }
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
