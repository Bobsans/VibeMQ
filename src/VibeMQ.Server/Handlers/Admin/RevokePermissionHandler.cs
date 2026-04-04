using System.Text.Json;
using Microsoft.Extensions.Logging;
using VibeMQ.Protocol;
using VibeMQ.Server.Auth;
using VibeMQ.Server.Connections;

namespace VibeMQ.Server.Handlers.Admin;

/// <summary>
/// Admin command: revokes a user's permission on a queue pattern. Superuser-only.
/// Payload: { "username": "...", "queuePattern": "..." }
/// </summary>
public sealed partial class RevokePermissionHandler(IAuthRepository repository, ILogger<RevokePermissionHandler> logger) : ICommandHandler {
    private readonly ILogger<RevokePermissionHandler> _logger = logger;

    public CommandType CommandType => CommandType.AdminRevokePermission;

    public async Task HandleAsync(ClientConnection connection, ProtocolMessage message, CancellationToken cancellationToken = default) {
        if (!connection.IsSuperuser) {
            await connection.SendErrorAsync(message.Id, "NOT_AUTHORIZED", "Superuser access required.", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!message.Payload.HasValue) {
            await connection.SendErrorAsync(message.Id, "INVALID_PAYLOAD", "Payload required: { username, queuePattern }.", cancellationToken).ConfigureAwait(false);
            return;
        }

        var payload = message.Payload.Value;
        var username = payload.GetProperty("username").GetString();
        var queuePattern = payload.GetProperty("queuePattern").GetString();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(queuePattern)) {
            await connection.SendErrorAsync(message.Id, "INVALID_PAYLOAD", "username and queuePattern are required.", cancellationToken).ConfigureAwait(false);
            return;
        }

        await repository.RevokePermissionAsync(username, queuePattern, cancellationToken).ConfigureAwait(false);

        LogPermissionRevoked(connection.Username ?? "<superuser>", username, queuePattern);

        await connection.SendMessageAsync(new ProtocolMessage {
            Id = message.Id,
            Type = CommandType.AdminRevokePermission,
            Payload = JsonSerializer.SerializeToElement(new { username, queuePattern }, ProtocolSerializer.Options)
        }, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Permission revoked from '{username}' on '{pattern}' by '{actor}'.")]
    private partial void LogPermissionRevoked(string actor, string username, string pattern);
}
