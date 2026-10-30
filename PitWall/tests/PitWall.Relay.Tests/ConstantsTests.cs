using PitWall.Contracts;

namespace PitWall.Relay.Tests;

public sealed class ConstantsTests
{
    [Fact]
    public void RoomCodeAlphabetHasEightDistinctAllowedCharacters()
    {
        Assert.Equal(8, Constants.RoomCodeLength);
        Assert.Equal(Constants.RoomCodeAlphabet.Length, Constants.RoomCodeAlphabet.Distinct().Count());
        Assert.DoesNotContain('0', Constants.RoomCodeAlphabet);
        Assert.DoesNotContain('1', Constants.RoomCodeAlphabet);
    }
}
