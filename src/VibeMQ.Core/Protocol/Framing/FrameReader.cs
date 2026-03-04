using System.Buffers;
using System.Buffers.Binary;
using VibeMQ.Protocol.Binary;
using VibeMQ.Protocol.Compression;

namespace VibeMQ.Protocol.Framing;

/// <summary>
/// Reads length-prefixed frames from a stream with transparent decompression.
/// Format: [4 bytes: body length in Big Endian uint32] [1 byte: compression flags] [N bytes: body]
/// Uses <see cref="ArrayPool{T}"/> to minimize heap allocations for body buffers.
/// Each frame is self-contained: the compression algorithm is stored in the frame flags byte.
/// </summary>
public static class FrameReader {
    private static readonly VibeMQBinaryCodec _codec = new();

    /// <summary>
    /// Reads a single frame from the stream and deserializes it into a <see cref="ProtocolMessage"/>.
    /// Returns null if the stream is closed (0 bytes read for the length prefix).
    /// Decompresses the body when the frame flags byte indicates a compression algorithm.
    /// </summary>
    /// <param name="stream">The network stream to read from.</param>
    /// <param name="maxMessageSize">Maximum allowed compressed body size in bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the message exceeds the maximum allowed size or uses an unknown algorithm.</exception>
    /// <exception cref="IOException">Thrown when the stream is unexpectedly closed mid-frame.</exception>
    public static async Task<ProtocolMessage?> ReadFrameAsync(
        Stream stream,
        int maxMessageSize,
        CancellationToken cancellationToken = default
    ) {
        // Read a 4-byte length prefix
        var lengthBuffer = new byte[ProtocolConstants.FRAME_LENGTH_PREFIX_SIZE];
        var bytesRead = await ReadExactAsync(stream, lengthBuffer, cancellationToken).ConfigureAwait(false);

        if (bytesRead == 0) {
            return null; // Connection closed gracefully
        }

        if (bytesRead < ProtocolConstants.FRAME_LENGTH_PREFIX_SIZE) {
            throw new IOException("Connection closed unexpectedly while reading frame length.");
        }

        var bodyLength = (int)BinaryPrimitives.ReadUInt32BigEndian(lengthBuffer);

        if (bodyLength <= 0) {
            throw new InvalidOperationException("Invalid frame: body length must be positive.");
        }

        if (bodyLength > maxMessageSize) {
            throw new InvalidOperationException(
                $"Frame body size ({bodyLength} bytes) exceeds maximum allowed size ({maxMessageSize} bytes)."
            );
        }

        // Read 1-byte compression flags
        var flagsBuffer = new byte[ProtocolConstants.FRAME_FLAGS_SIZE];
        var flagsBytesRead = await ReadExactAsync(stream, flagsBuffer, cancellationToken).ConfigureAwait(false);

        if (flagsBytesRead < ProtocolConstants.FRAME_FLAGS_SIZE) {
            throw new IOException("Connection closed unexpectedly while reading frame flags.");
        }

        var compressionFlag = flagsBuffer[0];

        // Read body using pooled buffer to avoid GC pressure
        var bodyBuffer = ArrayPool<byte>.Shared.Rent(bodyLength);

        try {
            bytesRead = await ReadExactAsync(stream, bodyBuffer.AsMemory(0, bodyLength), cancellationToken)
                .ConfigureAwait(false);

            if (bytesRead < bodyLength) {
                throw new IOException("Connection closed unexpectedly while reading frame body.");
            }

            if (compressionFlag == 0x00) {
                return _codec.Decode(bodyBuffer.AsSpan(0, bodyLength));
            }

            var algorithm = (CompressionAlgorithm)compressionFlag;
            var compressor = CompressorFactory.Get(algorithm)
                ?? throw new InvalidOperationException(
                    $"Unknown compression algorithm in frame flags: 0x{compressionFlag:X2}."
                );

            var decompressed = await compressor.DecompressAsync(bodyBuffer.AsMemory(0, bodyLength))
                .ConfigureAwait(false);

            return _codec.Decode(decompressed.AsSpan());
        } finally {
            ArrayPool<byte>.Shared.Return(bodyBuffer);
        }
    }

    /// <summary>
    /// Reads exactly the specified number of bytes from the stream.
    /// Returns the number of bytes actually read (may be less if the stream ends).
    /// </summary>
    private static async Task<int> ReadExactAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken
    ) {
        var totalRead = 0;

        while (totalRead < buffer.Length) {
            var read = await stream.ReadAsync(
                buffer[totalRead..],
                cancellationToken
            ).ConfigureAwait(false);

            if (read == 0) {
                break; // Stream ended
            }

            totalRead += read;
        }

        return totalRead;
    }
}
