using System.Buffers;
using System.Buffers.Binary;
using VibeMQ.Protocol.Binary;
using VibeMQ.Protocol.Compression;

namespace VibeMQ.Protocol.Framing;

/// <summary>
/// Writes length-prefixed frames to a stream with optional compression.
/// Format: [4 bytes: body length in Big Endian uint32] [1 byte: compression flags] [N bytes: body]
/// Uses <see cref="ArrayPool{T}"/> and a single write call to minimize allocations and syscalls.
/// </summary>
public sealed class FrameWriter {
    /// <summary>
    /// Reusable buffer writer to avoid re-allocating on every frame.
    /// Not thread-safe — callers must serialize access (e.g. via _writeLock).
    /// </summary>
    private readonly ArrayBufferWriter<byte> _encodeBuffer = new();

    private CompressionAlgorithm _algorithm = CompressionAlgorithm.None;
    private int _threshold = ProtocolConstants.COMPRESSION_THRESHOLD;

    /// <summary>
    /// Configures the compression algorithm and the minimum body size that triggers compression.
    /// Call this after a successful compression negotiation handshake.
    /// </summary>
    /// <param name="algorithm">Algorithm to use for subsequent frames.</param>
    /// <param name="threshold">Minimum uncompressed body size in bytes. Smaller bodies are sent as-is.</param>
    public void SetCompression(CompressionAlgorithm algorithm, int threshold = ProtocolConstants.COMPRESSION_THRESHOLD) {
        _algorithm = algorithm;
        _threshold = threshold;
    }

    /// <summary>
    /// Serializes a <see cref="ProtocolMessage"/> and writes it as a length-prefixed frame.
    /// Applies compression when the configured algorithm is not <see cref="CompressionAlgorithm.None"/>
    /// and the serialized body size meets the threshold.
    /// </summary>
    public async Task WriteFrameAsync(
        Stream stream,
        ProtocolMessage message,
        CancellationToken cancellationToken = default
    ) {
        // Encode into reusable buffer (safe: caller hold _writeLock)
        _encodeBuffer.ResetWrittenCount();
        VibeMQBinaryCodec.EncodeTo(message, _encodeBuffer);

        var encodedSpan = _encodeBuffer.WrittenSpan;

        if (_algorithm != CompressionAlgorithm.None && encodedSpan.Length >= _threshold) {
            var compressor = CompressorFactory.Get(_algorithm)
                ?? throw new InvalidOperationException($"No compressor registered for algorithm {_algorithm}.");
            var compressedBody = await compressor.CompressAsync(_encodeBuffer.WrittenMemory).ConfigureAwait(false);

            await WriteFrameToStreamAsync(stream, compressedBody, (byte)_algorithm, cancellationToken).ConfigureAwait(false);
        } else {
            // No compression — write header + encoded body directly, avoiding an extra byte[] allocation
            var bodyLength = encodedSpan.Length;
            var totalLength = ProtocolConstants.FRAME_LENGTH_PREFIX_SIZE + ProtocolConstants.FRAME_FLAGS_SIZE + bodyLength;
            var buffer = ArrayPool<byte>.Shared.Rent(totalLength);

            try {
                BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(0, 4), (uint)bodyLength);
                buffer[4] = 0x00;
                encodedSpan.CopyTo(buffer.AsSpan(5));

                await stream.WriteAsync(buffer.AsMemory(0, totalLength), cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            } finally {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    private static async Task WriteFrameToStreamAsync(
        Stream stream,
        byte[] body,
        byte flags,
        CancellationToken cancellationToken
    ) {
        var totalLength = ProtocolConstants.FRAME_LENGTH_PREFIX_SIZE + ProtocolConstants.FRAME_FLAGS_SIZE + body.Length;
        var buffer = ArrayPool<byte>.Shared.Rent(totalLength);

        try {
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(0, 4), (uint)body.Length);
            buffer[4] = flags;
            body.CopyTo(buffer, 5);

            await stream.WriteAsync(buffer.AsMemory(0, totalLength), cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        } finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
