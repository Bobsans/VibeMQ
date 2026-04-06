using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VibeMQ.Configuration;
using VibeMQ.Server;
using VibeMQ.Server.DependencyInjection;
using VibeMQ.Server.Storage.Sqlite;
using VibeMQ.Server.WebUI;

// Optional config path (e.g. in Docker: /app/config/appsettings.json)
var configPath = Environment.GetEnvironmentVariable("VIBEMQ_CONFIG_PATH");
var configPaths = new List<string> { "appsettings.json" };
if (!string.IsNullOrWhiteSpace(configPath)) {
    configPaths.Insert(0, configPath.Trim());
}

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) => {
        foreach (var path in configPaths.Distinct()) {
            config.AddJsonFile(path, optional: true, reloadOnChange: false);
        }
    })
    .ConfigureServices((context, services) => {
        var configuration = context.Configuration;

        services.Configure<BrokerOptions>(configuration.GetSection("VibeMQ"));
        services.Configure<WebUIOptions>(configuration.GetSection("VibeMQ:WebUI"));

        var storageTypeStr = configuration["VibeMQ:StorageType"];
        if (string.Equals(storageTypeStr, "Sqlite", StringComparison.OrdinalIgnoreCase)) {
            services.AddVibeMQSqliteStorage(opts => {
                configuration.GetSection("VibeMQ:SqliteStorage").Bind(opts);
            });
        }

        services.AddVibeMQBroker();
    })
    .ConfigureLogging((context, logging) => {
        var level = context.Configuration["Logging:LogLevel:Default"];
        if (!string.IsNullOrEmpty(level) && Enum.TryParse<LogLevel>(level, ignoreCase: true, out var logLevel)) {
            logging.SetMinimumLevel(logLevel);
        }
    })
    .Build();

var brokerOptions = host.Services.GetRequiredService<IOptions<BrokerOptions>>().Value;
var webUIOptions = host.Services.GetRequiredService<IOptions<WebUIOptions>>().Value;
var authEnabled = brokerOptions.Authorization is not null;
Console.WriteLine("VibeMQ broker + Web UI starting. Broker port: {0}, Web UI port: {1}, Auth: {2}",
    brokerOptions.Port, webUIOptions.Port, authEnabled);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => {
    cts.Cancel();
    e.Cancel = true;
};

var broker = host.Services.GetRequiredService<BrokerServer>();
await broker.RunWithWebUIAsync(webUIOptions, cts.Token);
