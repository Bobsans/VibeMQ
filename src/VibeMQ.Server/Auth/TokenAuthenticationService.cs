using VibeMQ.Interfaces;
using VibeMQ.Server.Auth.Models;

namespace VibeMQ.Server.Auth;

/// <summary>
/// Simple token-based authentication: compares the client token with the configured server token.
/// </summary>
public sealed class TokenAuthenticationService : IAuthenticationService {
    private readonly string _expectedToken;

    public TokenAuthenticationService(string expectedToken) {
        _expectedToken = expectedToken;
    }

    /// <inheritdoc />
    [Obsolete("Use AuthenticateAsync(username, password) for new deployments.")]
    public Task<bool> AuthenticateAsync(string token, CancellationToken cancellationToken = default) {
        var isValid = string.Equals(_expectedToken, token, StringComparison.Ordinal);
        return Task.FromResult(isValid);
    }

    // Token-based service does not support username/password authentication.
    Task<AuthResult?> IAuthenticationService.AuthenticateAsync(string username, string password, CancellationToken cancellationToken) {
        return Task.FromResult<AuthResult?>(null);
    }
}
