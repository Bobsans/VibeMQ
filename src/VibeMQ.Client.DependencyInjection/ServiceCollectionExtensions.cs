using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VibeMQ.Client.Exceptions;
using VibeMQ.Interfaces;

namespace VibeMQ.Client.DependencyInjection;

/// <summary>
/// Extension methods for registering VibeMQ client with Microsoft dependency injection.
/// </summary>
public static class ServiceCollectionExtensions {
    /// <param name="services">The service collection.</param>
    extension(IServiceCollection services) {
        /// <summary>
        /// Adds the VibeMQ client factory and injectable <see cref="IVibeMQClient"/> to the service collection.
        /// Options can be configured via <see cref="OptionsBuilder{TOptions}"/> (e.g., services.Configure&lt;VibeMQClientSettings&gt;(...))
        /// or by using the overload that accepts a configuration delegate.
        /// Inject <see cref="IVibeMQClient"/> for a shared, lazily connected client, or <see cref="IVibeMQClientFactory"/> and call
        /// <see cref="IVibeMQClientFactory.CreateAsync"/> to get a dedicated connected client (caller disposes).
        /// </summary>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection AddVibeMQClient() {
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
        /// <param name="configureOptions">Delegate to configure <see cref="VibeMQClientSettings"/>.</param>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection AddVibeMQClient(Action<VibeMQClientSettings> configureOptions) {
            ArgumentNullException.ThrowIfNull(configureOptions);
            services.Configure(configureOptions);
            return services.AddVibeMQClient();
        }

        /// <summary>
        /// Adds the VibeMQ client and configures it from a connection string (URL or key=value format).
        /// </summary>
        /// <param name="connectionString">Connection string, e.g. <c>vibemq://host:2925</c> or <c>Host=localhost;Port=2925</c>.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="VibeMQConnectionStringException">The connection string is invalid.</exception>
        public IServiceCollection AddVibeMQClient(string connectionString) {
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
            var parsed = VibeMQConnectionString.Parse(connectionString);
            return services.AddVibeMQClient(settings => {
                settings.Host = parsed.Host;
                settings.Port = parsed.Port;
                settings.ClientOptions = parsed.Options;
            });
        }

        /// <summary>
        /// Adds the VibeMQ client and configures it from configuration.
        /// Reads connection string from <c>ConnectionStrings:VibeMQ</c> or <c>VibeMQ:Client:ConnectionString</c> (first non-empty wins).
        /// </summary>
        /// <param name="configuration">Application configuration.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="VibeMQConnectionStringException">The connection string is missing or invalid.</exception>
        public IServiceCollection AddVibeMQClient(IConfiguration configuration) {
            ArgumentNullException.ThrowIfNull(configuration);
            var connectionString = configuration["ConnectionStrings:VibeMQ"] ?? configuration["VibeMQ:Client:ConnectionString"];
            if (string.IsNullOrWhiteSpace(connectionString)) {
                throw new VibeMQConnectionStringException("VibeMQ connection string not found. Set ConnectionStrings:VibeMQ or VibeMQ:Client:ConnectionString in configuration.");
            }
            return services.AddVibeMQClient(connectionString.Trim());
        }

        /// <summary>
        /// Registers a message handler type in the DI container.
        /// The handler will be available for use with <see cref="IVibeMQClient.SubscribeAsync{TMessage, THandler}"/>.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <typeparam name="THandler">The handler type implementing <see cref="IMessageHandler{TMessage}"/>.</typeparam>
        /// <param name="lifetime">Service lifetime (default: Scoped).</param>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection AddMessageHandler<TMessage, THandler>(
            ServiceLifetime lifetime = ServiceLifetime.Scoped
        ) where THandler : class, IMessageHandler<TMessage> {
            services.TryAdd(new ServiceDescriptor(typeof(THandler), typeof(THandler), lifetime));
            services.TryAddEnumerable(new ServiceDescriptor(typeof(IMessageHandler<TMessage>), typeof(THandler), lifetime));
            return services;
        }

        /// <summary>
        /// Scans the specified assembly for types implementing <see cref="IMessageHandler{T}"/> and registers them in the DI container.
        /// </summary>
        /// <param name="assembly">The assembly to scan.</param>
        /// <param name="lifetime">Service lifetime for handlers (default: Scoped).</param>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection AddMessageHandlers(
            Assembly assembly,
            ServiceLifetime lifetime = ServiceLifetime.Scoped
        ) {
            ArgumentNullException.ThrowIfNull(assembly);

            var handlerTypes = assembly.GetTypes()
                .Where(t => t is { IsAbstract: false, IsInterface: false })
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
        /// Enables automatic subscription for message handlers decorated with <see cref="VibeMQ.Attributes.QueueAttribute"/>.
        /// When the application starts, all registered handlers with <see cref="VibeMQ.Attributes.QueueAttribute"/> will be automatically subscribed.
        /// </summary>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection AddMessageHandlerSubscriptions() {
            services.TryAddSingleton<MessageHandlerHostedService>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, MessageHandlerHostedService>(
                sp => sp.GetRequiredService<MessageHandlerHostedService>()));
            return services;
        }
    }
}
