using Microsoft.Extensions.Logging;
using VibeMQ.Configuration;
using VibeMQ.Interfaces;
using VibeMQ.Protocol;
using VibeMQ.Protocol.Compression;
using VibeMQ.Server.Auth;
using VibeMQ.Server.Connections;

namespace VibeMQ.Server.Handlers;

/// <summary>
/// Handles the Connect command: authenticates the client and negotiates compression.
/// Supports both legacy token-based auth (<see cref="IAuthenticationService"/>) and
/// new username/password auth (<see cref="IPasswordAuthenticationService"/>).
/// </summary>
public sealed partial class ConnectHandler : ICommandHandler {
    private readonly BrokerOptions _options;
    private readonly IAuthenticationService? _authService;
    private readonly IPasswordAuthenticationService? _passwordAuthService;
    private readonly ILogger<ConnectHandler> _logger;

    public ConnectHandler(
        BrokerOptions options,
        IAuthenticationService? authService,
        IPasswordAuthenticationService? passwordAuthService,
        ILogger<ConnectHandler> logger
    ) {
        _options = options;
        _authService = authService;
        _passwordAuthService = passwordAuthService;
        _logger = logger;
    }

    public CommandType CommandType => CommandType.Connect;

    public async Task HandleAsync(
        ClientConnection connection,
        ProtocolMessage message,
        CancellationToken cancellationToken = default
    ) {
        // --- Authorization mode: username + password ---
        if (_passwordAuthService is not null) {
            var username = message.Headers?.GetValueOrDefault("authUsername");
            var password = message.Headers?.GetValueOrDefault("authPassword");

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password)) {
                await connection.SendErrorAsync(
                    "AUTH_REQUIRED",
                    "Username and password are required.",
                    cancellationToken
                ).ConfigureAwait(false);
                return;
            }

            var result = await _passwordAuthService.AuthenticateAsync(username, password, cancellationToken)
                .ConfigureAwait(false);

            if (result is null) {
                LogAuthFailed(connection.Id, username);
                await connection.SendErrorAsync(
                    "AUTH_FAILED",
                    "Invalid username or password.",
                    cancellationToken
                ).ConfigureAwait(false);
                return;
            }

            connection.Username = result.Username;
            connection.IsSuperuser = result.IsSuperuser;
            connection.CachedPermissions = result.Permissions;
        }
        // --- Legacy mode: token ---
        else if (_options.EnableAuthentication && _authService is not null) {
            var token = message.Headers?.GetValueOrDefault("authToken");

            if (string.IsNullOrEmpty(token)) {
                await connection.SendErrorAsync("AUTH_REQUIRED", "Authentication token is required.", cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

#pragma warning disable CS0618 // AuthenticateAsync(token) is intentionally used for backward compat
            var isValid = await _authService.AuthenticateAsync(token, cancellationToken).ConfigureAwait(false);
#pragma warning restore CS0618

            if (!isValid) {
                LogAuthFailed(connection.Id, "<token>");
                await connection.SendErrorAsync("AUTH_FAILED", "Invalid authentication token.", cancellationToken)
                    .ConfigureAwait(false);
                return;
            }
        }

        connection.IsAuthenticated = true;
        LogClientAuthenticated(connection.Id, connection.Username);

        // Negotiate compression
        Dictionary<string, string>? ackHeaders = null;
        var supportedByClient = message.Headers?.GetValueOrDefault("supported-compression");

        if (!string.IsNullOrEmpty(supportedByClient) && _options.SupportedCompressions.Count > 0) {
            var algorithm = NegotiateAlgorithm(supportedByClient);

            if (algorithm is not null) {
                ackHeaders = new Dictionary<string, string> {
                    ["negotiated-compression"] = CompressorFactory.Serialize(algorithm.Value),
                };
                connection.SetCompression(algorithm.Value, _options.CompressionThreshold);
                LogCompressionNegotiated(connection.Id, algorithm.Value);
            }
        }

        await connection.SendMessageAsync(new ProtocolMessage {
            Type = CommandType.ConnectAck,
            Headers = ackHeaders,
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Picks the first algorithm from the client's preference list that the broker also supports.
    /// </summary>
    private CompressionAlgorithm? NegotiateAlgorithm(string supportedByClient) {
        foreach (var part in supportedByClient.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
            var algorithm = CompressorFactory.Parse(part);

            if (algorithm is not null && _options.SupportedCompressions.Contains(algorithm.Value)) {
                return algorithm.Value;
            }
        }

        return null;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Authentication failed for client {clientId} (user: {username}).")]
    private partial void LogAuthFailed(string clientId, string username);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Client {clientId} authenticated successfully (user: {username}).")]
    private partial void LogClientAuthenticated(string clientId, string? username);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Compression negotiated for client {clientId}: {algorithm}.")]
    private partial void LogCompressionNegotiated(string clientId, CompressionAlgorithm algorithm);
}
