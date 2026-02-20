using Microsoft.Extensions.Logging;
using VibeMQ.Interfaces;
using VibeMQ.Protocol;
using VibeMQ.Server.Connections;

namespace VibeMQ.Server.Handlers;

/// <summary>
/// Handles DeleteQueue commands: removes a queue by name.
/// </summary>
public sealed partial class DeleteQueueHandler : ICommandHandler {
    private readonly IQueueManager _queueManager;
    private readonly ILogger<DeleteQueueHandler> _logger;

    public DeleteQueueHandler(IQueueManager queueManager, ILogger<DeleteQueueHandler> logger) {
        _queueManager = queueManager;
        _logger = logger;
    }

    public CommandType CommandType => CommandType.DeleteQueue;

    public async Task HandleAsync(
        ClientConnection connection,
        ProtocolMessage message,
        CancellationToken cancellationToken = default
    ) {
        if (string.IsNullOrEmpty(message.Queue)) {
            await connection.SendErrorAsync("INVALID_QUEUE", "Queue name is required for DeleteQueue.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await _queueManager.DeleteQueueAsync(message.Queue, cancellationToken).ConfigureAwait(false);

        LogQueueDeleted(connection.Id, message.Queue);

        await connection.SendMessageAsync(new ProtocolMessage {
            Id = message.Id,
            Type = CommandType.DeleteQueue,
            Queue = message.Queue,
        }, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Client {clientId} deleted queue {queueName}.")]
    private partial void LogQueueDeleted(string clientId, string queueName);
}
