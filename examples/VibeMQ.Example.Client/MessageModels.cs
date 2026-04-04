using System.Text.Json.Serialization;

namespace VibeMQ.Example.Client;

public class Notification {
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("body")]
    public string Body { get; set; } = "";

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = "normal";
}

public class AlertEvent {
    [JsonPropertyName("source")]
    public string Source { get; set; } = "";

    [JsonPropertyName("level")]
    public string Level { get; set; } = "";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";

    [JsonPropertyName("tags")]
    public Dictionary<string, string>? Tags { get; set; }
}

public class BroadcastMessage {
    [JsonPropertyName("channel")]
    public string Channel { get; set; } = "";

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

public class JobPayload {
    [JsonPropertyName("jobId")]
    public string JobId { get; set; } = "";

    [JsonPropertyName("jobType")]
    public string JobType { get; set; } = "";

    [JsonPropertyName("payload")]
    public string? Payload { get; set; }
}
