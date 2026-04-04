using System.Text.Json;
using Microsoft.Extensions.Logging;
using VibeMQ.Protocol;
using VibeMQ.Server.Auth;
using VibeMQ.Server.Connections;

namespace VibeMQ.Server.Handlers.Admin;

/// <summary>
/// Admin command: returns the list of all users. Superuser-only.
/// Returns: [{ username, isSuperuser, createdAt }]
/// </summary>
public sealed partial class ListUsersHandler(IAuthRepository repository, ILogger<ListUsersHandler> logger) : ICommandHandler {
    private readonly ILogger<ListUsersHandler> _logger = logger;

    public CommandType CommandType => CommandType.AdminListUsers;

    public async Task HandleAsync(ClientConnection connection, ProtocolMessage message, CancellationToken cancellationToken = default) {
        if (!connection.IsSuperuser) {
            await connection.SendErrorAsync(message.Id, "NOT_AUTHORIZED", "Superuser access required.", cancellationToken).ConfigureAwait(false);
            return;
        }

        var users = await repository.ListUsersAsync(cancellationToken).ConfigureAwait(false);

        var result = users.Select(u => new {
            username = u.Username,
            isSuperuser = u.IsSuperuser,
            createdAt = u.CreatedAt
        });

        LogListUsers(connection.Username ?? "<superuser>", users.Count);

        await connection.SendMessageAsync(new ProtocolMessage {
            Id = message.Id,
            Type = CommandType.AdminListUsers,
            Payload = JsonSerializer.SerializeToElement(result, ProtocolSerializer.Options)
        }, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "'{actor}' listed users ({count} total).")]
    private partial void LogListUsers(string actor, int count);
}
