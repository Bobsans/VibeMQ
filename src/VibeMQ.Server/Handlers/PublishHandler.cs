using System.Text.Json;
using Microsoft.Extensions.Logging;
using VibeMQ.Enums;
using VibeMQ.Interfaces;
using VibeMQ.Models;
using VibeMQ.Protocol;
using VibeMQ.Server.Auth;
using VibeMQ.Server.Connections;

namespace VibeMQ.Server.Handlers;

/// <summary>
/// Handles Publish commands: validates, stores the message, and routes it to subscribers.
/// </summary>
public sealed partial class PublishHandler : ICommandHandler {
    private readonly IQueueManager _queueManager;
    private readonly IAuthorizationService? _authz;
    private readonly ILogger<PublishHandler> _logger;

    public PublishHandler(IQueueManager queueManager, IAuthorizationService? authz, ILogger<PublishHandler> logger) {
        _queueManager = queueManager;
        _authz = authz;
        _logger = logger;
    }

    public CommandType CommandType => CommandType.Publish;

    public async Task HandleAsync(
        ClientConnection connection,
        ProtocolMessage message,
        CancellationToken cancellationToken = default
    ) {
        if (string.IsNullOrEmpty(message.Queue)) {
            await connection.SendErrorAsync(message.Id, "INVALID_QUEUE", "Queue name is required for publish.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (_authz is not null && !await _authz.IsAuthorizedAsync(connection, QueueOperation.Publish, message.Queue, cancellationToken).ConfigureAwait(false)) {
            await connection.SendErrorAsync(message.Id, "NOT_AUTHORIZED", "Access denied.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var priority = MessagePriority.Normal;
        if (message.Headers?.TryGetValue("priority", out var priorityStr) == true) {
            Enum.TryParse(priorityStr, true, out priority);
        }

        var brokerMessage = new BrokerMessage {
            Id = message.Id,
            QueueName = message.Queue,
            Payload = message.Payload ?? default(JsonElement),
            Headers = message.Headers ?? [],
            Priority = priority,
        };

        await _queueManager.PublishAsync(brokerMessage, cancellationToken).ConfigureAwait(false);

        LogMessagePublished(message.Id, message.Queue);

        await connection.SendMessageAsync(new ProtocolMessage {
            Id = message.Id,
            Type = CommandType.PublishAck,
            Queue = message.Queue,
        }, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Message {messageId} published to queue {queueName}.")]
    private partial void LogMessagePublished(string messageId, string queueName);
}
