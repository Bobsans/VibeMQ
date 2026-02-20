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

#if NET10_0_OR_GREATER
        var collection = X509CertificateLoader.LoadPkcs12CollectionFromFile(
            options.CertificatePath,
            options.CertificatePassword ?? string.Empty);
        var certificate = collection[0];
#else
        var certificate = new X509Certificate2(options.CertificatePath, options.CertificatePassword);
#endif
        var sslStream = new SslStream(innerStream, leaveInnerStreamOpen: false);

        await sslStream.AuthenticateAsServerAsync(
            new SslServerAuthenticationOptions {
                ServerCertificate = certificate,
                ClientCertificateRequired = options.RequireClientCertificate,
                EnabledSslProtocols = options.SslProtocols,
            },
            cancellationToken
        ).ConfigureAwait(false);

        return sslStream;
    }
}
