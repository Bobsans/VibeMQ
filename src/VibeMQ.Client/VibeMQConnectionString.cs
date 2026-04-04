using System.Diagnostics.CodeAnalysis;
using VibeMQ.Client.Exceptions;
using VibeMQ.Configuration;
using VibeMQ.Protocol.Compression;

namespace VibeMQ.Client;

/// <summary>
/// Parsed result of a VibeMQ connection string. Use <see cref="Parse"/> or <see cref="TryParse"/> to create.
/// </summary>
/// <param name="Host">Broker host.</param>
/// <param name="Port">Broker port.</param>
/// <param name="Options">Client options (auth, TLS, compression, reconnect, etc.).</param>
public sealed record VibeMQConnectionString(string Host, int Port, ClientOptions Options) {
    private const string SCHEME_PREFIX = "vibemq://";
    private const int DEFAULT_PORT = 2925;

    /// <summary>
    /// Parses a connection string (URL-style or key=value) and returns the result.
    /// </summary>
    /// <param name="connectionString">Connection string. URL: <c>vibemq://[user:password@]host[:port][?query]</c>. Key=value: <c>Host=...;Port=...;...</c>.</param>
    /// <returns>Parsed host, port, and client options.</returns>
    /// <exception cref="VibeMQConnectionStringException">The string is null, empty, or invalid.</exception>
    public static VibeMQConnectionString Parse(string? connectionString) {
        if (string.IsNullOrWhiteSpace(connectionString)) {
            throw new VibeMQConnectionStringException("Connection string cannot be null or empty.", nameof(connectionString));
        }

        var s = connectionString.Trim();
        return s.StartsWith(SCHEME_PREFIX, StringComparison.OrdinalIgnoreCase) ? ParseUrl(s) : ParseKeyValue(s);
    }

    /// <summary>
    /// Tries to parse a connection string.
    /// </summary>
    /// <param name="connectionString">Connection string to parse.</param>
    /// <param name="result">Parsed result, or null when parsing fails.</param>
    /// <returns>True if parsing succeeded; otherwise false.</returns>
    public static bool TryParse(string? connectionString, [NotNullWhen(true)] out VibeMQConnectionString? result) {
        result = null;
        if (string.IsNullOrWhiteSpace(connectionString)) {
            return false;
        }

        try {
            result = Parse(connectionString);
            return true;
        } catch (VibeMQConnectionStringException) {
            return false;
        }
    }

    private static VibeMQConnectionString ParseUrl(string s) {
        if (!Uri.TryCreate(s, UriKind.Absolute, out var uri) || !uri.Scheme.Equals("vibemq", StringComparison.OrdinalIgnoreCase)) {
            throw new VibeMQConnectionStringException("Invalid VibeMQ URL format. Expected: vibemq://[user:password@]host[:port][?query]", nameof(s));
        }

        var host = uri.Host;
        if (string.IsNullOrEmpty(host)) {
            throw new VibeMQConnectionStringException("URL must specify a host.", nameof(s));
        }

        var port = uri.Port;
        if (port is <= 0 or > 65535) {
            port = DEFAULT_PORT;
        }

        var options = new ClientOptions();
        if (!string.IsNullOrEmpty(uri.UserInfo)) {
            ParseUserInfo(uri.UserInfo, options);
        }

        if (!string.IsNullOrEmpty(uri.Query)) {
            ApplyQueryToOptions(uri.Query.TrimStart('?'), options);
        }

        return new VibeMQConnectionString(host, port, options);
    }

    private static void ParseUserInfo(string userInfo, ClientOptions options) {
        var colon = userInfo.IndexOf(':');
        if (colon >= 0) {
            options.Username = Uri.UnescapeDataString(userInfo[..colon]);
            options.Password = Uri.UnescapeDataString(userInfo[(colon + 1)..]);
        } else {
            options.Username = Uri.UnescapeDataString(userInfo);
        }
    }

    private static void ApplyQueryToOptions(string query, ClientOptions options) {
        foreach (var pair in query.Split('&')) {
            var segment = pair.Trim();
            if (segment.Length == 0) {
                continue;
            }

            var eq = segment.IndexOf('=');
            if (eq < 0) {
                throw new VibeMQConnectionStringException($"Invalid query parameter: '{segment}'.");
            }

            var key = segment[..eq].Trim();
            var value = segment[(eq + 1)..].Trim();
            value = Uri.UnescapeDataString(value);
            ApplyOption(key, value, options);
        }
    }

