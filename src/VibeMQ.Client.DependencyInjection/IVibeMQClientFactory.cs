namespace VibeMQ.Client.DependencyInjection;

/// <summary>
/// Factory for creating connected VibeMQ clients using options and logging from the DI container.
/// </summary>
public interface IVibeMQClientFactory {
    /// <summary>
    /// Connects to the broker and returns a <see cref="VibeMQClient"/> instance.
    /// The caller is responsible for disposing the client (e.g. with await using).
    /// </summary>
    Task<VibeMQClient> CreateAsync(CancellationToken cancellationToken = default);
}
