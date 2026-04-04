using System.Text.Json;
using Microsoft.Extensions.Logging;
using VibeMQ.Enums;
using VibeMQ.Protocol;
using VibeMQ.Server.Auth;
using VibeMQ.Server.Connections;

namespace VibeMQ.Server.Handlers.Admin;

/// <summary>
/// Admin command: grants a user permission on a queue pattern. Superuser-only.
/// Payload: { "username": "...", "queuePattern": "...", "operations": ["Publish", "Subscribe", ...] }
/// </summary>
public sealed partial class GrantPermissionHandler(IAuthRepository repository, ILogger<GrantPermissionHandler> logger) : ICommandHandler {
    private readonly ILogger<GrantPermissionHandler> _logger = logger;

    public CommandType CommandType => CommandType.AdminGrantPermission;

    public async Task HandleAsync(ClientConnection connection, ProtocolMessage message, CancellationToken cancellationToken = default) {
        if (!connection.IsSuperuser) {
            await connection.SendErrorAsync(message.Id, "NOT_AUTHORIZED", "Superuser access required.", cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!message.Payload.HasValue) {
            await connection.SendErrorAsync(message.Id, "INVALID_PAYLOAD", "Payload required: { username, queuePattern, operations }.", cancellationToken).ConfigureAwait(false);
            return;
        }

        var payload = message.Payload.Value;
        var username = payload.GetProperty("username").GetString();
        var queuePattern = payload.GetProperty("queuePattern").GetString();
        var opsArray = payload.GetProperty("operations").Deserialize<string[]>(ProtocolSerializer.Options) ?? [];

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(queuePattern) || opsArray.Length == 0) {
            await connection.SendErrorAsync(message.Id, "INVALID_PAYLOAD", "username, queuePattern, and operations are required.", cancellationToken).ConfigureAwait(false);
            return;
        }

        var user = await repository.FindUserAsync(username, cancellationToken).ConfigureAwait(false);
        if (user is null) {
            await connection.SendErrorAsync(message.Id, "USER_NOT_FOUND", $"User '{username}' not found.", cancellationToken).ConfigureAwait(false);
            return;
        }

        var ops = opsArray
            .Where(s => Enum.TryParse<QueueOperation>(s, true, out _))
            .Select(s => Enum.Parse<QueueOperation>(s, true))
            .Distinct()
            .ToArray();

        if (ops.Length == 0) {
            await connection.SendErrorAsync(message.Id, "INVALID_PAYLOAD", "No valid operations specified.", cancellationToken).ConfigureAwait(false);
            return;
        }

        await repository.GrantPermissionAsync(username, queuePattern, ops, cancellationToken).ConfigureAwait(false);

        LogPermissionGranted(connection.Username ?? "<superuser>", username, queuePattern);

        await connection.SendMessageAsync(new ProtocolMessage {
            Id = message.Id,
            Type = CommandType.AdminGrantPermission,
            Payload = JsonSerializer.SerializeToElement(new { username, queuePattern, operations = ops.Select(o => o.ToString()) }, ProtocolSerializer.Options)
        }, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Permission granted to '{username}' on '{pattern}' by '{actor}'.")]
    private partial void LogPermissionGranted(string actor, string username, string pattern);
}
