using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace VibeMQ.Protocol.Binary;

/// <summary>
/// Binary codec implementation for VibeMQ protocol.
/// Format: version (1B) | type (1B) | id (2B len + UTF-8) | queue (2B len + UTF-8) | 
///         payload (4B len + UTF-8 JSON) | headers (2B count + pairs) | errorCode (2B len + UTF-8) | errorMessage (2B len + UTF-8)
/// All lengths are Big Endian.
/// </summary>
public sealed class VibeMQBinaryCodec : IBinaryCodec {
    private static readonly JsonSerializerOptions JsonOptions = ProtocolSerializer.Options;

    /// <summary>
    /// Encodes a ProtocolMessage into binary format.
    /// </summary>
    public byte[] Encode(ProtocolMessage message) {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = buffer;

        // version (1 byte) - first field for protocol version handling
        writer.GetSpan(1)[0] = (byte)message.Version;
        writer.Advance(1);

        // type (1 byte) - CommandType enum value
        writer.GetSpan(1)[0] = (byte)message.Type;
        writer.Advance(1);

        // id (2B len + UTF-8) - always present
        WriteString16(writer, message.Id);

        // queue (2B len + UTF-8) - length 0 = null/absent
        WriteString16(writer, message.Queue);

        // payload (4B len + UTF-8 JSON) - length 0 = null/absent
        WritePayload(writer, message.Payload);

        // headers (2B count + pairs of 2B len + UTF-8)
        WriteHeaders(writer, message.Headers);

        // errorCode (2B len + UTF-8) - only for Error type, length 0 = absent
        if (message.Type == CommandType.Error) {
            WriteString16(writer, message.ErrorCode);
            WriteString16(writer, message.ErrorMessage);
        }

        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>
    /// Decodes a ProtocolMessage from binary format.
    /// </summary>
    public ProtocolMessage Decode(ReadOnlySpan<byte> data) {
        var offset = 0;

        // version (1 byte)
        var version = data[offset];
        offset += 1;

        // type (1 byte)
        var type = (CommandType)data[offset];
        offset += 1;

        // id (2B len + UTF-8) - always present
        var (id, idLength) = ReadString16(data[offset..]);
        offset += idLength;
        if (id == null) {
            throw new InvalidOperationException("Message id cannot be null.");
        }

        // queue (2B len + UTF-8)
        var (queue, queueLength) = ReadString16(data[offset..]);
        offset += queueLength;

        // payload (4B len + UTF-8 JSON)
        var (payload, payloadLength) = ReadPayload(data[offset..]);
        offset += payloadLength;

        // headers (2B count + pairs)
        var (headers, headersLength) = ReadHeaders(data[offset..]);
        offset += headersLength;

        // errorCode and errorMessage (only for Error type)
        string? errorCode = null;
        string? errorMessage = null;
        if (type == CommandType.Error) {
            var (ec, ecLength) = ReadString16(data[offset..]);
            errorCode = ec;
            offset += ecLength;

            var (em, emLength) = ReadString16(data[offset..]);
            errorMessage = em;
            offset += emLength;
        }

        return new ProtocolMessage {
            Version = version,
            Type = type,
            Id = id!,
            Queue = queue,
            Payload = payload,
            Headers = headers,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
        };
    }

    private static void WriteString16(ArrayBufferWriter<byte> writer, string? value) {
        if (string.IsNullOrEmpty(value)) {
            BinaryPrimitives.WriteUInt16BigEndian(writer.GetSpan(2), 0);
            writer.Advance(2);
            return;
        }

        var byteCount = Encoding.UTF8.GetByteCount(value);
        var lengthSpan = writer.GetSpan(2);
        BinaryPrimitives.WriteUInt16BigEndian(lengthSpan, (ushort)byteCount);
        writer.Advance(2);

        var stringSpan = writer.GetSpan(byteCount);
        Encoding.UTF8.GetBytes(value, stringSpan);
        writer.Advance(byteCount);
    }

    private static (string?, int) ReadString16(ReadOnlySpan<byte> data) {
        if (data.Length < 2) {
            throw new InvalidOperationException("Insufficient data to read string length.");
        }

        var length = BinaryPrimitives.ReadUInt16BigEndian(data[0..2]);

        if (length == 0) {
            return (null, 2);
        }

        if (data.Length < 2 + length) {
            throw new InvalidOperationException($"Insufficient data to read string of length {length}.");
        }

        var value = Encoding.UTF8.GetString(data[2..(2 + length)]);
        return (value, 2 + length);
    }

    private static void WritePayload(ArrayBufferWriter<byte> writer, JsonElement? payload) {
        if (!payload.HasValue) {
            BinaryPrimitives.WriteUInt32BigEndian(writer.GetSpan(4), 0);
            writer.Advance(4);
            return;
        }

        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(payload.Value, JsonOptions);
        var lengthSpan = writer.GetSpan(4);
        BinaryPrimitives.WriteUInt32BigEndian(lengthSpan, (uint)jsonBytes.Length);
        writer.Advance(4);

        var payloadSpan = writer.GetSpan(jsonBytes.Length);
        jsonBytes.CopyTo(payloadSpan);
        writer.Advance(jsonBytes.Length);
    }

    private static (JsonElement?, int) ReadPayload(ReadOnlySpan<byte> data) {
        if (data.Length < 4) {
            throw new InvalidOperationException("Insufficient data to read payload length.");
        }

        var length = BinaryPrimitives.ReadUInt32BigEndian(data[0..4]);

        if (length == 0) {
            return (null, 4);
        }

        if (data.Length < 4 + (int)length) {
            throw new InvalidOperationException($"Insufficient data to read payload of length {length}.");
        }

        var jsonSpan = data[4..(4 + (int)length)];
        var payload = JsonSerializer.Deserialize<JsonElement>(jsonSpan, JsonOptions);
        return (payload, 4 + (int)length);
    }

    private static void WriteHeaders(ArrayBufferWriter<byte> writer, Dictionary<string, string>? headers) {
        if (headers == null || headers.Count == 0) {
            BinaryPrimitives.WriteUInt16BigEndian(writer.GetSpan(2), 0);
            writer.Advance(2);
            return;
        }

        var countSpan = writer.GetSpan(2);
        BinaryPrimitives.WriteUInt16BigEndian(countSpan, (ushort)headers.Count);
        writer.Advance(2);

        foreach (var (key, value) in headers) {
            WriteString16(writer, key);
            WriteString16(writer, value);
        }
    }

    private static (Dictionary<string, string>?, int) ReadHeaders(ReadOnlySpan<byte> data) {
        if (data.Length < 2) {
            throw new InvalidOperationException("Insufficient data to read headers count.");
        }

        var count = BinaryPrimitives.ReadUInt16BigEndian(data[0..2]);
        var offset = 2;

        if (count == 0) {
            return (null, offset);
        }

        var headers = new Dictionary<string, string>();

        for (var i = 0; i < count; i++) {
            var (key, keyLength) = ReadString16(data[offset..]);
            offset += keyLength;

            var (value, valueLength) = ReadString16(data[offset..]);
            offset += valueLength;

            if (key != null && value != null) {
                headers[key] = value;
            }
        }

        return (headers, offset);
    }
}
