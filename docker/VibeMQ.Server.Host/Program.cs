using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VibeMQ.Configuration;
using VibeMQ.Server.DependencyInjection;
using VibeMQ.Server.Storage.Sqlite;

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

        // Bind broker options from "VibeMQ" section (env vars: VibeMQ__Port, VibeMQ__Authorization__SuperuserPassword, etc.)
        services.Configure<BrokerOptions>(configuration.GetSection("VibeMQ"));

        // When StorageType is Sqlite, register SQLite storage from "VibeMQ:SqliteStorage" section
        var storageTypeStr = configuration["VibeMQ:StorageType"];
        if (string.Equals(storageTypeStr, "Sqlite", StringComparison.OrdinalIgnoreCase)) {
            services.AddVibeMQSqliteStorage(opts => {
                configuration.GetSection("VibeMQ:SqliteStorage").Bind(opts);
            });
        }

        services.AddVibeMQBroker();
    })
    .ConfigureLogging((context, logging) => {
        // Allow override from config (e.g. Logging:LogLevel:Default) or env
        var level = context.Configuration["Logging:LogLevel:Default"];
        if (!string.IsNullOrEmpty(level) && Enum.TryParse<LogLevel>(level, ignoreCase: true, out var logLevel)) {
            logging.SetMinimumLevel(logLevel);
        }
    })
    .Build();

var options = host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<BrokerOptions>>().Value;
Console.WriteLine("VibeMQ broker starting. Port: {0}, Auth: {1}", options.Port, options.EnableAuthentication);

await host.RunAsync();
