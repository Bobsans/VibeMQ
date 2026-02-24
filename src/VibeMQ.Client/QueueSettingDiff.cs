namespace VibeMQ.Client;

/// <summary>
/// Describes a single setting difference between a declared queue configuration and an existing one.
/// </summary>
/// <param name="SettingName">Name of the setting that differs.</param>
/// <param name="ExistingValue">Current value on the broker.</param>
/// <param name="DeclaredValue">Value specified in the client declaration.</param>
/// <param name="Severity">Severity of the difference.</param>
public sealed record QueueSettingDiff(
    string SettingName,
    object? ExistingValue,
    object? DeclaredValue,
    ConflictSeverity Severity
);
