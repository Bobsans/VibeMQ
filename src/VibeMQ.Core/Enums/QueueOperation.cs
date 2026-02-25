namespace VibeMQ.Enums;

/// <summary>
/// Operations that can be performed on a queue.
/// Used in permission entries to grant or restrict access.
/// </summary>
public enum QueueOperation {
    /// <summary>Publish messages to a queue.</summary>
    Publish,

    /// <summary>Subscribe to receive messages from a queue.</summary>
    Subscribe,

    /// <summary>Create new queues.</summary>
    CreateQueue,

    /// <summary>Delete existing queues.</summary>
    DeleteQueue,

    /// <summary>Purge all messages from a queue.</summary>
    PurgeQueue,

    /// <summary>Retrieve queue metadata.</summary>
    GetQueueInfo,

    /// <summary>List available queues. Controls which queues appear in the filtered listing.</summary>
    ListQueues,
}
