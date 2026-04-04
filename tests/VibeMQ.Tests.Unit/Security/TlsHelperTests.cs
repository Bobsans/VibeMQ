using System.Security.Authentication;
using VibeMQ.Configuration;
using VibeMQ.Server.Security;

namespace VibeMQ.Tests.Unit.Security;

public class TlsHelperTests {
    [Fact]
    public void TlsOptions_DefaultsToTls13Only() {
        var options = new TlsOptions();

        Assert.Equal(SslProtocols.Tls13, options.SslProtocols);
    }

    [Fact]
    public async Task AuthenticateAsServerAsync_WhenCertificatePathIsNull_Throws() {
        var options = new TlsOptions { CertificatePath = null };
        await using var stream = new MemoryStream();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => TlsHelper.AuthenticateAsServerAsync(stream, options)
        );

        Assert.Contains("certificate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuthenticateAsServerAsync_WhenCertificatePathIsEmpty_Throws() {
        var options = new TlsOptions { CertificatePath = "" };
        await using var stream = new MemoryStream();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => TlsHelper.AuthenticateAsServerAsync(stream, options)
        );

        Assert.Contains("certificate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuthenticateAsServerAsync_WhenCertificatePathIsWhitespace_Throws() {
        var options = new TlsOptions { CertificatePath = "   " };
        await using var stream = new MemoryStream();

        await Assert.ThrowsAnyAsync<Exception>(
            () => TlsHelper.AuthenticateAsServerAsync(stream, options)
        );
    }

    [Fact]
    public async Task AuthenticateAsServerAsync_PathOutsideWorkingDirectory_ThrowsInvalidOperation() {
        var outsidePath = Path.Combine(Path.GetTempPath(), "vibemq-cert-outside.pfx");
        var options = new TlsOptions {
            CertificatePath = outsidePath,
            RestrictCertificatePathToWorkingDirectory = true
        };
        await using var stream = new MemoryStream();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => TlsHelper.AuthenticateAsServerAsync(stream, options)
        );
        Assert.Contains("must be under", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuthenticateAsServerAsync_PathInAllowedDirectory_PassesPathValidation() {
        var outsidePath = Path.Combine(Path.GetTempPath(), "vibemq-cert-allowed.pfx");
        var options = new TlsOptions {
            CertificatePath = outsidePath,
            RestrictCertificatePathToWorkingDirectory = true,
            AllowedCertificateDirectories = [Path.GetTempPath()]
        };
        await using var stream = new MemoryStream();

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => TlsHelper.AuthenticateAsServerAsync(stream, options)
        );
    }
}
