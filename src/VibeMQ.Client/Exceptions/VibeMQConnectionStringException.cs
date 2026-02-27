namespace VibeMQ.Client.Exceptions;

/// <summary>
/// Thrown when a VibeMQ connection string is invalid or contains an unsupported parameter.
/// </summary>
public sealed class VibeMQConnectionStringException : ArgumentException {
    /// <inheritdoc />
    public VibeMQConnectionStringException(string message)
        : base(message) { }

    /// <inheritdoc />
    public VibeMQConnectionStringException(string message, string? paramName)
        : base(message, paramName) { }

    /// <inheritdoc />
    public VibeMQConnectionStringException(string message, Exception? innerException)
        : base(message, innerException) { }
}
