using System.Text.Json;
using Microsoft.Extensions.Logging;
using VibeMQ.Configuration;
using VibeMQ.Enums;
using VibeMQ.Interfaces;
using VibeMQ.Protocol;
using VibeMQ.Server.Auth;
using VibeMQ.Server.Connections;

namespace VibeMQ.Server.Handlers;

/// <summary>
/// Handles CreateQueue commands: creates a new queue with optional configuration.
/// </summary>
public sealed partial class CreateQueueHandler(IQueueManager queueManager, IAuthorizationService? authz, ILogger<CreateQueueHandler> logger) : ICommandHandler {
    private readonly ILogger<CreateQueueHandler> _logger = logger;

    public CommandType CommandType => CommandType.CreateQueue;

    public async Task HandleAsync(
        ClientConnection connection,
        ProtocolMessage message,
        CancellationToken cancellationToken = default
    ) {
        if (string.IsNullOrEmpty(message.Queue)) {
            await connection.SendErrorAsync(message.Id, "INVALID_QUEUE", "Queue name is required for CreateQueue.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (authz is not null && !await authz.IsAuthorizedAsync(connection, QueueOperation.CreateQueue, message.Queue, cancellationToken).ConfigureAwait(false)) {
            await connection.SendErrorAsync(message.Id, "NOT_AUTHORIZED", "Access denied.", cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        QueueOptions? options = null;
        if (message.Payload.HasValue) {
            options = message.Payload.Value.Deserialize<QueueOptions>(ProtocolSerializer.Options);
        }

        await queueManager.CreateQueueAsync(message.Queue, options, cancellationToken).ConfigureAwait(false);

        LogQueueCreated(connection.Id, message.Queue);

        await connection.SendMessageAsync(new ProtocolMessage {
            Id = message.Id,
            Type = CommandType.CreateQueue,
            Queue = message.Queue,
        }, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Client {clientId} created queue {queueName}.")]
    private partial void LogQueueCreated(string clientId, string queueName);
}
