using System.Buffers.Binary;
using System.Text.Json;

namespace VibeMQ.Protocol.Framing;

/// <summary>
/// Writes length-prefixed frames to a stream.
/// Format: [4 bytes: body length in Big Endian uint32] [N bytes: JSON body in UTF-8]
/// </summary>
public static class FrameWriter {
    /// <summary>
    /// Serializes a <see cref="ProtocolMessage"/> and writes it as a length-prefixed frame.
    /// </summary>
    public static async Task WriteFrameAsync(
        Stream stream,
        ProtocolMessage message,
        CancellationToken cancellationToken = default
    ) {
        var json = JsonSerializer.SerializeToUtf8Bytes(message, ProtocolSerializer.Options);
        var lengthPrefix = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lengthPrefix, (uint)json.Length);

        await stream.WriteAsync(lengthPrefix, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(json, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
