using System.Text.Json;
using Microsoft.Extensions.Logging;
using VibeMQ.Interfaces;
using VibeMQ.Protocol;
using VibeMQ.Server.Connections;

namespace VibeMQ.Server.Handlers;

/// <summary>
/// Handles ListQueues commands: returns all queue names on the broker.
/// </summary>
public sealed partial class ListQueuesHandler : ICommandHandler {
    private readonly IQueueManager _queueManager;
    private readonly ILogger<ListQueuesHandler> _logger;

    public ListQueuesHandler(IQueueManager queueManager, ILogger<ListQueuesHandler> logger) {
        _queueManager = queueManager;
        _logger = logger;
    }

    public CommandType CommandType => CommandType.ListQueues;

    public async Task HandleAsync(
        ClientConnection connection,
        ProtocolMessage message,
        CancellationToken cancellationToken = default
    ) {
        var queues = await _queueManager.ListQueuesAsync(cancellationToken).ConfigureAwait(false);

        LogListQueuesRequested(connection.Id, queues.Count);

        await connection.SendMessageAsync(new ProtocolMessage {
            Id = message.Id,
            Type = CommandType.ListQueues,
            Payload = JsonSerializer.SerializeToElement(queues, ProtocolSerializer.Options),
        }, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Client {clientId} listed queues ({count} total).")]
    private partial void LogListQueuesRequested(string clientId, int count);
}
