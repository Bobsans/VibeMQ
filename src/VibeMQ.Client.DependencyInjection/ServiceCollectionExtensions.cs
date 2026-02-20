using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VibeMQ.Client;
using VibeMQ.Interfaces;

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
        services.TryAddSingleton<IVibeMQClientFactory>(sp => new VibeMQClientFactory(
            sp.GetRequiredService<IOptions<VibeMQClientSettings>>(),
            sp.GetRequiredService<ILogger<VibeMQClient>>(),
            sp
        ));
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

    /// <summary>
    /// Registers a message handler type in the DI container.
    /// The handler will be available for use with <see cref="IVibeMQClient.SubscribeAsync{TMessage, THandler}"/>.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="THandler">The handler type implementing <see cref="IMessageHandler{TMessage}"/>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">Service lifetime (default: Scoped).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMessageHandler<TMessage, THandler>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped
    ) where THandler : class, IMessageHandler<TMessage> {
        services.TryAdd(new ServiceDescriptor(typeof(THandler), typeof(THandler), lifetime));
        services.TryAddEnumerable(new ServiceDescriptor(typeof(IMessageHandler<TMessage>), typeof(THandler), lifetime));
        return services;
    }

    /// <summary>
    /// Scans the specified assembly for types implementing <see cref="IMessageHandler{T}"/> and registers them in the DI container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assembly">The assembly to scan.</param>
    /// <param name="lifetime">Service lifetime for handlers (default: Scoped).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMessageHandlers(
        this IServiceCollection services,
        Assembly assembly,
        ServiceLifetime lifetime = ServiceLifetime.Scoped
    ) {
        ArgumentNullException.ThrowIfNull(assembly);

        var handlerTypes = assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMessageHandler<>))
                .Select(i => new { HandlerType = t, MessageType = i.GetGenericArguments()[0] }))
            .ToList();

        foreach (var handler in handlerTypes) {
            var serviceType = typeof(IMessageHandler<>).MakeGenericType(handler.MessageType);
            services.TryAdd(new ServiceDescriptor(handler.HandlerType, handler.HandlerType, lifetime));
            services.TryAddEnumerable(new ServiceDescriptor(serviceType, handler.HandlerType, lifetime));
        }

        return services;
    }

    /// <summary>
    /// Enables automatic subscription for message handlers decorated with <see cref="QueueAttribute"/>.
    /// When the application starts, all registered handlers with <see cref="QueueAttribute"/> will be automatically subscribed.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMessageHandlerSubscriptions(this IServiceCollection services) {
        services.TryAddSingleton<MessageHandlerHostedService>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService>(
            sp => sp.GetRequiredService<MessageHandlerHostedService>()));
        return services;
    }
}
