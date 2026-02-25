using System.Text.Json;
using Microsoft.Extensions.Logging;
using VibeMQ.Protocol;
using VibeMQ.Server.Auth;
using VibeMQ.Server.Connections;

namespace VibeMQ.Server.Handlers.Admin;

/// <summary>
/// Admin command: returns the permissions for a specific user. Superuser-only.
/// Payload: { "username": "..." }
/// Returns: [{ queuePattern, operations }]
/// </summary>
public sealed partial class GetUserPermissionsHandler : ICommandHandler {
    private readonly IAuthRepository _repository;
    private readonly ILogger<GetUserPermissionsHandler> _logger;

    public GetUserPermissionsHandler(IAuthRepository repository, ILogger<GetUserPermissionsHandler> logger) {
        _repository = repository;
        _logger = logger;
    }

    public CommandType CommandType => CommandType.AdminGetUserPermissions;

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

        var permissions = await _repository.GetPermissionsAsync(username, cancellationToken).ConfigureAwait(false);

        var result = permissions.Select(p => new {
            queuePattern = p.QueuePattern,
            operations = p.Operations.Select(o => o.ToString()),
        });

        await connection.SendMessageAsync(new ProtocolMessage {
            Id = message.Id,
            Type = CommandType.AdminGetUserPermissions,
            Payload = JsonSerializer.SerializeToElement(result, ProtocolSerializer.Options),
        }, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "'{actor}' queried permissions for '{username}'.")]
    private partial void LogGetPermissions(string actor, string username);
}
