using System.Text.Json.Serialization;

namespace VibeMQ.Example.Client.DI;

public class Notification {
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("body")]
    public string Body { get; set; } = "";

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = "normal";
}
