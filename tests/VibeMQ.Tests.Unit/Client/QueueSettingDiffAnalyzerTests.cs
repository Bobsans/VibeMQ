using VibeMQ.Client;
using VibeMQ.Configuration;
using VibeMQ.Enums;
using VibeMQ.Models;

namespace VibeMQ.Tests.Unit.Client;

public class QueueSettingDiffAnalyzerTests {
    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────

    private static QueueInfo Existing(
        DeliveryMode mode = DeliveryMode.RoundRobin,
        int maxSize = 10_000,
        TimeSpan? messageTtl = null,
        bool enableDlq = false,
        string? dlqName = null,
        OverflowStrategy overflow = OverflowStrategy.DropOldest,
        int maxRetry = 3
    ) => new QueueInfo {
        Name = "test",
        MessageCount = 0,
        SubscriberCount = 0,
        DeliveryMode = mode,
        MaxSize = maxSize,
        CreatedAt = DateTime.UtcNow,
        MessageTtl = messageTtl,
        EnableDeadLetterQueue = enableDlq,
        DeadLetterQueueName = dlqName,
        OverflowStrategy = overflow,
        MaxRetryAttempts = maxRetry,
    };

    private static QueueOptions Declared(
        DeliveryMode mode = DeliveryMode.RoundRobin,
        int maxSize = 10_000,
        TimeSpan? messageTtl = null,
        bool enableDlq = false,
        string? dlqName = null,
        OverflowStrategy overflow = OverflowStrategy.DropOldest,
        int maxRetry = 3
    ) => new QueueOptions {
        Mode = mode,
        MaxQueueSize = maxSize,
        MessageTtl = messageTtl,
        EnableDeadLetterQueue = enableDlq,
        DeadLetterQueueName = dlqName,
        OverflowStrategy = overflow,
        MaxRetryAttempts = maxRetry,
    };

    // ──────────────────────────────────────────────────────────────
    // No differences
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void Analyze_NoChanges_ReturnsEmpty() {
        var diffs = QueueSettingDiffAnalyzer.Analyze(Declared(), Existing());
        Assert.Empty(diffs);
    }

    // ──────────────────────────────────────────────────────────────
    // Mode
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void Analyze_ModeChanged_ReturnsHard() {
        var diffs = QueueSettingDiffAnalyzer.Analyze(
            Declared(mode: DeliveryMode.FanOutWithAck),
            Existing(mode: DeliveryMode.RoundRobin)
        );

        var diff = Assert.Single(diffs);
        Assert.Equal("Mode", diff.SettingName);
        Assert.Equal(ConflictSeverity.Hard, diff.Severity);
        Assert.Equal(DeliveryMode.RoundRobin, diff.ExistingValue);
        Assert.Equal(DeliveryMode.FanOutWithAck, diff.DeclaredValue);
    }

    [Fact]
    public void Analyze_ModeSame_NoDiff() {
        var diffs = QueueSettingDiffAnalyzer.Analyze(
            Declared(mode: DeliveryMode.FanOutWithAck),
            Existing(mode: DeliveryMode.FanOutWithAck)
        );

        Assert.DoesNotContain(diffs, d => d.SettingName == "Mode");
    }

    // ──────────────────────────────────────────────────────────────
    // MaxQueueSize
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void Analyze_MaxQueueSizeIncreased_ReturnsInfo() {
        var diffs = QueueSettingDiffAnalyzer.Analyze(
            Declared(maxSize: 20_000),
            Existing(maxSize: 10_000)
        );

        var diff = Assert.Single(diffs);
        Assert.Equal("MaxQueueSize", diff.SettingName);
        Assert.Equal(ConflictSeverity.Info, diff.Severity);
    }

    [Fact]
    public void Analyze_MaxQueueSizeDecreased_ReturnsInfo() {
        var diffs = QueueSettingDiffAnalyzer.Analyze(
            Declared(maxSize: 5_000),
            Existing(maxSize: 10_000)
        );

        var diff = Assert.Single(diffs);
        Assert.Equal("MaxQueueSize", diff.SettingName);
        Assert.Equal(ConflictSeverity.Info, diff.Severity);
    }

