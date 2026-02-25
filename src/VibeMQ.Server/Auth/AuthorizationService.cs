using VibeMQ.Enums;
using VibeMQ.Server.Connections;

namespace VibeMQ.Server.Auth;

/// <summary>
/// Evaluates per-queue ACL using the permission cache stored in <see cref="ClientConnection"/>.
/// Superusers always pass; otherwise the union of all matching glob patterns is checked.
/// </summary>
public sealed class AuthorizationService : IAuthorizationService {
    /// <inheritdoc />
    public ValueTask<bool> IsAuthorizedAsync(
        ClientConnection connection,
        QueueOperation operation,
        string? queueName,
        CancellationToken cancellationToken = default
    ) {
        if (connection.IsSuperuser) {
            return ValueTask.FromResult(true);
        }

        if (string.IsNullOrEmpty(queueName)) {
            // Operations without a queue name (e.g. ListQueues) are allowed when any
            // permission exists — the handler itself filters the result.
            return ValueTask.FromResult(connection.CachedPermissions.Count > 0);
        }

        foreach (var entry in connection.CachedPermissions) {
            if (GlobMatcher.IsMatch(queueName, entry.QueuePattern)) {
                foreach (var op in entry.Operations) {
                    if (op == operation) {
                        return ValueTask.FromResult(true);
                    }
                }
            }
        }

        return ValueTask.FromResult(false);
    }
}
