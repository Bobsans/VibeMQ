using System.Text.Json;
using Microsoft.Extensions.Logging;
using VibeMQ.Protocol;
using VibeMQ.Server.Auth;
using VibeMQ.Server.Auth.Models;
using VibeMQ.Server.Connections;

namespace VibeMQ.Server.Handlers.Admin;

/// <summary>
/// Admin command: creates a new user. Superuser-only.
/// Payload: { "username": "...", "password": "..." }
/// </summary>
public sealed partial class CreateUserHandler(IAuthRepository repository, ILogger<CreateUserHandler> logger) : ICommandHandler {
    private readonly ILogger<CreateUserHandler> _logger = logger;

    public CommandType CommandType => CommandType.AdminCreateUser;

    public async Task HandleAsync(ClientConnection connection, ProtocolMessage message, CancellationToken cancellationToken = default) {
        if (!connection.IsSuperuser) {
            await connection.SendErrorAsync(message.Id, "NOT_AUTHORIZED", "Superuser access required.", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!message.Payload.HasValue) {
            await connection.SendErrorAsync(message.Id, "INVALID_PAYLOAD", "Payload required: { username, password }.", cancellationToken).ConfigureAwait(false);
            return;
        }

        var payload = message.Payload.Value;
        var username = payload.GetProperty("username").GetString();
        var password = payload.GetProperty("password").GetString();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password)) {
            await connection.SendErrorAsync(message.Id, "INVALID_PAYLOAD", "username and password are required.", cancellationToken).ConfigureAwait(false);
            return;
        }

        var existing = await repository.FindUserAsync(username, cancellationToken).ConfigureAwait(false);
        if (existing is not null) {
            await connection.SendErrorAsync(message.Id, "USER_EXISTS", $"User '{username}' already exists.", cancellationToken).ConfigureAwait(false);
            return;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var record = new UserRecord {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12),
            IsSuperuser = false,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await repository.CreateUserAsync(record, cancellationToken).ConfigureAwait(false);

        LogUserCreated(connection.Username ?? "<superuser>", username);

        await connection.SendMessageAsync(new ProtocolMessage {
            Id = message.Id,
            Type = CommandType.AdminCreateUser,
            Payload = JsonSerializer.SerializeToElement(new { username }, ProtocolSerializer.Options),
        }, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "User '{username}' created by '{actor}'.")]
    private partial void LogUserCreated(string actor, string username);
}