    // ──────────────────────────────────────────────────────────────
    // MessageTtl
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void Analyze_MessageTtlNullToValue_ReturnsSoft() {
        var diffs = QueueSettingDiffAnalyzer.Analyze(
            Declared(messageTtl: TimeSpan.FromHours(1)),
            Existing(messageTtl: null)
        );

        var diff = Assert.Single(diffs);
        Assert.Equal("MessageTtl", diff.SettingName);
        Assert.Equal(ConflictSeverity.Soft, diff.Severity);
    }

    [Fact]
    public void Analyze_MessageTtlValueToNull_ReturnsInfo() {
        var diffs = QueueSettingDiffAnalyzer.Analyze(
            Declared(messageTtl: null),
            Existing(messageTtl: TimeSpan.FromHours(1))
        );

        var diff = Assert.Single(diffs);
        Assert.Equal("MessageTtl", diff.SettingName);
        Assert.Equal(ConflictSeverity.Info, diff.Severity);
    }

    [Fact]
    public void Analyze_MessageTtlDecreased_ReturnsSoft() {
        var diffs = QueueSettingDiffAnalyzer.Analyze(
            Declared(messageTtl: TimeSpan.FromMinutes(30)),
            Existing(messageTtl: TimeSpan.FromHours(1))
        );

        var diff = Assert.Single(diffs);
        Assert.Equal("MessageTtl", diff.SettingName);
        Assert.Equal(ConflictSeverity.Soft, diff.Severity);
    }

    [Fact]
    public void Analyze_MessageTtlIncreased_ReturnsInfo() {
        var diffs = QueueSettingDiffAnalyzer.Analyze(
            Declared(messageTtl: TimeSpan.FromHours(2)),
            Existing(messageTtl: TimeSpan.FromHours(1))
        );

        var diff = Assert.Single(diffs);
        Assert.Equal("MessageTtl", diff.SettingName);
        Assert.Equal(ConflictSeverity.Info, diff.Severity);
    }

    // ──────────────────────────────────────────────────────────────
    // EnableDeadLetterQueue
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void Analyze_EnableDlqFalseToTrue_ReturnsInfo() {
        var diffs = QueueSettingDiffAnalyzer.Analyze(
            Declared(enableDlq: true),
            Existing(enableDlq: false)
        );

        var diff = diffs.Single(d => d.SettingName == "EnableDeadLetterQueue");
        Assert.Equal(ConflictSeverity.Info, diff.Severity);
    }

    [Fact]
    public void Analyze_EnableDlqTrueToFalse_ReturnsSoft() {
        var diffs = QueueSettingDiffAnalyzer.Analyze(
            Declared(enableDlq: false),
            Existing(enableDlq: true)
        );

        var diff = diffs.Single(d => d.SettingName == "EnableDeadLetterQueue");
        Assert.Equal(ConflictSeverity.Soft, diff.Severity);
    }

    // ──────────────────────────────────────────────────────────────
    // DeadLetterQueueName
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void Analyze_DlqNameChanged_WhenDlqEnabledOnBothSides_ReturnsHard() {
        var diffs = QueueSettingDiffAnalyzer.Analyze(
            Declared(enableDlq: true, dlqName: "new-dlq"),
            Existing(enableDlq: true, dlqName: "old-dlq")
        );

        var diff = diffs.Single(d => d.SettingName == "DeadLetterQueueName");
        Assert.Equal(ConflictSeverity.Hard, diff.Severity);
    }

    [Fact]
    public void Analyze_DlqNameChanged_WhenDlqDisabledOnExisting_NoDlqNameDiff() {
        // DLQ not enabled on the existing queue — name comparison is skipped
        var diffs = QueueSettingDiffAnalyzer.Analyze(
            Declared(enableDlq: true, dlqName: "new-dlq"),
            Existing(enableDlq: false, dlqName: null)
        );

        Assert.DoesNotContain(diffs, d => d.SettingName == "DeadLetterQueueName");
    }

