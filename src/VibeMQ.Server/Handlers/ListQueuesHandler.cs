using System.Text.Json;
using Microsoft.Extensions.Logging;
using VibeMQ.Interfaces;
using VibeMQ.Protocol;
using VibeMQ.Server.Auth;
using VibeMQ.Server.Connections;

namespace VibeMQ.Server.Handlers;

/// <summary>
/// Handles ListQueues commands: returns all queue names the client is allowed to see.
/// Superusers see all queues; regular users see only queues matched by their permission patterns.
/// </summary>
public sealed partial class ListQueuesHandler : ICommandHandler {
    private readonly IQueueManager _queueManager;
    private readonly IAuthorizationService? _authz;
    private readonly ILogger<ListQueuesHandler> _logger;

    public ListQueuesHandler(IQueueManager queueManager, IAuthorizationService? authz, ILogger<ListQueuesHandler> logger) {
        _queueManager = queueManager;
        _authz = authz;
        _logger = logger;
    }

    public CommandType CommandType => CommandType.ListQueues;

    public async Task HandleAsync(
        ClientConnection connection,
        ProtocolMessage message,
        CancellationToken cancellationToken = default
    ) {
        var queues = await _queueManager.ListQueuesAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<string> visible;

        if (_authz is null || connection.IsSuperuser) {
            // No authorization or superuser — return all queues
            visible = queues;
        } else {
            // Filter: keep only queues matched by at least one permission pattern
            var filtered = new List<string>();
            foreach (var queueName in queues) {
                foreach (var entry in connection.CachedPermissions) {
                    if (GlobMatcher.IsMatch(queueName, entry.QueuePattern)) {
                        filtered.Add(queueName);
                        break;
                    }
                }
            }

            visible = filtered;
        }

        LogListQueuesRequested(connection.Id, visible.Count);

        await connection.SendMessageAsync(new ProtocolMessage {
            Id = message.Id,
            Type = CommandType.ListQueues,
            Payload = JsonSerializer.SerializeToElement(visible, ProtocolSerializer.Options),
        }, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Client {clientId} listed queues ({count} visible).")]
    private partial void LogListQueuesRequested(string clientId, int count);
}
