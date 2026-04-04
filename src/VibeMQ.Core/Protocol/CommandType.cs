namespace VibeMQ.Protocol;

/// <summary>
/// All command types supported by the VibeMQ wire protocol.
/// </summary>
public enum CommandType {
    // Connection lifecycle
    Connect = 0,
    ConnectAck = 1,
    Disconnect = 2,

    // Keep-alive
    Ping = 10,
    Pong = 11,

    // Pub/Sub
    Publish = 20,
    PublishAck = 21,
    Subscribe = 22,
    SubscribeAck = 23,
    Unsubscribe = 24,
    UnsubscribeAck = 25,
    Deliver = 26,

    // Message acknowledgment
    Ack = 30,

    // Queue management
    CreateQueue = 40,
    DeleteQueue = 41,
    QueueInfo = 42,
    ListQueues = 43,

    // Admin (superuser only)
    AdminCreateUser = 50,
    AdminDeleteUser = 51,
    AdminChangePassword = 52,
    AdminGrantPermission = 53,
    AdminRevokePermission = 54,
    AdminListUsers = 55,
    AdminGetUserPermissions = 56,

    // Errors
    Error = 99
}
