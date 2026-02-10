using VibeMQ.Protocol;
using VibeMQ.Server.Connections;

namespace VibeMQ.Server.Handlers;

/// <summary>
/// Handles Ping commands by responding with Pong (keep-alive).
/// </summary>
public sealed class PingHandler : ICommandHandler {
    public CommandType CommandType => CommandType.Ping;

    public async Task HandleAsync(
        ClientConnection connection,
        ProtocolMessage message,
        CancellationToken cancellationToken = default
    ) {
        await connection.SendMessageAsync(new ProtocolMessage {
            Id = message.Id,
            Type = CommandType.Pong,
        }, cancellationToken).ConfigureAwait(false);
    }
}
