namespace VibeMQ.Client;

/// <summary>
/// User info returned by <see cref="VibeMQClient.ListUsersAsync"/>.
/// </summary>
/// <param name="Username">Login name.</param>
/// <param name="IsSuperuser">Whether the user has superuser privileges.</param>
/// <param name="CreatedAt">Unix timestamp when the user was created.</param>
public sealed record AdminUserInfo(string Username, bool IsSuperuser, long CreatedAt);

/// <summary>
/// Permission entry returned by <see cref="VibeMQClient.GetUserPermissionsAsync"/>.
/// </summary>
/// <param name="QueuePattern">Glob pattern for queue names.</param>
/// <param name="Operations">Allowed operations (e.g. "Publish", "Subscribe").</param>
public sealed record AdminPermissionInfo(string QueuePattern, string[] Operations);
