using VibeMQ.Client;
using VibeMQ.Client.Exceptions;
using VibeMQ.Protocol.Compression;

namespace VibeMQ.Tests.Unit.Client;

public sealed class VibeMQConnectionStringTests {
    [Fact]
    public void Parse_URL_localhost_uses_default_port() {
        var r = VibeMQConnectionString.Parse("vibemq://localhost");
        Assert.Equal("localhost", r.Host);
        Assert.Equal(2925, r.Port);
        Assert.NotNull(r.Options);
    }

    [Fact]
    public void Parse_URL_with_port() {
        var r = VibeMQConnectionString.Parse("vibemq://broker.example.com:9000");
        Assert.Equal("broker.example.com", r.Host);
        Assert.Equal(9000, r.Port);
    }

    [Fact]
    public void Parse_URL_with_username_password() {
        var r = VibeMQConnectionString.Parse("vibemq://user:secret@host:2925");
        Assert.Equal("user", r.Options.Username);
        Assert.Equal("secret", r.Options.Password);
        Assert.Equal("host", r.Host);
    }

    [Fact]
    public void Parse_URL_with_query_tls_and_keepAlive() {
        var r = VibeMQConnectionString.Parse("vibemq://localhost?tls=true&keepAlive=60");
        Assert.True(r.Options.UseTls);
        Assert.Equal(TimeSpan.FromSeconds(60), r.Options.KeepAliveInterval);
    }

    [Fact]
    public void Parse_URL_with_compression_none() {
        var r = VibeMQConnectionString.Parse("vibemq://localhost?compression=none");
        Assert.NotNull(r.Options.PreferredCompressions);
        Assert.Empty(r.Options.PreferredCompressions);
    }

    [Fact]
    public void Parse_URL_with_compression_brotli_gzip() {
        var r = VibeMQConnectionString.Parse("vibemq://localhost?compression=brotli,gzip");
        Assert.Equal(2, r.Options.PreferredCompressions.Count);
        Assert.Equal(CompressionAlgorithm.Brotli, r.Options.PreferredCompressions[0]);
        Assert.Equal(CompressionAlgorithm.GZip, r.Options.PreferredCompressions[1]);
    }

    [Fact]
    public void Parse_URL_with_queues() {
        var r = VibeMQConnectionString.Parse("vibemq://localhost?queues=orders,notifications");
        Assert.Equal(2, r.Options.QueueDeclarations.Count);
        Assert.Equal("orders", r.Options.QueueDeclarations[0].QueueName);
        Assert.Equal("notifications", r.Options.QueueDeclarations[1].QueueName);
    }

    [Fact]
    public void Parse_URL_with_reconnect_params() {
        var r = VibeMQConnectionString.Parse("vibemq://localhost?reconnectMaxAttempts=5&reconnectInitialDelay=2&reconnectMaxDelay=120&reconnectExponentialBackoff=false");
        Assert.Equal(5, r.Options.ReconnectPolicy.MaxAttempts);
        Assert.Equal(TimeSpan.FromSeconds(2), r.Options.ReconnectPolicy.InitialDelay);
        Assert.Equal(TimeSpan.FromSeconds(120), r.Options.ReconnectPolicy.MaxDelay);
        Assert.False(r.Options.ReconnectPolicy.UseExponentialBackoff);
    }

    [Fact]
    public void Parse_keyvalue_Host_Port() {
        var r = VibeMQConnectionString.Parse("Host=myhost;Port=3000");
        Assert.Equal("myhost", r.Host);
        Assert.Equal(3000, r.Port);
    }

    [Fact]
    public void Parse_keyvalue_default_host_when_empty() {
        var r = VibeMQConnectionString.Parse("Port=2925");
        Assert.Equal("localhost", r.Host);
        Assert.Equal(2925, r.Port);
    }

    [Fact]
    public void Parse_keyvalue_Username_Password_UseTls() {
        var r = VibeMQConnectionString.Parse("Host=h;Port=2925;Username=u;Password=p;UseTls=true");
        Assert.Equal("u", r.Options.Username);
        Assert.Equal("p", r.Options.Password);
        Assert.True(r.Options.UseTls);
    }

    [Fact]
    public void Parse_keyvalue_CompressionThreshold() {
        var r = VibeMQConnectionString.Parse("Host=h;Port=2925;CompressionThreshold=2048");
        Assert.Equal(2048, r.Options.CompressionThreshold);
    }

    [Fact]
    public void Parse_keyvalue_Queues() {
        var r = VibeMQConnectionString.Parse("Host=h;Port=2925;Queues=a,b,c");
        Assert.Equal(3, r.Options.QueueDeclarations.Count);
        Assert.Equal("a", r.Options.QueueDeclarations[0].QueueName);
        Assert.Equal("b", r.Options.QueueDeclarations[1].QueueName);
        Assert.Equal("c", r.Options.QueueDeclarations[2].QueueName);
    }

    [Fact]
    public void Parse_null_throws() {
        var ex = Assert.Throws<VibeMQConnectionStringException>(() => VibeMQConnectionString.Parse(null));
        Assert.Contains("null or empty", ex.Message);
    }

    [Fact]
    public void Parse_empty_throws() {
        Assert.Throws<VibeMQConnectionStringException>(() => VibeMQConnectionString.Parse(""));
        Assert.Throws<VibeMQConnectionStringException>(() => VibeMQConnectionString.Parse("   "));
    }

    [Fact]
    public void Parse_URL_invalid_query_param_without_equals_throws() {
        var ex = Assert.Throws<VibeMQConnectionStringException>(() => VibeMQConnectionString.Parse("vibemq://localhost?badparam"));
        Assert.Contains("Invalid query parameter", ex.Message);
    }

    [Fact]
    public void TryParse_null_returns_false() {
        Assert.False(VibeMQConnectionString.TryParse(null, out var r));
        Assert.Null(r);
    }

    [Fact]
    public void TryParse_valid_returns_true() {
        Assert.True(VibeMQConnectionString.TryParse("vibemq://localhost:2925", out var r));
        Assert.NotNull(r);
        Assert.Equal("localhost", r!.Host);
        Assert.Equal(2925, r.Port);
    }

    [Fact]
    public void TryParse_invalid_returns_false() {
        Assert.False(VibeMQConnectionString.TryParse("vibemq://localhost?x", out var r));
        Assert.Null(r);
    }

    [Fact]
    public void Parse_URL_scheme_case_insensitive() {
        var r = VibeMQConnectionString.Parse("VibeMQ://localhost");
        Assert.Equal("localhost", r.Host);
    }

    [Fact]
    public void Parse_reconnectMaxAttempts_zero_means_unlimited() {
        var r = VibeMQConnectionString.Parse("Host=h;Port=2925;ReconnectMaxAttempts=0");
        Assert.Equal(int.MaxValue, r.Options.ReconnectPolicy.MaxAttempts);
    }
}
