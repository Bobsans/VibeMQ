using Microsoft.Extensions.Logging;
using VibeMQ.Interfaces;
using VibeMQ.Protocol;
using VibeMQ.Server.Connections;

namespace VibeMQ.Server.Handlers;

/// <summary>
/// Handles Ack commands: acknowledges successful processing of a delivered message.
/// </summary>
public sealed partial class AckHandler : ICommandHandler {
    private readonly IQueueManager _queueManager;
    private readonly ILogger<AckHandler> _logger;

    public AckHandler(IQueueManager queueManager, ILogger<AckHandler> logger) {
        _queueManager = queueManager;
        _logger = logger;
    }

    public CommandType CommandType => CommandType.Ack;

    public async Task HandleAsync(
        ClientConnection connection,
        ProtocolMessage message,
        CancellationToken cancellationToken = default
    ) {
        if (string.IsNullOrEmpty(message.Id)) {
            await connection.SendErrorAsync("INVALID_MESSAGE", "Message ID is required for acknowledgment.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var acknowledged = await _queueManager.AcknowledgeAsync(message.Id, cancellationToken).ConfigureAwait(false);

        if (acknowledged) {
            LogAcknowledged(message.Id, connection.Id);
        } else {
            LogAckNotFound(message.Id, connection.Id);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Message {messageId} acknowledged by client {clientId}.")]
    private partial void LogAcknowledged(string messageId, string clientId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Ack for unknown message {messageId} from client {clientId}.")]
    private partial void LogAckNotFound(string messageId, string clientId);
}
