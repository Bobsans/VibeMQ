using System.Collections.Concurrent;

namespace VibeMQ.Server.Auth;

/// <summary>
/// Matches queue names against glob patterns.
/// <list type="bullet">
///   <item><description><c>*</c> matches any sequence of characters, including dots.</description></item>
///   <item><description>All other characters are matched literally.</description></item>
///   <item><description>Multiple matching patterns produce a <em>union</em> of allowed operations.</description></item>
/// </list>
/// Uses a simple character-based matcher instead of regex to avoid ReDoS.
/// Compiled matchers are cached per pattern for performance.
/// </summary>
public static class GlobMatcher {
    private const int MAX_PATTERN_LENGTH = 256;
    private const int MAX_CACHE_SIZE = 1024;
    private static readonly ConcurrentDictionary<string, Func<string, bool>> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="input"/> matches <paramref name="pattern"/>.
    /// </summary>
    public static bool IsMatch(string input, string pattern) {
        if (pattern.Length > MAX_PATTERN_LENGTH) {
            throw new ArgumentException($"Glob pattern exceeds maximum length ({MAX_PATTERN_LENGTH}).");
        }

        if (_cache.TryGetValue(pattern, out var cached)) {
            return cached(input);
        }

        var matcher = BuildMatcher(pattern);

        // Prevent unbounded growth from user-supplied patterns
        if (_cache.Count < MAX_CACHE_SIZE) {
            _cache.TryAdd(pattern, matcher);
        }

        return matcher(input);
    }

    private static Func<string, bool> BuildMatcher(string pattern) {
        // Fast path: literal match (no wildcards)
        if (!pattern.Contains('*')) {
            return input => string.Equals(input, pattern, StringComparison.OrdinalIgnoreCase);
        }

        // Fast path: match-all
        if (pattern == "*") {
            return _ => true;
        }

        // General case: character-based glob matching
        return input => GlobMatch(input, pattern);
    }

    private static bool GlobMatch(string input, string pattern) {
        int iIdx = 0, pIdx = 0;
        int starIdx = -1, matchIdx = 0;

        while (iIdx < input.Length) {
            if (pIdx < pattern.Length && char.ToLowerInvariant(pattern[pIdx]) == char.ToLowerInvariant(input[iIdx])) {
                iIdx++;
                pIdx++;
            } else if (pIdx < pattern.Length && pattern[pIdx] == '*') {
                starIdx = pIdx;
                matchIdx = iIdx;
                pIdx++;
            } else if (starIdx >= 0) {
                pIdx = starIdx + 1;
                matchIdx++;
                iIdx = matchIdx;
            } else {
                return false;
            }
        }

        while (pIdx < pattern.Length && pattern[pIdx] == '*') {
            pIdx++;
        }

        return pIdx == pattern.Length;
    }
}
