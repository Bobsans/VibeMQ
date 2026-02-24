using VibeMQ.Configuration;
using VibeMQ.Enums;
using VibeMQ.Models;

namespace VibeMQ.Client;

/// <summary>
/// Compares a declared <see cref="QueueOptions"/> against an existing <see cref="QueueInfo"/>
/// and produces a list of setting differences with their conflict severity.
/// </summary>
public static class QueueSettingDiffAnalyzer {
    /// <summary>
    /// Compares <paramref name="declared"/> against <paramref name="existing"/> and returns
    /// all settings that differ along with their <see cref="ConflictSeverity"/>.
    /// Only settings that actually differ are included in the result.
    /// </summary>
    public static IReadOnlyList<QueueSettingDiff> Analyze(QueueOptions declared, QueueInfo existing) {
        var diffs = new List<QueueSettingDiff>();

        // Mode: any change → Hard
        if (declared.Mode != existing.DeliveryMode) {
            diffs.Add(new QueueSettingDiff("Mode", existing.DeliveryMode, declared.Mode, ConflictSeverity.Hard));
        }

        // MaxQueueSize: any direction → Info
        if (declared.MaxQueueSize != existing.MaxSize) {
            diffs.Add(new QueueSettingDiff("MaxQueueSize", existing.MaxSize, declared.MaxQueueSize, ConflictSeverity.Info));
        }

        // MessageTtl: direction-aware
        if (declared.MessageTtl != existing.MessageTtl) {
            var severity = ClassifyMessageTtlChange(existing.MessageTtl, declared.MessageTtl);
            diffs.Add(new QueueSettingDiff("MessageTtl", existing.MessageTtl, declared.MessageTtl, severity));
        }

        // EnableDeadLetterQueue: direction-aware
        if (declared.EnableDeadLetterQueue != existing.EnableDeadLetterQueue) {
            var severity = declared.EnableDeadLetterQueue
                ? ConflictSeverity.Info   // false → true: additive
                : ConflictSeverity.Soft;  // true → false: in-flight retries will be dropped
            diffs.Add(new QueueSettingDiff("EnableDeadLetterQueue", existing.EnableDeadLetterQueue, declared.EnableDeadLetterQueue, severity));
        }

        // DeadLetterQueueName: Hard if DLQ is enabled on both sides and names differ
        if (declared.EnableDeadLetterQueue && existing.EnableDeadLetterQueue &&
            declared.DeadLetterQueueName != existing.DeadLetterQueueName) {
            diffs.Add(new QueueSettingDiff("DeadLetterQueueName", existing.DeadLetterQueueName, declared.DeadLetterQueueName, ConflictSeverity.Hard));
        }

        // OverflowStrategy: direction and cross-parameter aware
        if (declared.OverflowStrategy != existing.OverflowStrategy) {
            var severity = ClassifyOverflowStrategyChange(declared, existing);
            diffs.Add(new QueueSettingDiff("OverflowStrategy", existing.OverflowStrategy, declared.OverflowStrategy, severity));
        }

        // MaxRetryAttempts: any change → Info
        if (declared.MaxRetryAttempts != existing.MaxRetryAttempts) {
            diffs.Add(new QueueSettingDiff("MaxRetryAttempts", existing.MaxRetryAttempts, declared.MaxRetryAttempts, ConflictSeverity.Info));
        }

        return diffs;
    }

    private static ConflictSeverity ClassifyMessageTtlChange(TimeSpan? existing, TimeSpan? declared) {
        // null → value: messages that already exist may expire immediately → Soft
        if (existing is null && declared is not null) {
            return ConflictSeverity.Soft;
        }

        // value → null: messages live longer, additive → Info
        if (existing is not null && declared is null) {
            return ConflictSeverity.Info;
        }

        // Both non-null: compare durations
        if (existing is not null && declared is not null) {
            // Decrease: existing messages may expire sooner → Soft
            if (declared < existing) {
                return ConflictSeverity.Soft;
            }

            // Increase: messages live longer, additive → Info
            return ConflictSeverity.Info;
        }

        // Both null: no change (shouldn't reach here since we only call when different)
        return ConflictSeverity.Info;
    }

    private static ConflictSeverity ClassifyOverflowStrategyChange(QueueOptions declared, QueueInfo existing) {
        // Changing to non-RedirectToDlq: purely policy change, takes effect on next overflow → Info
        if (declared.OverflowStrategy != OverflowStrategy.RedirectToDlq) {
            return ConflictSeverity.Info;
        }

        // Changing to RedirectToDlq: cross-parameter check on the existing queue's DLQ state.
        // If the existing queue has DLQ enabled, overflow will redirect correctly → Info.
        // If existing DLQ is not enabled, a runtime error would occur on overflow → Hard.
        return existing.EnableDeadLetterQueue
            ? ConflictSeverity.Info
            : ConflictSeverity.Hard;
    }
}
