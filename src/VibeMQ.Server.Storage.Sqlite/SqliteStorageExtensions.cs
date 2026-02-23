using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using VibeMQ.Interfaces;

namespace VibeMQ.Server.Storage.Sqlite;

/// <summary>
/// Extension methods for configuring SQLite storage with <see cref="BrokerBuilder"/> and DI.
/// </summary>
public static class SqliteStorageExtensions {
    /// <summary>
    /// Configures the broker to use SQLite for message persistence (fluent builder API).
    /// </summary>
    /// <param name="builder">The broker builder instance.</param>
    /// <param name="configure">Optional callback to configure SQLite options.</param>
    /// <returns>The builder instance for chaining.</returns>
    public static BrokerBuilder UseSqliteStorage(
        this BrokerBuilder builder,
        Action<SqliteStorageOptions>? configure = null
    ) {
        var options = new SqliteStorageOptions();
        configure?.Invoke(options);

        builder.UseStorageProvider(loggerFactory => {
            var logger = loggerFactory.CreateLogger<SqliteStorageProvider>();
            return new SqliteStorageProvider(options, logger);
        });

        return builder;
    }

    /// <summary>
    /// Registers the SQLite storage provider in the DI container.
    /// Call this before <c>AddVibeMQBroker()</c> to enable persistence.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional callback to configure SQLite options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVibeMQSqliteStorage(
        this IServiceCollection services,
        Action<SqliteStorageOptions>? configure = null
    ) {
        var options = new SqliteStorageOptions();
        configure?.Invoke(options);

        services.TryAddSingleton<IStorageProvider>(sp => {
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<SqliteStorageProvider>();
            return new SqliteStorageProvider(options, logger);
        });

        return services;
    }
}
