using VibeMQ.Protocol;
using VibeMQ.Server.Security;

namespace VibeMQ.Tests.Unit.Security;

public class MessageValidatorTests {
    [Fact]
    public void Validate_ValidMessage_ReturnsNull() {
        var message = new ProtocolMessage {
            Type = CommandType.Publish,
            Queue = "my-queue.v1",
        };

        Assert.Null(MessageValidator.Validate(message));
    }

    [Fact]
    public void Validate_EmptyId_ReturnsError() {
        var message = new ProtocolMessage {
            Id = "",
            Type = CommandType.Ping,
        };

        var error = MessageValidator.Validate(message);

        Assert.NotNull(error);
        Assert.Contains("ID", error);
    }

    [Fact]
    public void Validate_QueueNameTooLong_ReturnsError() {
        var message = new ProtocolMessage {
            Type = CommandType.Publish,
            Queue = new string('a', 300),
        };

        var error = MessageValidator.Validate(message);

        Assert.NotNull(error);
        Assert.Contains("maximum length", error);
    }

    [Fact]
    public void Validate_QueueNameWithInvalidChars_ReturnsError() {
        var message = new ProtocolMessage {
            Type = CommandType.Publish,
            Queue = "my queue!@#",
        };

        var error = MessageValidator.Validate(message);

        Assert.NotNull(error);
        Assert.Contains("invalid characters", error);
    }

    [Theory]
    [InlineData("simple")]
    [InlineData("my-queue")]
    [InlineData("my_queue")]
    [InlineData("my.queue.v2")]
    [InlineData("Queue123")]
    public void Validate_ValidQueueNames_ReturnsNull(string queueName) {
        var message = new ProtocolMessage {
            Type = CommandType.Publish,
            Queue = queueName,
        };

        Assert.Null(MessageValidator.Validate(message));
    }

    [Fact]
    public void Validate_TooManyHeaders_ReturnsError() {
        var headers = new Dictionary<string, string>();

        for (var i = 0; i < 51; i++) {
            headers[$"key{i}"] = "value";
        }

        var message = new ProtocolMessage {
            Type = CommandType.Publish,
            Headers = headers,
        };

        var error = MessageValidator.Validate(message);

        Assert.NotNull(error);
        Assert.Contains("Too many headers", error);
    }

    [Fact]
    public void Validate_EmptyHeaderKey_ReturnsError() {
        var message = new ProtocolMessage {
            Type = CommandType.Publish,
            Headers = new Dictionary<string, string> { [""] = "value" },
        };

        var error = MessageValidator.Validate(message);

        Assert.NotNull(error);
        Assert.Contains("key cannot be empty", error);
    }

    [Fact]
    public void Validate_HeaderValueTooLong_ReturnsError() {
        var message = new ProtocolMessage {
            Type = CommandType.Publish,
            Headers = new Dictionary<string, string> { ["key"] = new string('x', 5000) },
        };

        var error = MessageValidator.Validate(message);

        Assert.NotNull(error);
        Assert.Contains("maximum length", error);
    }

    [Fact]
    public void Validate_NullQueue_IsValid() {
        var message = new ProtocolMessage { Type = CommandType.Ping };

        Assert.Null(MessageValidator.Validate(message));
    }
}