    private static VibeMQConnectionString ParseKeyValue(string s) {
        var options = new ClientOptions();
        string? host = null;
        int? port = null;

        foreach (var (key, value) in SplitKeyValue(s)) {
            var k = key.Trim().ToLowerInvariant();
            var v = value?.Trim() ?? string.Empty;

            switch (k) {
                case "host":
                    host = v;
                    break;
                case "port":
                    if (int.TryParse(v, out var p) && p is > 0 and <= 65535) {
                        port = p;
                    } else {
                        throw new VibeMQConnectionStringException($"Invalid Port value: '{value}'.");
                    }

                    break;
                case "username":
                    options.Username = v;
                    break;
                case "password":
                    options.Password = v;
                    break;
                default:
                    ApplyOption(k, v, options);
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(host)) {
            host = "localhost";
        }

        return new VibeMQConnectionString(host, port ?? DEFAULT_PORT, options);
    }

    private static IEnumerable<(string key, string? value)> SplitKeyValue(string s) {
        var i = 0;
        while (i < s.Length) {
            var start = i;
            while (i < s.Length && s[i] != ';') {
                if (s[i] == '"') {
                    i++;
                    while (i < s.Length && (s[i] != '"' || (i > 0 && s[i - 1] == '\\'))) {
                        i++;
                    }

                    if (i < s.Length) {
                        i++;
                    }
                } else {
                    i++;
                }
            }

            var segment = s[start..i].Trim();
            i++;
            if (segment.Length == 0) {
                continue;
            }

            var eq = segment.IndexOf('=');
            if (eq < 0) {
                yield return (segment, null);
                continue;
            }

            var key = segment[..eq].Trim();
            var value = segment[(eq + 1)..];
            if (value.Length >= 2 && value.StartsWith('"') && value.EndsWith('"')) {
                value = value[1..^1].Replace("\\\"", "\"").Replace(@"\\", "\\").Replace("\\;", ";").Replace("\\=", "=");
            }

            yield return (key, value);
        }
    }

    private static void ApplyOption(string key, string value, ClientOptions options) {
        var k = key.ToLowerInvariant();

        switch (k) {
            case "tls":
            case "usetls":
                options.UseTls = ParseBool(value);
                break;
            case "skipcertvalidation":
                options.SkipCertificateValidation = ParseBool(value);
                break;
            case "keepalive":
            case "keepaliveinterval":
                if (int.TryParse(value, out var keepAliveSec) && keepAliveSec > 0) {
                    options.KeepAliveInterval = TimeSpan.FromSeconds(keepAliveSec);
                }

                break;
            case "commandtimeout":
                if (int.TryParse(value, out var timeoutSec) && timeoutSec > 0) {
                    options.CommandTimeout = TimeSpan.FromSeconds(timeoutSec);
                }

                break;
            case "compression":
                options.PreferredCompressions = ParseCompression(value);
                break;
            case "compressionthreshold":
                if (int.TryParse(value, out var threshold) && threshold >= 0) {
                    options.CompressionThreshold = threshold;
                }

                break;
            case "reconnectmaxattempts":
                if (int.TryParse(value, out var maxAttempts)) {
                    options.ReconnectPolicy.MaxAttempts = maxAttempts <= 0 ? int.MaxValue : maxAttempts;
                }

                break;
            case "reconnectinitialdelay":
                if (int.TryParse(value, out var initialSec) && initialSec >= 0) {
                    options.ReconnectPolicy.InitialDelay = TimeSpan.FromSeconds(initialSec);
                }

                break;
            case "reconnectmaxdelay":
                if (int.TryParse(value, out var maxSec) && maxSec >= 0) {
                    options.ReconnectPolicy.MaxDelay = TimeSpan.FromSeconds(maxSec);
                }

                break;
            case "reconnectexponentialbackoff":
                options.ReconnectPolicy.UseExponentialBackoff = ParseBool(value);
                break;
            case "queues":
                foreach (var name in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
                    if (name.Length > 0) {
                        options.QueueDeclarations.Add(new QueueDeclaration {
                            QueueName = name,
                            Options = new QueueOptions(),
                            OnConflict = QueueConflictResolution.Ignore,
                            FailOnProvisioningError = true
                        });
                    }
                }

                break;
        }
    }

    private static bool ParseBool(string v) {
        return v.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
            || v.Trim().Equals("1", StringComparison.OrdinalIgnoreCase)
            || v.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static List<CompressionAlgorithm> ParseCompression(string value) {
        var v = value.Trim();
        if (string.IsNullOrEmpty(v) || v.Equals("none", StringComparison.OrdinalIgnoreCase)) {
            return [];
        }

        var list = new List<CompressionAlgorithm>();
        foreach (var part in v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
            var algo = CompressorFactory.Parse(part);
            if (algo.HasValue && algo.Value != CompressionAlgorithm.None) {
                list.Add(algo.Value);
            }
        }

        return list;
    }
}
