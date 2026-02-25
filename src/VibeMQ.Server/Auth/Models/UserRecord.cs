namespace VibeMQ.Server.Auth.Models;

/// <summary>
/// Represents a user record stored in the auth database.
/// </summary>
public sealed class UserRecord {
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool IsSuperuser { get; set; }
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }
}
