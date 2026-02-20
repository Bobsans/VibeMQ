using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VibeMQ.Client;
using VibeMQ.Attributes;
using VibeMQ.Interfaces;

namespace VibeMQ.Client.DependencyInjection;

/// <summary>
/// Hosted service that automatically subscribes to queues for registered message handlers.
/// Scans for handlers with <see cref="QueueAttribute"/> and subscribes them when the application starts.
/// </summary>
internal sealed partial class MessageHandlerHostedService : IHostedService {
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MessageHandlerHostedService> _logger;
    private readonly List<IAsyncDisposable> _subscriptions = new();

    public MessageHandlerHostedService(
        IServiceProvider serviceProvider,
        ILogger<MessageHandlerHostedService> logger
    ) {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken) {
        var client = _serviceProvider.GetService<IVibeMQClient>();
        if (client is null) {
            LogClientNotFound();
            return;
        }

        // Find all registered handlers with QueueAttribute
        var handlerRegistrations = FindHandlerRegistrations();

        foreach (var registration in handlerRegistrations) {
            try {
                LogSubscribingHandler(registration.HandlerType.Name, registration.QueueName, registration.MessageType.Name);

                // Use reflection to call SubscribeAsync<TMessage, THandler>
                var subscribeMethod = typeof(IVibeMQClient)
                    .GetMethod(nameof(IVibeMQClient.SubscribeAsync), new[] { typeof(string), typeof(CancellationToken) })
                    ?.MakeGenericMethod(registration.MessageType, registration.HandlerType);

                if (subscribeMethod is null) {
                    LogSubscribeMethodNotFound(registration.HandlerType.Name);
                    continue;
                }

                var subscriptionTask = (Task<IAsyncDisposable>)subscribeMethod.Invoke(client, new object[] { registration.QueueName, cancellationToken })!;
                var subscription = await subscriptionTask.ConfigureAwait(false);
                _subscriptions.Add(subscription);

                LogSubscribedSuccessfully(registration.HandlerType.Name, registration.QueueName);
            } catch (Exception ex) {
                LogSubscribeFailed(ex, registration.HandlerType.Name, registration.QueueName);
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken) {
        LogUnsubscribing(_subscriptions.Count);

        foreach (var subscription in _subscriptions) {
            try {
                await subscription.DisposeAsync().ConfigureAwait(false);
            } catch (Exception ex) {
                LogDisposeError(ex);
            }
        }

        _subscriptions.Clear();
    }

    private List<HandlerRegistration> FindHandlerRegistrations() {
        var registrations = new List<HandlerRegistration>();

        // Scan loaded assemblies for handlers with QueueAttribute
        // Also check if handlers are registered in DI and can be resolved
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !a.FullName?.StartsWith("System.", StringComparison.Ordinal) == true && !a.FullName?.StartsWith("Microsoft.", StringComparison.Ordinal) == true);

        foreach (var assembly in assemblies) {
            try {
                var handlerTypes = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface)
                    .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMessageHandler<>)));

                foreach (var handlerType in handlerTypes) {
                    var queueAttribute = handlerType.GetCustomAttribute<QueueAttribute>();
                    if (queueAttribute is null) {
                        continue;
                    }

                    var messageHandlerInterface = handlerType.GetInterfaces()
                        .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMessageHandler<>));

                    if (messageHandlerInterface is null) {
                        continue;
                    }

                    // Verify handler can be resolved from DI
                    try {
                        using var scope = _serviceProvider.CreateScope();
                        var handler = scope.ServiceProvider.GetService(handlerType);
                        if (handler is null) {
                            LogHandlerNotRegistered(handlerType.Name);
                            continue;
                        }
                    } catch {
                        // Handler might not be registered, skip it
                        LogHandlerCannotBeResolved(handlerType.Name);
                        continue;
                    }

                    var messageType = messageHandlerInterface.GetGenericArguments()[0];
                    registrations.Add(new HandlerRegistration {
                        HandlerType = handlerType,
                        MessageType = messageType,
                        QueueName = queueAttribute.QueueName
                    });
                }
            } catch (Exception ex) {
                LogAssemblyScanError(ex, assembly.FullName ?? "Unknown");
            }
        }

        return registrations;
    }

    private sealed class HandlerRegistration {
        public required Type HandlerType { get; init; }
        public required Type MessageType { get; init; }
        public required string QueueName { get; init; }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "IVibeMQClient not found in DI container. Automatic subscriptions will not be created.")]
    private partial void LogClientNotFound();

    [LoggerMessage(Level = LogLevel.Information, Message = "Subscribing handler {HandlerType} to queue {QueueName} for message type {MessageType}")]
    private partial void LogSubscribingHandler(string handlerType, string queueName, string messageType);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to find SubscribeAsync method for {HandlerType}")]
    private partial void LogSubscribeMethodNotFound(string handlerType);

    [LoggerMessage(Level = LogLevel.Information, Message = "Successfully subscribed {HandlerType} to queue {QueueName}")]
    private partial void LogSubscribedSuccessfully(string handlerType, string queueName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to subscribe {HandlerType} to queue {QueueName}")]
    private partial void LogSubscribeFailed(Exception ex, string handlerType, string queueName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Unsubscribing {Count} message handlers")]
    private partial void LogUnsubscribing(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Error disposing subscription")]
    private partial void LogDisposeError(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Handler {HandlerType} is not registered in DI container. Skipping automatic subscription.")]
    private partial void LogHandlerNotRegistered(string handlerType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Handler {HandlerType} cannot be resolved from DI. Skipping automatic subscription.")]
    private partial void LogHandlerCannotBeResolved(string handlerType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Error scanning assembly {Assembly} for handlers")]
    private partial void LogAssemblyScanError(Exception ex, string assembly);
}
