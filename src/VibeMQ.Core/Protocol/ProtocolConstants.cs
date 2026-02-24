namespace VibeMQ.Protocol;

/// <summary>
/// Protocol-level constants.
/// </summary>
public static class ProtocolConstants {
    /// <summary>
    /// Size of the frame length prefix in bytes.
    /// </summary>
    public const int FRAME_LENGTH_PREFIX_SIZE = 4;

    /// <summary>
    /// Size of the compression flags byte in the frame header.
    /// </summary>
    public const int FRAME_FLAGS_SIZE = 1;

    /// <summary>
    /// Current protocol version.
    /// </summary>
    public const int PROTOCOL_VERSION = 1;

    /// <summary>
    /// Default maximum message size (1 MB).
    /// </summary>
    public const int DEFAULT_MAX_MESSAGE_SIZE = 1_048_576;

    /// <summary>
    /// Minimum frame body size in bytes required to apply compression.
    /// Bodies smaller than this threshold are sent uncompressed.
    /// </summary>
    public const int COMPRESSION_THRESHOLD = 1024;
}
