using Microsoft.Extensions.Logging;
using VibeMQ.Core.Configuration;
using VibeMQ.Core.Interfaces;
using VibeMQ.Protocol;
using VibeMQ.Server.Connections;

namespace VibeMQ.Server.Handlers;

/// <summary>
/// Handles the Connect command: authenticates the client if required.
/// </summary>
public sealed partial class ConnectHandler : ICommandHandler {
    private readonly BrokerOptions _options;
    private readonly IAuthenticationService? _authService;
    private readonly ILogger<ConnectHandler> _logger;

    public ConnectHandler(
        BrokerOptions options,
        IAuthenticationService? authService,
        ILogger<ConnectHandler> logger
    ) {
        _options = options;
        _authService = authService;
        _logger = logger;
    }

    public CommandType CommandType => CommandType.Connect;

    public async Task HandleAsync(
        ClientConnection connection,
        ProtocolMessage message,
        CancellationToken cancellationToken = default
    ) {
        if (_options.EnableAuthentication && _authService is not null) {
            var token = message.Headers?.GetValueOrDefault("authToken");

            if (string.IsNullOrEmpty(token)) {
                await connection.SendErrorAsync("AUTH_REQUIRED", "Authentication token is required.", cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            var isValid = await _authService.AuthenticateAsync(token, cancellationToken).ConfigureAwait(false);

            if (!isValid) {
                LogAuthFailed(connection.Id);
                await connection.SendErrorAsync("AUTH_FAILED", "Invalid authentication token.", cancellationToken)
                    .ConfigureAwait(false);
                return;
            }
        }

        connection.IsAuthenticated = true;
        LogClientAuthenticated(connection.Id);

        await connection.SendMessageAsync(new ProtocolMessage {
            Type = CommandType.ConnectAck,
        }, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Authentication failed for client {clientId}.")]
    private partial void LogAuthFailed(string clientId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Client {clientId} authenticated successfully.")]
    private partial void LogClientAuthenticated(string clientId);
}
