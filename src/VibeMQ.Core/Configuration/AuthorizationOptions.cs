namespace VibeMQ.Configuration;

/// <summary>
/// Configuration options for the username/password authorization system.
/// </summary>
public sealed class AuthorizationOptions {
    /// <summary>
    /// Username for the built-in superuser account. Default: "vibemq".
    /// </summary>
    public string SuperuserUsername { get; set; } = "vibemq";

    /// <summary>
    /// Password for the superuser on first startup. Once the account is created in the
    /// database this value is ignored — use ChangePassword admin command to update it.
    /// Must be explicitly configured when authorization is enabled.
    /// </summary>
    public string? SuperuserPassword { get; set; }

    /// <summary>
    /// Path to the SQLite database file that stores users and permissions.
    /// Default: "auth.db" (relative to the working directory).
    /// </summary>
    public string DatabasePath { get; set; } = "auth.db";
}
