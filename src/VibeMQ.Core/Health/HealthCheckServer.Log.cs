using Microsoft.Extensions.Logging;

namespace VibeMQ.Health;

public sealed partial class HealthCheckServer {
    [LoggerMessage(Level = LogLevel.Information, Message = "Health check server is disabled.")]
    private partial void LogDisabled();

    [LoggerMessage(Level = LogLevel.Information, Message = "Health check server started on port {port}.")]
    private partial void LogStarted(int port);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error accepting health check request.")]
    private partial void LogAcceptError(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error handling health check request.")]
    private partial void LogHandleError(Exception exception);
}
