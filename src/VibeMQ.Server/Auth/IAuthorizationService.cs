using VibeMQ.Enums;
using VibeMQ.Server.Connections;

namespace VibeMQ.Server.Auth;

/// <summary>
/// Per-queue authorization service. Checks whether a connected client has the
/// required operation on the given queue using cached ACL data from their session.
/// </summary>
public interface IAuthorizationService {
    /// <summary>
    /// Returns <see langword="true"/> if the connection is allowed to perform
    /// <paramref name="operation"/> on <paramref name="queueName"/>.
    /// Superusers always return <see langword="true"/>.
    /// </summary>
    ValueTask<bool> IsAuthorizedAsync(
        ClientConnection connection,
        QueueOperation operation,
        string? queueName,
        CancellationToken cancellationToken = default
    );
}
