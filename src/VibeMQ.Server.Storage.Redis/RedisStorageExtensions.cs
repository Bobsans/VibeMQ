using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using VibeMQ.Interfaces;

namespace VibeMQ.Server.Storage.Redis;

/// <summary>
/// Extension methods for configuring Redis storage with <see cref="BrokerBuilder"/> and DI.
/// </summary>
public static class RedisStorageExtensions {
    /// <summary>
    /// Configures the broker to use Redis for message persistence (fluent builder API).
    /// </summary>
    /// <param name="builder">The broker builder instance.</param>
    /// <param name="connectionString">Redis connection string (e.g. "localhost:6379").</param>
    /// <param name="configure">Optional callback to configure Redis storage options.</param>
    /// <returns>The builder instance for chaining.</returns>
    public static BrokerBuilder UseRedisStorage(
        this BrokerBuilder builder,
        string connectionString,
        Action<RedisStorageOptions>? configure = null
    ) {
        var options = new RedisStorageOptions { ConnectionString = connectionString };
        configure?.Invoke(options);

        builder.UseStorageProvider(loggerFactory => {
            var logger = loggerFactory.CreateLogger<RedisStorageProvider>();
            var connectionFactory = new RedisStorageConnectionFactory(options, loggerFactory.CreateLogger<RedisStorageConnectionFactory>());
            return new RedisStorageProvider(options, connectionFactory, logger);
        });

        return builder;
    }

    /// <param name="services">The service collection.</param>
    extension(IServiceCollection services) {
        /// <summary>
        /// Registers the Redis storage provider in the DI container.
        /// Call this before <c>AddVibeMQBroker()</c> to enable Redis persistence.
        /// </summary>
        /// <param name="connectionString">Redis connection string.</param>
        /// <param name="configure">Optional callback to configure Redis storage options.</param>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection AddVibeMQRedisStorage(string connectionString, Action<RedisStorageOptions>? configure = null) {
            var options = new RedisStorageOptions { ConnectionString = connectionString };
            configure?.Invoke(options);

            services.TryAddSingleton<RedisStorageConnectionFactory>(sp => {
                var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<RedisStorageConnectionFactory>();
                return new RedisStorageConnectionFactory(options, logger);
            });
            services.TryAddSingleton<IStorageProvider>(sp => {
                var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<RedisStorageProvider>();
                var factory = sp.GetRequiredService<RedisStorageConnectionFactory>();
                return new RedisStorageProvider(options, factory, logger);
            });

            return services;
        }

        /// <summary>
        /// Registers the Redis storage provider using configuration (e.g. "VibeMQ:Storage:Redis" or "ConnectionStrings:Redis").
        /// Call this before <c>AddVibeMQBroker()</c> to enable Redis persistence.
        /// </summary>
        /// <param name="configuration">Configuration section with keys: ConnectionString (or use ConnectionStrings:Redis), Database, KeyPrefix, etc.</param>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection AddVibeMQRedisStorage(IConfiguration configuration) {
            var options = new RedisStorageOptions();
            var connStr = configuration["ConnectionString"] ?? configuration.GetConnectionString("Redis");
            if (!string.IsNullOrEmpty(connStr)) {
                options.ConnectionString = connStr;
            }

            if (int.TryParse(configuration["Database"], out var db)) {
                options.Database = db;
            }

            if (configuration["KeyPrefix"] is { Length: > 0 } prefix) {
                options.KeyPrefix = prefix;
            }

            if (int.TryParse(configuration["ConnectTimeoutMs"], out var connectTimeout)) {
                options.ConnectTimeoutMs = connectTimeout;
            }

            if (int.TryParse(configuration["SyncTimeoutMs"], out var syncTimeout)) {
                options.SyncTimeoutMs = syncTimeout;
            }

            return services.AddVibeMQRedisStorage(options.ConnectionString, o => {
                o.Database = options.Database;
                o.KeyPrefix = options.KeyPrefix;
                o.ConnectTimeoutMs = options.ConnectTimeoutMs;
                o.SyncTimeoutMs = options.SyncTimeoutMs;
            });
        }
    }
}
