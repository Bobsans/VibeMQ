using VibeMQ.Configuration;
using VibeMQ.Server.Security;

namespace VibeMQ.Tests.Unit.Security;

public class TlsHelperTests {
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
}
