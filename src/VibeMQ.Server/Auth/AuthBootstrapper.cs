using Microsoft.Extensions.Logging;
using VibeMQ.Configuration;
using VibeMQ.Server.Auth.Models;

namespace VibeMQ.Server.Auth;

/// <summary>
/// Initializes the auth database schema and creates the superuser account on first startup.
/// </summary>
public sealed partial class AuthBootstrapper {
    private readonly AuthorizationOptions _options;
    private readonly ILogger<AuthBootstrapper> _logger;

    /// <summary>
    /// The auth repository instance used by this bootstrapper.
    /// Exposed for reuse by admin command handlers (avoids duplicate instances).
    /// </summary>
    public IAuthRepository Repository { get; }

    public AuthBootstrapper(
        IAuthRepository repository,
        AuthorizationOptions options,
        ILogger<AuthBootstrapper> logger
    ) {
        Repository = repository;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Creates the schema and seeds the superuser account if necessary.
    /// Must be called before the broker starts accepting connections.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default) {
        await Repository.CreateSchemaAsync(cancellationToken).ConfigureAwait(false);

        var existing = await Repository.FindUserAsync(_options.SuperuserUsername, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null) {
            LogSuperuserAlreadyExists(_options.SuperuserUsername);
            return;
        }

        if (string.IsNullOrEmpty(_options.SuperuserPassword)) {
            throw new InvalidOperationException(
                $"Authorization is enabled but SuperuserPassword is empty. " +
                $"Set Authorization.SuperuserPassword in BrokerOptions before first startup."
            );
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var hash = BCrypt.Net.BCrypt.HashPassword(_options.SuperuserPassword, workFactor: 12);

        var record = new UserRecord {
            Username = _options.SuperuserUsername,
            PasswordHash = hash,
            IsSuperuser = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await Repository.CreateUserAsync(record, cancellationToken).ConfigureAwait(false);
        LogSuperuserCreated(_options.SuperuserUsername);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Superuser '{username}' already exists — skipping seed.")]
    private partial void LogSuperuserAlreadyExists(string username);

    [LoggerMessage(Level = LogLevel.Information, Message = "Superuser '{username}' created successfully.")]
    private partial void LogSuperuserCreated(string username);
}
