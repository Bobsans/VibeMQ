using System.Text.Json;
using Microsoft.Extensions.Logging;
using VibeMQ.Protocol;
using VibeMQ.Server.Auth;
using VibeMQ.Server.Connections;

namespace VibeMQ.Server.Handlers.Admin;

/// <summary>
/// Admin command: deletes a user. Superuser-only. Cannot delete other superusers (lockout protection).
/// Payload: { "username": "..." }
/// </summary>
public sealed partial class DeleteUserHandler(IAuthRepository repository, ILogger<DeleteUserHandler> logger) : ICommandHandler {
    private readonly ILogger<DeleteUserHandler> _logger = logger;

    public CommandType CommandType => CommandType.AdminDeleteUser;

    public async Task HandleAsync(ClientConnection connection, ProtocolMessage message, CancellationToken cancellationToken = default) {
        if (!connection.IsSuperuser) {
            await connection.SendErrorAsync(message.Id, "NOT_AUTHORIZED", "Superuser access required.", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!message.Payload.HasValue) {
            await connection.SendErrorAsync(message.Id, "INVALID_PAYLOAD", "Payload required: { username }.", cancellationToken).ConfigureAwait(false);
            return;
        }

        var username = message.Payload.Value.GetProperty("username").GetString();
        if (string.IsNullOrWhiteSpace(username)) {
            await connection.SendErrorAsync(message.Id, "INVALID_PAYLOAD", "username is required.", cancellationToken).ConfigureAwait(false);
            return;
        }

        var target = await repository.FindUserAsync(username, cancellationToken).ConfigureAwait(false);
        if (target is null) {
            await connection.SendErrorAsync(message.Id, "USER_NOT_FOUND", $"User '{username}' not found.", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (target.IsSuperuser) {
            await connection.SendErrorAsync(message.Id, "FORBIDDEN", "Cannot delete a superuser account.", cancellationToken).ConfigureAwait(false);
            return;
        }

        await repository.DeleteUserAsync(username, cancellationToken).ConfigureAwait(false);

        LogUserDeleted(connection.Username ?? "<superuser>", username);

        await connection.SendMessageAsync(new ProtocolMessage {
            Id = message.Id,
            Type = CommandType.AdminDeleteUser,
            Payload = JsonSerializer.SerializeToElement(new { username }, ProtocolSerializer.Options),
        }, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "User '{username}' deleted by '{actor}'.")]
    private partial void LogUserDeleted(string actor, string username);
}
