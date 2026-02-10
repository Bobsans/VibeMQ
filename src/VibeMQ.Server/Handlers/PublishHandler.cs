using System.Text.Json;
using Microsoft.Extensions.Logging;
using VibeMQ.Core.Enums;
using VibeMQ.Core.Interfaces;
using VibeMQ.Core.Models;
using VibeMQ.Protocol;
using VibeMQ.Server.Connections;

namespace VibeMQ.Server.Handlers;

/// <summary>
/// Handles Publish commands: validates, stores the message, and routes it to subscribers.
/// </summary>
public sealed partial class PublishHandler : ICommandHandler {
    private readonly IQueueManager _queueManager;
    private readonly ILogger<PublishHandler> _logger;

    public PublishHandler(IQueueManager queueManager, ILogger<PublishHandler> logger) {
        _queueManager = queueManager;
        _logger = logger;
    }

    public CommandType CommandType => CommandType.Publish;

    public async Task HandleAsync(
        ClientConnection connection,
        ProtocolMessage message,
        CancellationToken cancellationToken = default
    ) {
        if (string.IsNullOrEmpty(message.Queue)) {
            await connection.SendErrorAsync("INVALID_QUEUE", "Queue name is required for publish.", cancellationToken)
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
