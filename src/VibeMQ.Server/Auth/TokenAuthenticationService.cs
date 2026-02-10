using VibeMQ.Core.Interfaces;

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
    public Task<bool> AuthenticateAsync(string token, CancellationToken cancellationToken = default) {
        var isValid = string.Equals(_expectedToken, token, StringComparison.Ordinal);
        return Task.FromResult(isValid);
    }
}
