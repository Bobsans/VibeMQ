using VibeMQ.Protocol;

namespace VibeMQ.Tests.Unit.Protocol;

public class ProtocolMessagePoolTests {
    [Fact]
    public void Rent_ReturnsMessageWithCorrectType() {
        var msg = ProtocolMessagePool.Rent(CommandType.Publish);
        try {
            Assert.NotNull(msg);
            Assert.Equal(CommandType.Publish, msg.Type);
            Assert.NotNull(msg.Id);
            Assert.Equal(1, msg.Version);
        } finally {
            ProtocolMessagePool.Return(msg);
        }
    }

    [Fact]
    public void Return_ThenRent_CanReuseInstance() {
        var first = ProtocolMessagePool.Rent(CommandType.Ping);
        var id = first.Id;
        ProtocolMessagePool.Return(first);

        var second = ProtocolMessagePool.Rent(CommandType.Pong);
        Assert.Equal(CommandType.Pong, second.Type);
        Assert.NotEqual(id, second.Id);
        Assert.Null(second.Queue);
        Assert.Null(second.Payload);
        Assert.Null(second.Headers);
        Assert.Null(second.ErrorCode);
        Assert.Null(second.ErrorMessage);
        ProtocolMessagePool.Return(second);
    }

    [Fact]
    public void Return_ClearsQueuePayloadHeaders() {
        var msg = ProtocolMessagePool.Rent(CommandType.Publish);
        msg.Queue = "q";
        msg.Headers = new Dictionary<string, string> { ["x"] = "y" };
        ProtocolMessagePool.Return(msg);

        var reused = ProtocolMessagePool.Rent(CommandType.Subscribe);
        Assert.Null(reused.Queue);
        Assert.Null(reused.Headers);
        ProtocolMessagePool.Return(reused);
    }

    [Fact]
    public void Rent_WhenPoolEmpty_CreatesNewMessage() {
        var msg = ProtocolMessagePool.Rent(CommandType.Connect);
        Assert.NotNull(msg);
        Assert.Equal(CommandType.Connect, msg.Type);
        ProtocolMessagePool.Return(msg);
    }

    [Fact]
    public void Return_WhenPoolAtMaxSize_DoesNotThrow() {
        var list = new List<ProtocolMessage>();
        for (var i = 0; i < 300; i++) {
            var msg = ProtocolMessagePool.Rent(CommandType.Ping);
            list.Add(msg);
        }
        foreach (var msg in list) {
            ProtocolMessagePool.Return(msg);
        }
    }
}
