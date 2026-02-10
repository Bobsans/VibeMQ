using VibeMQ.Protocol;
using VibeMQ.Server.Connections;

namespace VibeMQ.Server.Handlers;

/// <summary>
/// Handles a specific protocol command type.
/// </summary>
public interface ICommandHandler {
    /// <summary>
    /// The command type this handler is responsible for.
    /// </summary>
    CommandType CommandType { get; }

    /// <summary>
    /// Processes the incoming message from the given client connection.
    /// </summary>
    Task HandleAsync(ClientConnection connection, ProtocolMessage message, CancellationToken cancellationToken = default);
}
