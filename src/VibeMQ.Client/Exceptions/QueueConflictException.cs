using System.Globalization;
using System.Text;

namespace VibeMQ.Client.Exceptions;

/// <summary>
/// Thrown when a queue declaration conflicts with the existing queue settings
/// and <see cref="QueueConflictResolution.Fail"/> is specified.
/// </summary>
public sealed class QueueConflictException : Exception {
    /// <summary>
    /// Name of the conflicting queue.
    /// </summary>
    public string QueueName { get; }

    /// <summary>
    /// Only <see cref="ConflictSeverity.Soft"/> and <see cref="ConflictSeverity.Hard"/> differences.
    /// <see cref="ConflictSeverity.Info"/> differences are never included.
    /// </summary>
    public IReadOnlyList<QueueSettingDiff> Conflicts { get; }

    /// <summary>
    /// Maximum severity among all conflicts.
    /// </summary>
    public ConflictSeverity HighestSeverity { get; }

    /// <inheritdoc />
    public QueueConflictException(string queueName, IReadOnlyList<QueueSettingDiff> conflicts)
        : base(BuildMessage(queueName, conflicts)) {
        QueueName = queueName;
        Conflicts = conflicts;
        HighestSeverity = conflicts.Max(d => d.Severity);
    }

    private static string BuildMessage(string queueName, IReadOnlyList<QueueSettingDiff> conflicts) {
        var severities = conflicts
            .Select(c => c.Severity.ToString())
            .Distinct()
            .OrderByDescending(s => s);

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"Queue '{queueName}' has conflicting settings [{string.Join(", ", severities)}]:");

        foreach (var diff in conflicts) {
            sb.AppendLine();
            sb.Append(CultureInfo.InvariantCulture, $"  [{diff.Severity}] {diff.SettingName,-20} {FormatValue(diff.ExistingValue),-15} →  {FormatValue(diff.DeclaredValue)}  (declared)");
        }

        return sb.ToString();
    }

    private static string FormatValue(object? value) => value switch {
        null => "null",
        _ => value.ToString() ?? "null"
    };
}
