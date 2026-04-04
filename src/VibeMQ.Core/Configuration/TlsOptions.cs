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
    /// Restrict certificate path to the current working directory subtree.
    /// Default: true.
    /// </summary>
    public bool RestrictCertificatePathToWorkingDirectory { get; set; } = true;

    /// <summary>
    /// Optional allow-list of absolute or relative directories for certificate files.
    /// When set, certificate path must be under one of these directories.
    /// </summary>
    public IReadOnlyList<string> AllowedCertificateDirectories { get; set; } = [];

    /// <summary>
    /// Password for the certificate file.
    /// </summary>
    public string? CertificatePassword { get; set; }

    /// <summary>
    /// Allowed SSL/TLS protocols. Default: TLS 1.3.
    /// You can explicitly include <c>SslProtocols.Tls12</c> for legacy compatibility.
    /// </summary>
    public SslProtocols SslProtocols { get; set; } = SslProtocols.Tls13;

    /// <summary>
    /// Whether to require client certificates. Default: false.
    /// </summary>
    public bool RequireClientCertificate { get; set; }
}
