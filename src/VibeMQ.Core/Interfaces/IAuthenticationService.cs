using VibeMQ.Auth;

namespace VibeMQ.Interfaces;

/// <summary>
/// Authentication service interface. Supports both legacy token-based and
/// username/password-based authentication.
/// </summary>
public interface IAuthenticationService {
    /// <summary>
    /// Returns true if the token is valid. Used by legacy token-based authentication.
    /// </summary>
    [Obsolete("Use AuthenticateAsync(username, password) for new deployments.")]
    Task<bool> AuthenticateAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Authenticates a user by username and password.
    /// Returns an <see cref="AuthResult"/> on success, or <see langword="null"/> on failure.
    /// </summary>
    Task<AuthResult?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default);
}
