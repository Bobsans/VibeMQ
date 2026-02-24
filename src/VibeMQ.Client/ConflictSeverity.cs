namespace VibeMQ.Client;

/// <summary>
/// Severity of a setting difference between a declared queue and an existing one.
/// </summary>
public enum ConflictSeverity {
    /// <summary>
    /// Informational difference. Not a conflict.
    /// Logged at Debug level; does not trigger OnConflict.
    /// </summary>
    Info,

    /// <summary>
    /// Behavioral difference. Counts as a conflict.
    /// Logged at Warning level; OnConflict is applied.
    /// </summary>
    Soft,

    /// <summary>
    /// Semantically breaking difference. Counts as a conflict.
    /// Logged at Error level; OnConflict is applied.
    /// </summary>
    Hard,
}
