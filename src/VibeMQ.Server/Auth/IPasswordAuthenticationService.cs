namespace VibeMQ.Server.Auth;

/// <summary>
/// Username/password authentication service used when authorization is enabled.
/// Returns a populated <see cref="AuthResult"/> on success (including cached permissions),
/// or <see langword="null"/> on failure.
/// </summary>
public interface IPasswordAuthenticationService {
    Task<AuthResult?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default);
}
