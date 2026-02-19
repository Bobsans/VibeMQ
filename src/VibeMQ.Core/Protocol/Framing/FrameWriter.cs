using System.Buffers;
using System.Buffers.Binary;
using VibeMQ.Protocol.Binary;

namespace VibeMQ.Protocol.Framing;

/// <summary>
/// Writes length-prefixed frames to a stream.
/// Format: [4 bytes: body length in Big Endian uint32] [N bytes: binary body]
/// Uses <see cref="ArrayPool{T}"/> and single write call to minimize allocations and syscalls.
/// </summary>
public static class FrameWriter {
    private static readonly VibeMQBinaryCodec Codec = new();

    /// <summary>
    /// Serializes a <see cref="ProtocolMessage"/> and writes it as a length-prefixed frame.
    /// Combines the length prefix and body into a single buffer to reduce write syscalls.
    /// </summary>
    public static async Task WriteFrameAsync(
        Stream stream,
        ProtocolMessage message,
        CancellationToken cancellationToken = default
    ) {
        var binaryBody = Codec.Encode(message);
        var totalLength = 4 + binaryBody.Length;

        // Rent a combined buffer: [4 bytes prefix] + [N bytes body]
        var buffer = ArrayPool<byte>.Shared.Rent(totalLength);

        try {
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(0, 4), (uint)binaryBody.Length);
            binaryBody.CopyTo(buffer, 4);

            // Single write call instead of two separate writes
            await stream.WriteAsync(buffer.AsMemory(0, totalLength), cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        } finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
