using VibeMQ.Protocol;

namespace VibeMQ.Server.Security;

/// <summary>
/// Validates incoming protocol messages for correctness and safety.
/// </summary>
public static class MessageValidator {
    private const int MAX_QUEUE_NAME_LENGTH = 256;
    private const int MAX_HEADER_COUNT = 50;
    private const int MAX_HEADER_VALUE_LENGTH = 4096;

    /// <summary>
    /// Validates a protocol message. Returns null if valid, or an error message string.
    /// </summary>
    public static string? Validate(ProtocolMessage message) {
        if (string.IsNullOrEmpty(message.Id)) {
            return "Message ID is required.";
        }

        if (message.Queue is not null && message.Queue.Length > MAX_QUEUE_NAME_LENGTH) {
            return $"Queue name exceeds maximum length ({MAX_QUEUE_NAME_LENGTH}).";
        }

        if (message.Queue is not null && !IsValidQueueName(message.Queue)) {
            return "Queue name contains invalid characters. Allowed: alphanumeric, hyphens, underscores, dots.";
        }

        if (message.Headers is not null) {
            if (message.Headers.Count > MAX_HEADER_COUNT) {
                return $"Too many headers ({message.Headers.Count}). Maximum: {MAX_HEADER_COUNT}.";
            }

            foreach (var (key, value) in message.Headers) {
                if (string.IsNullOrEmpty(key)) {
                    return "Header key cannot be empty.";
                }

                if (value.Length > MAX_HEADER_VALUE_LENGTH) {
                    return $"Header value for '{key}' exceeds maximum length ({MAX_HEADER_VALUE_LENGTH}).";
                }
            }
        }

        return null;
    }

    private static bool IsValidQueueName(string name) {
        foreach (var c in name) {
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '_' && c != '.') {
                return false;
            }
        }

        return name.Length > 0;
    }
}
