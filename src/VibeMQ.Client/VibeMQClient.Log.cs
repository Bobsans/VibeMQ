using Microsoft.Extensions.Logging;

namespace VibeMQ.Client;

public sealed partial class VibeMQClient {
    [LoggerMessage(Level = LogLevel.Information, Message = "Connecting to VibeMQ broker at {host}:{port}...")]
    private partial void LogConnecting(string host, int port);

    [LoggerMessage(Level = LogLevel.Information, Message = "Disconnecting from VibeMQ broker...")]
    private partial void LogDisconnecting();
}
