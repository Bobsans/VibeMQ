namespace VibeMQ.Interfaces;

/// <summary>
/// Simple token-based authentication service.
/// Validates provided token against the configured server token.
/// </summary>
public interface IAuthenticationService {
    /// <summary>
    /// Returns true if the token is valid.
    /// </summary>
    Task<bool> AuthenticateAsync(string token, CancellationToken cancellationToken = default);
}
