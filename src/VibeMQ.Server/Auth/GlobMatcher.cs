using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace VibeMQ.Server.Auth;

/// <summary>
/// Matches queue names against glob patterns.
/// <list type="bullet">
///   <item><description><c>*</c> matches any sequence of characters, including dots.</description></item>
///   <item><description>All other characters are matched literally.</description></item>
///   <item><description>Multiple matching patterns produce a <em>union</em> of allowed operations.</description></item>
/// </list>
/// Compiled regexes are cached per pattern for performance.
/// </summary>
public static class GlobMatcher {
    private static readonly ConcurrentDictionary<string, Regex> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="input"/> matches <paramref name="pattern"/>.
    /// </summary>
    public static bool IsMatch(string input, string pattern) {
        var regex = _cache.GetOrAdd(pattern, BuildRegex);
        return regex.IsMatch(input);
    }

    private static Regex BuildRegex(string pattern) {
        // Escape all regex meta-chars, then replace the escaped '*' back to '.*'
        var escaped = Regex.Escape(pattern).Replace(@"\*", ".*");
        return new Regex(
            $"^{escaped}$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100)
        );
    }
}
