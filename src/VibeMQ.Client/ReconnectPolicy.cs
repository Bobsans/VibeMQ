namespace VibeMQ.Client;

/// <summary>
/// Policy that controls automatic reconnection behavior.
/// </summary>
public sealed class ReconnectPolicy {
    /// <summary>
    /// Maximum number of reconnection attempts. Default: unlimited.
    /// </summary>
    public int MaxAttempts { get; set; } = int.MaxValue;

    /// <summary>
    /// Initial delay before the first reconnection attempt. Default: 1 second.
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum delay between reconnection attempts. Default: 5 minutes.
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Whether to use exponential backoff for delays. Default: true.
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Calculates the delay for the given attempt number (1-based).
    /// </summary>
    public TimeSpan GetDelay(int attempt) {
        if (!UseExponentialBackoff) {
            return InitialDelay;
        }

        var delay = TimeSpan.FromTicks(
            InitialDelay.Ticks * (long)Math.Pow(2, attempt - 1)
        );

        return delay > MaxDelay ? MaxDelay : delay;
    }
}
