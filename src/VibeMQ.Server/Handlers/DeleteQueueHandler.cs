using Microsoft.Extensions.Logging;
using VibeMQ.Enums;
using VibeMQ.Interfaces;
using VibeMQ.Protocol;
using VibeMQ.Server.Auth;
using VibeMQ.Server.Connections;

namespace VibeMQ.Server.Handlers;

/// <summary>
/// Handles DeleteQueue commands: removes a queue by name.
/// </summary>
public sealed partial class DeleteQueueHandler : ICommandHandler {
    private readonly IQueueManager _queueManager;
    private readonly IAuthorizationService? _authz;
    private readonly ILogger<DeleteQueueHandler> _logger;

    public DeleteQueueHandler(IQueueManager queueManager, IAuthorizationService? authz, ILogger<DeleteQueueHandler> logger) {
        _queueManager = queueManager;
        _authz = authz;
        _logger = logger;
    }

    public CommandType CommandType => CommandType.DeleteQueue;

    public async Task HandleAsync(
        ClientConnection connection,
        ProtocolMessage message,
        CancellationToken cancellationToken = default
    ) {
        if (string.IsNullOrEmpty(message.Queue)) {
            await connection.SendErrorAsync(message.Id, "INVALID_QUEUE", "Queue name is required for DeleteQueue.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (_authz is not null && !await _authz.IsAuthorizedAsync(connection, QueueOperation.DeleteQueue, message.Queue, cancellationToken).ConfigureAwait(false)) {
            await connection.SendErrorAsync(message.Id, "NOT_AUTHORIZED", "Access denied.", cancellationToken)
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
