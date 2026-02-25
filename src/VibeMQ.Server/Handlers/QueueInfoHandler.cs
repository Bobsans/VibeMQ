using System.Text.Json;
using Microsoft.Extensions.Logging;
using VibeMQ.Enums;
using VibeMQ.Interfaces;
using VibeMQ.Protocol;
using VibeMQ.Server.Auth;
using VibeMQ.Server.Connections;

namespace VibeMQ.Server.Handlers;

/// <summary>
/// Handles QueueInfo commands: returns metadata about a specific queue.
/// </summary>
public sealed partial class QueueInfoHandler : ICommandHandler {
    private readonly IQueueManager _queueManager;
    private readonly IAuthorizationService? _authz;
    private readonly ILogger<QueueInfoHandler> _logger;

    public QueueInfoHandler(IQueueManager queueManager, IAuthorizationService? authz, ILogger<QueueInfoHandler> logger) {
        _queueManager = queueManager;
        _authz = authz;
        _logger = logger;
    }

    public CommandType CommandType => CommandType.QueueInfo;

    public async Task HandleAsync(
        ClientConnection connection,
        ProtocolMessage message,
        CancellationToken cancellationToken = default
    ) {
        if (string.IsNullOrEmpty(message.Queue)) {
            await connection.SendErrorAsync(message.Id, "INVALID_QUEUE", "Queue name is required for QueueInfo.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (_authz is not null && !await _authz.IsAuthorizedAsync(connection, QueueOperation.GetQueueInfo, message.Queue, cancellationToken).ConfigureAwait(false)) {
            await connection.SendErrorAsync(message.Id, "NOT_AUTHORIZED", "Access denied.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var info = await _queueManager.GetQueueInfoAsync(message.Queue, cancellationToken).ConfigureAwait(false);

        LogQueueInfoRequested(connection.Id, message.Queue);

        var response = new ProtocolMessage {
            Id = message.Id,
            Type = CommandType.QueueInfo,
            Queue = message.Queue,
        };

        if (info is not null) {
            response.Payload = JsonSerializer.SerializeToElement(info, ProtocolSerializer.Options);
        }

        await connection.SendMessageAsync(response, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Client {clientId} requested info for queue {queueName}.")]
    private partial void LogQueueInfoRequested(string clientId, string queueName);
}