    [Fact]
    public void Analyze_DlqNameChanged_WhenDlqDisabledOnDeclared_NoDlqNameDiff() {
        // DLQ disabled in declared options — name comparison is skipped
        var diffs = QueueSettingDiffAnalyzer.Analyze(
            Declared(enableDlq: false, dlqName: null),
            Existing(enableDlq: true, dlqName: "old-dlq")
        );

        Assert.DoesNotContain(diffs, d => d.SettingName == "DeadLetterQueueName");
    }

    // ──────────────────────────────────────────────────────────────
    // OverflowStrategy
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void Analyze_OverflowStrategyToDropNewest_ReturnsInfo() {
        var diffs = QueueSettingDiffAnalyzer.Analyze(
            Declared(overflow: OverflowStrategy.DropNewest),
            Existing(overflow: OverflowStrategy.DropOldest)
        );

        var diff = Assert.Single(diffs);
        Assert.Equal("OverflowStrategy", diff.SettingName);
        Assert.Equal(ConflictSeverity.Info, diff.Severity);
    }

    [Fact]
    public void Analyze_OverflowStrategyToRedirectToDlq_WhenExistingDlqEnabled_ReturnsInfo() {
        var diffs = QueueSettingDiffAnalyzer.Analyze(
            Declared(overflow: OverflowStrategy.RedirectToDlq, enableDlq: true),
            Existing(overflow: OverflowStrategy.DropOldest, enableDlq: true)
        );

        var diff = diffs.Single(d => d.SettingName == "OverflowStrategy");
        Assert.Equal(ConflictSeverity.Info, diff.Severity);
    }

    [Fact]
    public void Analyze_OverflowStrategyToRedirectToDlq_WhenExistingDlqDisabled_ReturnsHard() {
        var diffs = QueueSettingDiffAnalyzer.Analyze(
            Declared(overflow: OverflowStrategy.RedirectToDlq, enableDlq: true),
            Existing(overflow: OverflowStrategy.DropOldest, enableDlq: false)
        );

        var diff = diffs.Single(d => d.SettingName == "OverflowStrategy");
        Assert.Equal(ConflictSeverity.Hard, diff.Severity);
    }

    [Fact]
    public void Analyze_OverflowStrategySame_NoDiff() {
        var diffs = QueueSettingDiffAnalyzer.Analyze(Declared(), Existing());
        Assert.DoesNotContain(diffs, d => d.SettingName == "OverflowStrategy");
    }

    // ──────────────────────────────────────────────────────────────
    // MaxRetryAttempts
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void Analyze_MaxRetryAttemptsChanged_ReturnsInfo() {
        var diffs = QueueSettingDiffAnalyzer.Analyze(
            Declared(maxRetry: 5),
            Existing(maxRetry: 3)
        );

        var diff = Assert.Single(diffs);
        Assert.Equal("MaxRetryAttempts", diff.SettingName);
        Assert.Equal(ConflictSeverity.Info, diff.Severity);
    }

    // ──────────────────────────────────────────────────────────────
    // Multi-diff
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void Analyze_ModeAndTtlChanged_ReturnsBothDiffs() {
        var diffs = QueueSettingDiffAnalyzer.Analyze(
            Declared(mode: DeliveryMode.FanOutWithAck, messageTtl: TimeSpan.FromHours(1)),
            Existing(mode: DeliveryMode.RoundRobin, messageTtl: null)
        );

        Assert.Equal(2, diffs.Count);
        Assert.Contains(diffs, d => d.SettingName == "Mode" && d.Severity == ConflictSeverity.Hard);
        Assert.Contains(diffs, d => d.SettingName == "MessageTtl" && d.Severity == ConflictSeverity.Soft);
    }
}

