using VibeMQ.Server.Auth;

namespace VibeMQ.Tests.Unit.Security;

public class TokenAuthTests {
    [Fact]
    public async Task Authenticate_ValidToken_ReturnsTrue() {
        var service = new TokenAuthenticationService("secret-token");

        var result = await service.AuthenticateAsync("secret-token");

        Assert.True(result);
    }

    [Fact]
    public async Task Authenticate_InvalidToken_ReturnsFalse() {
        var service = new TokenAuthenticationService("secret-token");

        var result = await service.AuthenticateAsync("wrong-token");

        Assert.False(result);
    }

    [Fact]
    public async Task Authenticate_EmptyToken_ReturnsFalse() {
        var service = new TokenAuthenticationService("secret-token");

        var result = await service.AuthenticateAsync("");

        Assert.False(result);
    }

    [Fact]
    public async Task Authenticate_CaseSensitive() {
        var service = new TokenAuthenticationService("Secret-Token");

        var result = await service.AuthenticateAsync("secret-token");

        Assert.False(result);
    }
}
