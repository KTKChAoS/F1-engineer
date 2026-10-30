using System.Text.Json;
using PitWall.Contracts;
using PitWall.Contracts.Messages;

namespace PitWall.Core.Tests;

public sealed class WireMessageTests
{
    [Fact]
    public void SerializesProtocolEnvelopeWithTimestampFieldT()
    {
        var message = new WireMessage<string>
        {
            Type = MessageTypes.Live,
            Seq = 7,
            Timestamp = 1_798_000_000_000,
            Payload = "telemetry"
        };

        var json = JsonSerializer.Serialize(message, JsonDefaults.Options);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("live", root.GetProperty("type").GetString());
        Assert.Equal(7, root.GetProperty("seq").GetInt64());
        Assert.Equal(message.Timestamp, root.GetProperty("t").GetInt64());
        Assert.Equal("telemetry", root.GetProperty("payload").GetString());
    }
}
