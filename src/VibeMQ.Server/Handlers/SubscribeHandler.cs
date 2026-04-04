using Microsoft.Extensions.Logging;
using VibeMQ.Enums;
using VibeMQ.Interfaces;
using VibeMQ.Protocol;
using VibeMQ.Server.Auth;
using VibeMQ.Server.Connections;

namespace VibeMQ.Server.Handlers;

/// <summary>
/// Handles Subscribe commands: registers the client as a subscriber to a queue.
/// </summary>
public sealed partial class SubscribeHandler(IQueueManager queueManager, IAuthorizationService? authz, ILogger<SubscribeHandler> logger) : ICommandHandler {
    private readonly ILogger<SubscribeHandler> _logger = logger;

    public CommandType CommandType => CommandType.Subscribe;

    public async Task HandleAsync(
        ClientConnection connection,
        ProtocolMessage message,
        CancellationToken cancellationToken = default
    ) {
        if (string.IsNullOrEmpty(message.Queue)) {
            await connection.SendErrorAsync(message.Id, "INVALID_QUEUE", "Queue name is required for subscribe.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (authz is not null && !await authz.IsAuthorizedAsync(connection, QueueOperation.Subscribe, message.Queue).ConfigureAwait(false)) {
            await connection.SendErrorAsync(message.Id, "NOT_AUTHORIZED", "Access denied.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        // Ensure the queue exists (auto-create if enabled)
        var queueInfo = await queueManager.GetQueueInfoAsync(message.Queue, cancellationToken).ConfigureAwait(false);

        if (queueInfo is null) {
            await queueManager.CreateQueueAsync(message.Queue, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        connection.Subscriptions.TryAdd(message.Queue, 0);
        LogSubscribed(connection.Id, message.Queue);

        await connection.SendMessageAsync(new ProtocolMessage {
            Id = message.Id,
            Type = CommandType.SubscribeAck,
            Queue = message.Queue
        }, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Client {clientId} subscribed to queue {queueName}.")]
    private partial void LogSubscribed(string clientId, string queueName);
}
