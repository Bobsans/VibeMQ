using VibeMQ.Enums;

namespace VibeMQ.Auth;

/// <summary>
/// A single ACL entry granting a set of queue operations for a specific queue pattern.
/// </summary>
public sealed record PermissionEntry(string QueuePattern, QueueOperation[] Operations);
