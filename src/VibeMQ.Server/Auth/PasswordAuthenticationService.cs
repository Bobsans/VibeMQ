using VibeMQ.Interfaces;
using VibeMQ.Server.Auth.Models;

namespace VibeMQ.Server.Auth;

/// <summary>
/// Username/password authentication service backed by the SQLite auth repository.
/// BCrypt verification is performed once per login; subsequent checks use the
/// per-session permission cache stored in <see cref="Connections.ClientConnection"/>.
/// </summary>
public sealed class PasswordAuthenticationService(IAuthRepository repository) : IAuthenticationService, IPasswordAuthenticationService {
    /// <inheritdoc />
    public Task<AuthResult?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default) {
        return AuthenticateInternalAsync(username, password, cancellationToken);
    }

    private async Task<AuthResult?> AuthenticateInternalAsync(string username, string password, CancellationToken cancellationToken) {
        var user = await repository.FindUserAsync(username, cancellationToken).ConfigureAwait(false);
        if (user is null) {
            return null;
        }

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)) {
            return null;
        }

        var permissions = await repository.GetPermissionsAsync(username, cancellationToken).ConfigureAwait(false);
        return new AuthResult(user.Username, user.IsSuperuser, permissions);
    }

    // Legacy stub — password auth service does not support bare tokens.
    [Obsolete("Use AuthenticateAsync(username, password) instead.")]
    Task<bool> IAuthenticationService.AuthenticateAsync(string token, CancellationToken cancellationToken) {
        return Task.FromResult(false);
    }
}
