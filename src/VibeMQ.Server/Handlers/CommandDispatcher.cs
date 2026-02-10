using Microsoft.Extensions.Logging;
using VibeMQ.Protocol;
using VibeMQ.Server.Connections;

namespace VibeMQ.Server.Handlers;

/// <summary>
/// Routes incoming protocol messages to the appropriate <see cref="ICommandHandler"/>.
/// </summary>
public sealed partial class CommandDispatcher {
    private readonly Dictionary<CommandType, ICommandHandler> _handlers;
    private readonly ILogger<CommandDispatcher> _logger;

    public CommandDispatcher(IEnumerable<ICommandHandler> handlers, ILogger<CommandDispatcher> logger) {
        _handlers = handlers.ToDictionary(h => h.CommandType);
        _logger = logger;
    }

    /// <summary>
    /// Dispatches a message to the registered handler for its command type.
    /// Sends an error response if no handler is found.
    /// </summary>
    public async Task DispatchAsync(
        ClientConnection connection,
        ProtocolMessage message,
        CancellationToken cancellationToken = default
    ) {
        if (_handlers.TryGetValue(message.Type, out var handler)) {
            LogDispatching(message.Type, message.Id, connection.Id);
            await handler.HandleAsync(connection, message, cancellationToken).ConfigureAwait(false);
        } else {
            LogUnknownCommand(message.Type, connection.Id);
            await connection.SendErrorAsync("UNKNOWN_COMMAND", $"Unknown command type: {message.Type}", cancellationToken)
                .ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Dispatching {commandType} (msg: {messageId}) from client {clientId}.")]
    private partial void LogDispatching(CommandType commandType, string messageId, string clientId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unknown command type {commandType} from client {clientId}.")]
    private partial void LogUnknownCommand(CommandType commandType, string clientId);
}
