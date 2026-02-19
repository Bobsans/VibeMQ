namespace VibeMQ.Protocol.Binary;

/// <summary>
/// Binary codec for serializing and deserializing ProtocolMessage to/from binary format.
/// </summary>
public interface IBinaryCodec {
    /// <summary>
    /// Encodes a ProtocolMessage into a binary format.
    /// </summary>
    /// <param name="message">The message to encode.</param>
    /// <returns>The encoded binary data.</returns>
    byte[] Encode(ProtocolMessage message);

    /// <summary>
    /// Decodes a ProtocolMessage from binary format.
    /// </summary>
    /// <param name="data">The binary data to decode.</param>
    /// <returns>The decoded ProtocolMessage.</returns>
    ProtocolMessage Decode(ReadOnlySpan<byte> data);
}
