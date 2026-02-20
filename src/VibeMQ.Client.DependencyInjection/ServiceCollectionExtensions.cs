using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VibeMQ.Client;

namespace VibeMQ.Client.DependencyInjection;

/// <summary>
/// Extension methods for registering VibeMQ client with Microsoft dependency injection.
/// </summary>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Adds the VibeMQ client factory and injectable <see cref="IVibeMQClient"/> to the service collection.
    /// Options can be configured via <see cref="OptionsBuilder{TOptions}"/> (e.g. services.Configure&lt;VibeMQClientSettings&gt;(...))
    /// or by using the overload that accepts a configuration delegate.
    /// Inject <see cref="IVibeMQClient"/> for a shared, lazily-connected client, or <see cref="IVibeMQClientFactory"/> and call
    /// <see cref="IVibeMQClientFactory.CreateAsync"/> to obtain a dedicated connected client (caller disposes).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVibeMQClient(this IServiceCollection services) {
        services.AddOptions<VibeMQClientSettings>();
        services.TryAddSingleton<IVibeMQClientFactory, VibeMQClientFactory>();
        services.TryAddSingleton<IVibeMQClient, ManagedVibeMQClient>();
        return services;
    }

    /// <summary>
    /// Adds the VibeMQ client factory and injectable <see cref="IVibeMQClient"/>, and configures client settings (host, port, auth, etc.).
    /// Inject <see cref="IVibeMQClient"/> for a shared client, or <see cref="IVibeMQClientFactory"/> to create a dedicated client.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Delegate to configure <see cref="VibeMQClientSettings"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVibeMQClient(this IServiceCollection services, Action<VibeMQClientSettings> configureOptions) {
        ArgumentNullException.ThrowIfNull(configureOptions);
        services.Configure(configureOptions);
        return services.AddVibeMQClient();
    }
}
