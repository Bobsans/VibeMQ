using System.Text.Json;
using Microsoft.Extensions.Logging;
using VibeMQ.Protocol;
using VibeMQ.Server.Auth;
using VibeMQ.Server.Connections;

namespace VibeMQ.Server.Handlers.Admin;

/// <summary>
/// Admin command: changes a user's password.
/// Superuser can change any user's password.
/// Regular users can only change their own password.
/// Payload: { "username": "...", "newPassword": "..." }
/// </summary>
public sealed partial class ChangePasswordHandler(IAuthRepository repository, ILogger<ChangePasswordHandler> logger) : ICommandHandler {
    private readonly ILogger<ChangePasswordHandler> _logger = logger;

    public CommandType CommandType => CommandType.AdminChangePassword;

    public async Task HandleAsync(ClientConnection connection, ProtocolMessage message, CancellationToken cancellationToken = default) {
        if (!message.Payload.HasValue) {
            await connection.SendErrorAsync(message.Id, "INVALID_PAYLOAD", "Payload required: { username, newPassword }.", cancellationToken).ConfigureAwait(false);
            return;
        }

        var payload = message.Payload.Value;
        var username = payload.GetProperty("username").GetString();
        var newPassword = payload.GetProperty("newPassword").GetString();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(newPassword)) {
            await connection.SendErrorAsync(message.Id, "INVALID_PAYLOAD", "username and newPassword are required.", cancellationToken).ConfigureAwait(false);
            return;
        }

        // Regular users can only change their own password
        if (!connection.IsSuperuser &&
            (connection.Username is null || !string.Equals(username, connection.Username, StringComparison.OrdinalIgnoreCase))) {
            await connection.SendErrorAsync(message.Id, "NOT_AUTHORIZED", "You may only change your own password.", cancellationToken).ConfigureAwait(false);
            return;
        }

        var target = await repository.FindUserAsync(username, cancellationToken).ConfigureAwait(false);
        if (target is null) {
            await connection.SendErrorAsync(message.Id, "USER_NOT_FOUND", $"User '{username}' not found.", cancellationToken).ConfigureAwait(false);
            return;
        }

        var hash = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 12);
        await repository.UpdatePasswordHashAsync(username, hash, cancellationToken).ConfigureAwait(false);

        LogPasswordChanged(connection.Username ?? "<actor>", username);

        await connection.SendMessageAsync(new ProtocolMessage {
            Id = message.Id,
            Type = CommandType.AdminChangePassword,
            Payload = JsonSerializer.SerializeToElement(new { username }, ProtocolSerializer.Options)
        }, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Password changed for '{username}' by '{actor}'.")]
    private partial void LogPasswordChanged(string actor, string username);
}
