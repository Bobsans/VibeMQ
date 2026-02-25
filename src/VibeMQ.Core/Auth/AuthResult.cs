namespace VibeMQ.Auth;

/// <summary>
/// Result of a successful username/password authentication.
/// Contains the resolved identity and a session-level permission cache.
/// </summary>
public sealed record AuthResult(
    string Username,
    bool IsSuperuser,
    IReadOnlyList<PermissionEntry> Permissions
);
