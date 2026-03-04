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
    private static readonly VibeMQBinaryCodec _codec = new();

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
        var binaryBody = _codec.Encode(message);
        byte[] body;
        byte flags;

        if (_algorithm != CompressionAlgorithm.None && binaryBody.Length >= _threshold) {
            var compressor = CompressorFactory.Get(_algorithm)!;
            body = await compressor.CompressAsync(binaryBody).ConfigureAwait(false);
            flags = (byte)_algorithm;
        } else {
            body = binaryBody;
            flags = 0x00;
        }

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
