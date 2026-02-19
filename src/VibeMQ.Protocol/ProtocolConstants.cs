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
    /// Current protocol version.
    /// </summary>
    public const int PROTOCOL_VERSION = 1;

    /// <summary>
    /// Default maximum message size (1 MB).
    /// </summary>
    public const int DEFAULT_MAX_MESSAGE_SIZE = 1_048_576;
}
