using System.Buffers.Binary;
using System.Text.Json;

namespace VibeMQ.Protocol.Framing;

/// <summary>
/// Reads length-prefixed frames from a stream.
/// Format: [4 bytes: body length in Big Endian uint32] [N bytes: JSON body in UTF-8]
/// </summary>
public static class FrameReader {
    /// <summary>
    /// Reads a single frame from the stream and deserializes it into a <see cref="ProtocolMessage"/>.
    /// Returns null if the stream is closed (0 bytes read for the length prefix).
    /// </summary>
    /// <param name="stream">The network stream to read from.</param>
    /// <param name="maxMessageSize">Maximum allowed message size in bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the message exceeds the maximum allowed size.</exception>
    /// <exception cref="IOException">Thrown when the stream is unexpectedly closed mid-frame.</exception>
    public static async Task<ProtocolMessage?> ReadFrameAsync(
        Stream stream,
        int maxMessageSize,
        CancellationToken cancellationToken = default
    ) {
        // Read 4-byte length prefix
        var lengthBuffer = new byte[4];
        var bytesRead = await ReadExactAsync(stream, lengthBuffer, cancellationToken).ConfigureAwait(false);

        if (bytesRead == 0) {
            return null; // Connection closed gracefully
        }

        if (bytesRead < 4) {
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

        // Read body
        var bodyBuffer = new byte[bodyLength];
        bytesRead = await ReadExactAsync(stream, bodyBuffer, cancellationToken).ConfigureAwait(false);

        if (bytesRead < bodyLength) {
            throw new IOException("Connection closed unexpectedly while reading frame body.");
        }

        return JsonSerializer.Deserialize<ProtocolMessage>(bodyBuffer, ProtocolSerializer.Options)
            ?? throw new InvalidOperationException("Failed to deserialize protocol message.");
    }

    /// <summary>
    /// Reads exactly buffer.Length bytes from the stream.
    /// Returns the number of bytes actually read (may be less if the stream ends).
    /// </summary>
    private static async Task<int> ReadExactAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken
    ) {
        var totalRead = 0;

        while (totalRead < buffer.Length) {
            var read = await stream.ReadAsync(
                buffer.AsMemory(totalRead, buffer.Length - totalRead),
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
