using System.Net;
using System.Net.Sockets;
using VibeMQ.Client;
using VibeMQ.Enums;
using VibeMQ.Server;
using VibeMQ.Server.Auth;

namespace VibeMQ.Tests.Integration;

/// <summary>
/// Shared test fixture that starts a broker with username/password authorization
/// on a random port for authorization integration tests.
/// </summary>
public sealed class AuthBrokerFixture : IAsyncLifetime {
    private const string SUPERUSER = "admin";
    private const string SUPERUSER_PASSWORD = "AdminP@ss-Tests-123";

    private BrokerServer? _server;
    private Task? _serverTask;
    private CancellationTokenSource? _cts;
    private string? _dbPath;

    public int Port { get; private set; }
    public static string SuperuserUsername => SUPERUSER;
    public static string SuperuserPassword => SUPERUSER_PASSWORD;

    /// <summary>
    /// Direct repository access for setting up test users and permissions.
    /// Available after <see cref="InitializeAsync"/> completes.
    /// </summary>
    public IAuthRepository Repository { get; private set; } = null!;

    public async Task InitializeAsync() {
        Port = GetFreePort();
        _dbPath = Path.GetTempFileName();
        _cts = new CancellationTokenSource();

        _server = BrokerBuilder.Create()
            .UsePort(Port)
            .UseAuthorization(o => {
                o.SuperuserUsername = SUPERUSER;
                o.SuperuserPassword = SUPERUSER_PASSWORD;
                o.DatabasePath = _dbPath;
            })
            .UseMaxConnections(100)
            .ConfigureRateLimiting(o => o.Enabled = false)
            .Build();

        _serverTask = _server.RunAsync(_cts.Token);

        // Wait until the server is actually listening (this also runs AuthBootstrapper)
        await WaitForPortAsync(Port, _serverTask, TimeSpan.FromSeconds(10));

        // Direct repository access for test user/permission setup
        Repository = new SqliteAuthRepository(_dbPath);
    }

    /// <summary>
    /// Creates a test user in the auth database with the given credentials.
    /// </summary>
    public async Task CreateUserAsync(string username, string password) {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var record = new VibeMQ.Server.Auth.Models.UserRecord {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 4), // Low cost for tests
            IsSuperuser = false,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await Repository.CreateUserAsync(record);
    }

    /// <summary>
    /// Grants the specified operations on a queue pattern to a user.
    /// </summary>
    public async Task GrantPermissionAsync(string username, string queuePattern, QueueOperation[] operations) {
        await Repository.GrantPermissionAsync(username, queuePattern, operations);
    }

    /// <summary>Connects as the superuser.</summary>
    public Task<VibeMQClient> ConnectAsSuperuserAsync() =>
        ConnectAsync(SuperuserUsername, SuperuserPassword);

    /// <summary>Connects as the specified user.</summary>
    public async Task<VibeMQClient> ConnectAsync(string username, string password) {
        var options = new ClientOptions {
            Username = username,
            Password = password,
            CommandTimeout = TimeSpan.FromSeconds(5),
            ReconnectPolicy = new ReconnectPolicy { MaxAttempts = 0 },
        };

        return await VibeMQClient.ConnectAsync("127.0.0.1", Port, options);
    }

    public async Task DisposeAsync() {
        if (_cts is not null) {
            await _cts.CancelAsync();
        }

        if (_serverTask is not null) {
            try {
                await _serverTask.WaitAsync(TimeSpan.FromSeconds(5));
            } catch {
                // Server task may throw on shutdown
            }
        }

        if (_server is not null) {
            try {
                await _server.DisposeAsync();
            } catch {
                // Ignore dispose errors during test cleanup
            }
        }

        _cts?.Dispose();

        if (_dbPath is not null && File.Exists(_dbPath)) {
            try { File.Delete(_dbPath); } catch { /* best effort */ }
        }
    }

    private static int GetFreePort() {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task WaitForPortAsync(int port, Task serverTask, TimeSpan timeout) {
        using var cts = new CancellationTokenSource(timeout);

        while (!cts.Token.IsCancellationRequested) {
            if (serverTask.IsCompleted) {
                await serverTask; // rethrows any exception
            }

            try {
                using var tcp = new TcpClient();
                await tcp.ConnectAsync("127.0.0.1", port, cts.Token);
                return;
            } catch (SocketException) {
                await Task.Delay(50, cts.Token);
            }
        }

        throw new TimeoutException($"Server did not start on port {port} within {timeout.TotalSeconds}s.");
    }
}
