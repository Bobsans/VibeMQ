using Microsoft.Extensions.Logging;

namespace VibeMQ.Client.DependencyInjection;

internal sealed partial class ManagedVibeMQClient {
    [LoggerMessage(Level = LogLevel.Warning, Message = "Managed VibeMQ client dispose did not complete within {Seconds}s.")]
    private partial void LogDisposeTimeout(int seconds);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error disposing managed VibeMQ client.")]
    private partial void LogDisposeError(Exception exception);
}
