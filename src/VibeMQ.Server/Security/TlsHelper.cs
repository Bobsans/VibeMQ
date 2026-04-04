using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using VibeMQ.Configuration;

namespace VibeMQ.Server.Security;

/// <summary>
/// Helper for establishing TLS/SSL connections on the server side.
/// </summary>
public static class TlsHelper {
    /// <summary>
    /// Wraps a network stream in an <see cref="SslStream"/> and performs server-side TLS handshake.
    /// </summary>
    public static async Task<SslStream> AuthenticateAsServerAsync(
        Stream innerStream,
        TlsOptions options,
        CancellationToken cancellationToken = default
    ) {
        if (string.IsNullOrEmpty(options.CertificatePath)) {
            throw new InvalidOperationException("TLS is enabled but no certificate path is configured.");
        }

        string fullCertificatePath;
        try {
            fullCertificatePath = Path.GetFullPath(options.CertificatePath);
        } catch (Exception ex) {
            throw new InvalidOperationException("TLS certificate path is invalid.", ex);
        }

        ValidateCertificatePath(fullCertificatePath, options);

        if (!File.Exists(fullCertificatePath)) {
            throw new FileNotFoundException($"TLS certificate file not found: {fullCertificatePath}");
        }

#if NET10_0_OR_GREATER
        var collection = X509CertificateLoader.LoadPkcs12CollectionFromFile(
            fullCertificatePath,
            options.CertificatePassword ?? string.Empty);
        var certificate = collection[0];
#else
        var certificate = new X509Certificate2(fullCertificatePath, options.CertificatePassword);
#endif
        var sslStream = new SslStream(innerStream, leaveInnerStreamOpen: false);

        try {
            await sslStream.AuthenticateAsServerAsync(
                new SslServerAuthenticationOptions {
                    ServerCertificate = certificate,
                    ClientCertificateRequired = options.RequireClientCertificate,
                    EnabledSslProtocols = options.SslProtocols
                },
                cancellationToken
            ).ConfigureAwait(false);

            return sslStream;
        } catch {
            await sslStream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static void ValidateCertificatePath(string fullCertificatePath, TlsOptions options) {
        if (options.AllowedCertificateDirectories.Count > 0) {
            foreach (var configuredDirectory in options.AllowedCertificateDirectories) {
                if (string.IsNullOrWhiteSpace(configuredDirectory)) {
                    continue;
                }

                var fullDirectory = Path.GetFullPath(configuredDirectory);
                if (IsPathUnderDirectory(fullCertificatePath, fullDirectory)) {
                    return;
                }
            }

            throw new InvalidOperationException(
                $"TLS certificate path '{fullCertificatePath}' is outside allowed directories.");
        }

        if (options.RestrictCertificatePathToWorkingDirectory) {
            var workingDirectory = Path.GetFullPath(Environment.CurrentDirectory);
            if (!IsPathUnderDirectory(fullCertificatePath, workingDirectory)) {
                throw new InvalidOperationException(
                    $"TLS certificate path '{fullCertificatePath}' must be under '{workingDirectory}'.");
            }
        }
    }

    private static bool IsPathUnderDirectory(string fullPath, string fullDirectory) {
        var normalizedDirectory = Path.TrimEndingDirectorySeparator(fullDirectory);
        var directoryPrefix = normalizedDirectory + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return fullPath.StartsWith(directoryPrefix, comparison);
    }
}
