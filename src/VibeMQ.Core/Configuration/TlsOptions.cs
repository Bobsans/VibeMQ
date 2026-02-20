using System.Security.Authentication;

namespace VibeMQ.Configuration;

/// <summary>
/// TLS/SSL configuration for the broker transport layer.
/// </summary>
public sealed class TlsOptions {
    /// <summary>
    /// Whether TLS is enabled. Default: false.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Path to the PFX/PKCS12 certificate file.
    /// </summary>
    public string? CertificatePath { get; set; }

    /// <summary>
    /// Password for the certificate file.
    /// </summary>
    public string? CertificatePassword { get; set; }

    /// <summary>
    /// Allowed SSL/TLS protocols. Default: TLS 1.2 and 1.3.
    /// </summary>
    public SslProtocols SslProtocols { get; set; } = SslProtocols.Tls12 | SslProtocols.Tls13;

    /// <summary>
    /// Whether to require client certificates. Default: false.
    /// </summary>
    public bool RequireClientCertificate { get; set; }
}
