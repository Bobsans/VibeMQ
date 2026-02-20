using Microsoft.Extensions.Logging;
using VibeMQ.Interfaces;
using VibeMQ.Protocol;
using VibeMQ.Server.Connections;

namespace VibeMQ.Server.Handlers;

/// <summary>
/// Handles Subscribe commands: registers the client as a subscriber to a queue.
/// </summary>
public sealed partial class SubscribeHandler : ICommandHandler {
    private readonly IQueueManager _queueManager;
    private readonly ILogger<SubscribeHandler> _logger;

    public SubscribeHandler(IQueueManager queueManager, ILogger<SubscribeHandler> logger) {
        _queueManager = queueManager;
        _logger = logger;
    }

    public CommandType CommandType => CommandType.Subscribe;

    public async Task HandleAsync(
        ClientConnection connection,
        ProtocolMessage message,
        CancellationToken cancellationToken = default
    ) {
        if (string.IsNullOrEmpty(message.Queue)) {
            await connection.SendErrorAsync("INVALID_QUEUE", "Queue name is required for subscribe.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        // Ensure the queue exists (auto-create if enabled)
        var queueInfo = await _queueManager.GetQueueInfoAsync(message.Queue, cancellationToken).ConfigureAwait(false);

        if (queueInfo is null) {
            await _queueManager.CreateQueueAsync(message.Queue, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        connection.Subscriptions.Add(message.Queue);
        LogSubscribed(connection.Id, message.Queue);

        await connection.SendMessageAsync(new ProtocolMessage {
            Id = message.Id,
            Type = CommandType.SubscribeAck,
            Queue = message.Queue,
        }, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Client {clientId} subscribed to queue {queueName}.")]
    private partial void LogSubscribed(string clientId, string queueName);
}
