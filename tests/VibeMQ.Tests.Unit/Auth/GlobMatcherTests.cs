using VibeMQ.Server.Auth;

namespace VibeMQ.Tests.Unit.Auth;

public class GlobMatcherTests {
    [Theory]
    [InlineData("orders", "orders", true)]
    [InlineData("orders", "Orders", true)]            // case-insensitive
    [InlineData("orders", "invoices", false)]
    [InlineData("*", "orders", true)]                 // wildcard matches any name
    [InlineData("*", "orders.events", true)]          // wildcard matches dots too
    [InlineData("*", "", true)]                       // wildcard matches empty
    [InlineData("orders.*", "orders.events", true)]
    [InlineData("orders.*", "orders.test.deep", true)]
    [InlineData("orders.*", "orders", false)]         // no suffix → no match
    [InlineData("*.events", "orders.events", true)]
    [InlineData("*.events", "Orders.Events", true)]   // case-insensitive with wildcards
    [InlineData("*.events", "orders.test", false)]
    [InlineData("tenant.*.events", "tenant.alice.events", true)]
    [InlineData("tenant.*.events", "tenant.bob.events", true)]
    [InlineData("tenant.*.events", "tenant.alice.logs", false)]
    [InlineData("tenant.*.events", "other.alice.events", false)]
    [InlineData("q*", "queue", true)]
    [InlineData("q*", "q", true)]
    [InlineData("q*", "other", false)]
    public void IsMatch_VariousPatterns_ReturnsExpected(string pattern, string queueName, bool expected) {
        var result = GlobMatcher.IsMatch(queueName, pattern);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsMatch_CalledTwiceWithSameArgs_ReturnsSameResult() {
        // Verifies caching doesn't break correctness
        const string pattern = "cached.*";
        const string queue = "cached.test";

        var first = GlobMatcher.IsMatch(queue, pattern);
        var second = GlobMatcher.IsMatch(queue, pattern);

        Assert.True(first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void IsMatch_MultiplePatternsInParallel_AllCorrect() {
        var cases = new (string Pattern, string Queue, bool Expected)[] {
            ("a.*", "a.b", true),
            ("a.*", "b.c", false),
            ("*", "anything", true),
            ("exact", "exact", true),
            ("exact", "other", false),
        };

        // Run in parallel to exercise the concurrent cache
        var results = cases.AsParallel()
            .Select(c => (c.Expected, Actual: GlobMatcher.IsMatch(c.Queue, c.Pattern)))
            .ToArray();

        foreach (var (expected, actual) in results) {
            Assert.Equal(expected, actual);
        }
    }

    [Theory]
    [InlineData("a+b", "a+b", true)]       // regex-special chars are escaped
    [InlineData("a+b", "ab", false)]
    [InlineData("a.b", "a.b", true)]       // literal dot in pattern
    [InlineData("a.b", "axb", false)]      // dot in pattern is NOT a regex wildcard
    public void IsMatch_RegexSpecialCharsInPattern_EscapedCorrectly(string pattern, string queueName, bool expected) {
        var result = GlobMatcher.IsMatch(queueName, pattern);

        Assert.Equal(expected, result);
    }
}
