using Microsoft.Extensions.Logging;
using VibeMQ.Protocol;
using VibeMQ.Server.Connections;

namespace VibeMQ.Server.Handlers;

/// <summary>
/// Handles Unsubscribe commands: removes the client from a queue's subscriber list.
/// </summary>
public sealed partial class UnsubscribeHandler : ICommandHandler {
    private readonly ILogger<UnsubscribeHandler> _logger;

    public UnsubscribeHandler(ILogger<UnsubscribeHandler> logger) {
        _logger = logger;
    }

    public CommandType CommandType => CommandType.Unsubscribe;

    public async Task HandleAsync(
        ClientConnection connection,
        ProtocolMessage message,
        CancellationToken cancellationToken = default
    ) {
        if (string.IsNullOrEmpty(message.Queue)) {
            await connection.SendErrorAsync("INVALID_QUEUE", "Queue name is required for unsubscribe.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        connection.Subscriptions.Remove(message.Queue);
        LogUnsubscribed(connection.Id, message.Queue);

        await connection.SendMessageAsync(new ProtocolMessage {
            Id = message.Id,
            Type = CommandType.UnsubscribeAck,
            Queue = message.Queue,
        }, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Client {clientId} unsubscribed from queue {queueName}.")]
    private partial void LogUnsubscribed(string clientId, string queueName);
}
