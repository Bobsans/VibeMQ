using Microsoft.Extensions.Logging;

namespace VibeMQ.Server;

public sealed partial class BrokerServer {
    [LoggerMessage(Level = LogLevel.Information, Message = "VibeMQ server starting on port {port}...")]
    private partial void LogServerStarting(int port);

    [LoggerMessage(Level = LogLevel.Information, Message = "VibeMQ server shutting down...")]
    private partial void LogServerShuttingDown();
}
