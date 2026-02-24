using VibeMQ.Configuration;
using VibeMQ.Enums;
using VibeMQ.Protocol;
using VibeMQ.Protocol.Compression;

namespace VibeMQ.Client;

/// <summary>
/// Configuration options for the VibeMQ client connection.
/// </summary>
public sealed class ClientOptions {
    /// <summary>
    /// Authentication token. If null, no authentication is performed.
    /// </summary>
    public string? AuthToken { get; set; }

    /// <summary>
    /// Reconnection policy for handling connection drops.
    /// </summary>
    public ReconnectPolicy ReconnectPolicy { get; set; } = new();

    /// <summary>
    /// Interval between keep-alive pings. Default: 30 seconds.
    /// </summary>
    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Timeout for waiting a response from the broker. Default: 10 seconds.
    /// </summary>
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Whether to use TLS for the connection. Default: false.
    /// </summary>
    public bool UseTls { get; set; }

    /// <summary>
    /// Whether to skip TLS certificate validation (for self-signed certs). Default: false.
    /// </summary>
    public bool SkipCertificateValidation { get; set; }

    /// <summary>
    /// Compression algorithms the client prefers, in descending priority order.
    /// Sent in the Connect handshake as <c>supported-compression</c>.
    /// An empty list disables compression negotiation.
    /// </summary>
    public IReadOnlyList<CompressionAlgorithm> PreferredCompressions { get; set; }
        = [CompressionAlgorithm.Brotli, CompressionAlgorithm.GZip];

    /// <summary>
    /// Minimum serialized body size in bytes required to apply compression.
    /// Default: <see cref="ProtocolConstants.COMPRESSION_THRESHOLD"/> (1 KB).
    /// </summary>
    public int CompressionThreshold { get; set; } = ProtocolConstants.COMPRESSION_THRESHOLD;

    /// <summary>
    /// Queues that the client automatically creates or verifies on connection.
    /// Processed sequentially in declaration order.
    /// </summary>
    public IList<QueueDeclaration> QueueDeclarations { get; set; } = [];

    /// <summary>
    /// Adds a queue declaration for automatic provisioning on connect.
    /// Returns <c>this</c> for fluent chaining.
    /// </summary>
    /// <param name="name">Queue name.</param>
    /// <param name="configure">Optional callback to configure queue options.</param>
    /// <param name="onConflict">Strategy when Soft/Hard differences are found. Default: Ignore.</param>
    /// <param name="failOnError">
    /// When <c>true</c> (default), a provisioning error aborts <c>ConnectAsync</c>.
    /// When <c>false</c>, the error is logged and the next declaration is processed.
    /// </param>
    public ClientOptions DeclareQueue(
        string name,
        Action<QueueOptions>? configure = null,
        QueueConflictResolution onConflict = QueueConflictResolution.Ignore,
        bool failOnError = true
    ) {
        var opts = new QueueOptions();
        configure?.Invoke(opts);
        QueueDeclarations.Add(new QueueDeclaration {
            QueueName = name,
            Options = opts,
            OnConflict = onConflict,
            FailOnProvisioningError = failOnError,
        });
        return this;
    }

    /// <summary>
    /// Validates all queue declarations before connecting.
    /// Throws <see cref="InvalidOperationException"/> if any declaration has an invalid
    /// cross-parameter combination (e.g. <see cref="OverflowStrategy.RedirectToDlq"/>
    /// without <see cref="QueueOptions.EnableDeadLetterQueue"/>).
    /// </summary>
    public void ValidateDeclarations() {
        foreach (var declaration in QueueDeclarations) {
            if (declaration.Options.OverflowStrategy == OverflowStrategy.RedirectToDlq &&
                !declaration.Options.EnableDeadLetterQueue) {
                throw new InvalidOperationException(
                    $"Queue declaration '{declaration.QueueName}': OverflowStrategy is RedirectToDlq " +
                    "but EnableDeadLetterQueue is false. Enable DLQ or choose a different overflow strategy."
                );
            }
        }
    }
}
