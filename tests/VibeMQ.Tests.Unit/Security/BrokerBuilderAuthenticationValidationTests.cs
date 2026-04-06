using VibeMQ.Configuration;
using VibeMQ.Server;

namespace VibeMQ.Tests.Unit.Security;

public sealed class BrokerBuilderAuthenticationValidationTests {
    [Fact]
    public async Task Build_WhenAuthorizationNotConfigured_Succeeds() {
        var options = new BrokerOptions();

        var broker = BrokerBuilder.Create()
            .ConfigureFrom(options)
            .Build();

        await broker.DisposeAsync();
    }

    [Fact]
    public async Task Build_WhenAuthorizationConfigured_Succeeds() {
        var dbPath = Path.GetTempFileName();
        try {
            var options = new BrokerOptions {
                Authorization = new AuthorizationOptions {
                    SuperuserUsername = "admin",
                    SuperuserPassword = "password-123",
                    DatabasePath = dbPath
                }
            };

            var broker = BrokerBuilder.Create()
                .ConfigureFrom(options)
                .Build();

            await broker.DisposeAsync();
        } finally {
            if (File.Exists(dbPath)) {
                File.Delete(dbPath);
            }
        }
    }
}
