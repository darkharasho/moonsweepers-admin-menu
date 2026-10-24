using AdminMenu;
using Xunit;

public class MenuGateTests
{
    [Theory]
    [InlineData(true, false, false, true, (int)ToggleDecision.Close)]
    [InlineData(false, false, false, false, (int)ToggleDecision.NotHosting)]
    [InlineData(false, true, false, false, (int)ToggleDecision.HostOnly)]
    [InlineData(false, true, true, true, (int)ToggleDecision.Busy)]
    [InlineData(false, true, true, false, (int)ToggleDecision.Open)]
    public void Decide(bool open, bool inLobby, bool isHost, bool busy, int expected) =>
        Assert.Equal((ToggleDecision)expected, MenuGate.Decide(open, inLobby, isHost, busy));

    [Fact]
    public void OnlyNotHostingAndHostOnlyHaveMessages()
    {
        Assert.NotNull(MenuGate.Message(ToggleDecision.NotHosting));
        Assert.NotNull(MenuGate.Message(ToggleDecision.HostOnly));
        Assert.Null(MenuGate.Message(ToggleDecision.Busy));
        Assert.Null(MenuGate.Message(ToggleDecision.Open));
    }

    [Theory]
    [InlineData(true, true, false, false)]
    [InlineData(false, true, false, true)]
    [InlineData(true, false, false, true)]
    [InlineData(true, true, true, true)]
    public void AutoClose(bool inLobby, bool isHost, bool loading, bool expected) =>
        Assert.Equal(expected, MenuGate.ShouldAutoClose(inLobby, isHost, loading));
}
