using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VibeMQ.Configuration;

namespace VibeMQ.Server.DependencyInjection;

/// <summary>
/// Extension methods for registering VibeMQ broker with Microsoft dependency injection.
/// </summary>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Adds the VibeMQ broker to the service collection. Options can be configured via
    /// <see cref="OptionsBuilder{TOptions}"/> (e.g. services.Configure&lt;BrokerOptions&gt;(...))
    /// or by using the overload that accepts a configuration delegate.
    /// The broker runs as an <see cref="Microsoft.Extensions.Hosting.IHostedService"/> when the host starts.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVibeMQBroker(this IServiceCollection services) {
        services.AddOptions<BrokerOptions>();
        services.TryAddSingleton(static sp => {
            var options = sp.GetRequiredService<IOptions<BrokerOptions>>().Value;
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            return BrokerBuilder.Create()
                .ConfigureFrom(options)
                .UseLoggerFactory(loggerFactory)
                .Build();
        });
        services.AddHostedService<VibeMQBrokerHostedService>();
        return services;
    }

    /// <summary>
    /// Adds the VibeMQ broker to the service collection and configures its options.
    /// The broker runs as an <see cref="Microsoft.Extensions.Hosting.IHostedService"/> when the host starts.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Delegate to configure <see cref="BrokerOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVibeMQBroker(this IServiceCollection services, Action<BrokerOptions> configureOptions) {
        ArgumentNullException.ThrowIfNull(configureOptions);
        services.Configure(configureOptions);
        return services.AddVibeMQBroker();
    }
}
