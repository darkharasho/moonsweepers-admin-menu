using System.Linq;
using AdminMenu;
using Xunit;

public class KickListTests
{
    [Fact]
    public void KickedAndBannedPlayersAreBlocked()
    {
        var list = new KickList();
        list.Kick(1, "Ann");
        list.Ban(2, "Bob");
        Assert.True(list.Contains(1));
        Assert.True(list.Contains(2));
        Assert.False(list.Contains(3));
        Assert.False(list.IsBanned(1));
        Assert.True(list.IsBanned(2));
    }

    [Fact]
    public void BanningAKickedPlayerMovesThemAndKeepsTheirName()
    {
        var list = new KickList();
        list.Kick(1, "Ann");
        list.Ban(1, null);
        Assert.Equal(1, list.Count);
        Assert.True(list.IsBanned(1));
        Assert.Equal("Ann", list.NameOf(1));
    }

    [Fact]
    public void KickingABannedPlayerKeepsTheBan()
    {
        var list = new KickList();
        list.Ban(1, "Ann");
        list.Kick(1, "Ann");
        Assert.True(list.IsBanned(1));
        Assert.Equal(1, list.Count);
    }

    [Fact]
    public void PardonClearsBothLists()
    {
        var list = new KickList();
        list.Kick(1, "Ann");
        list.Ban(2, "Bob");
        Assert.True(list.Pardon(1));
        Assert.True(list.Pardon(2));
        Assert.False(list.Pardon(3));
        Assert.Equal(0, list.Count);
    }

    [Fact]
    public void ClearKicksKeepsBans()
    {
        var list = new KickList();
        list.Kick(1, "Ann");
        list.Ban(2, "Bob");
        list.ClearKicks();
        Assert.False(list.Contains(1));
        Assert.True(list.Contains(2));
    }

    [Fact]
    public void ZeroIdIsIgnored()
    {
        var list = new KickList();
        list.Kick(0, "x");
        list.Ban(0, "x");
        Assert.Equal(0, list.Count);
    }

    [Fact]
    public void BansRoundTripAndNamesLoseSeparators()
    {
        var list = new KickList();
        list.Ban(76561198000000002, "Bob, the: Builder");
        list.Ban(76561198000000001, "Ann");
        list.Kick(5, "kicked only");
        var saved = list.SaveBans();
        Assert.Equal("76561198000000001:Ann,76561198000000002:Bob  the  Builder", saved);

        var loaded = new KickList();
        loaded.LoadBans(saved);
        Assert.True(loaded.IsBanned(76561198000000001));
        Assert.Equal("Bob  the  Builder", loaded.NameOf(76561198000000002));
        Assert.False(loaded.Contains(5));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("junk,:name,0:zero,-1:neg")]
    public void BadBanTextLoadsNothing(string text)
    {
        var list = new KickList();
        list.LoadBans(text);
        Assert.Equal(0, list.Count);
    }

    [Fact]
    public void BansWithoutNamesLoad()
    {
        var list = new KickList();
        list.LoadBans("12, 34 ");
        Assert.True(list.IsBanned(12));
        Assert.True(list.IsBanned(34));
    }

    [Fact]
    public void IdsEncodeSortedAndDecode()
    {
        var list = new KickList();
        list.Kick(30, "c");
        list.Kick(10, "a");
        list.Ban(20, "b");
        var encoded = list.EncodeIds();
        Assert.Equal("20,10,30", encoded);
        Assert.Equal(new ulong[] { 10, 20, 30 }, KickList.DecodeIds(encoded).OrderBy(i => i));
    }

    [Fact]
    public void EncodingFitsSteamsLimitWithBansFirst()
    {
        var list = new KickList();
        for (ulong i = 0; i < 600; i++)
            list.Kick(76561198000000000 + i, "");
        list.Ban(76561199999999999, "");
        var encoded = list.EncodeIds();
        Assert.True(encoded.Length <= KickList.MaxEncodedLength);
        Assert.StartsWith("76561199999999999,", encoded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("x,,0, ,7")]
    public void DecodeSkipsJunk(string text)
    {
        var ids = KickList.DecodeIds(text);
        Assert.DoesNotContain(0UL, ids);
        Assert.True(ids.Count <= 1);
    }

    [Fact]
    public void EntriesListBansThenKicksByName()
    {
        var list = new KickList();
        list.Kick(1, "Zed");
        list.Kick(2, "Amy");
        list.Ban(3, "Mo");
        Assert.Equal(new[] { "Mo", "Amy", "Zed" }, list.Entries().Select(e => e.Name));
        Assert.True(list.Entries()[0].Banned);
    }
}

public class KickRulesTests
{
    [Theory]
    [InlineData(5UL, 1UL, 1UL, true)]
    [InlineData(1UL, 1UL, 1UL, false)]
    [InlineData(2UL, 1UL, 2UL, false)]
    [InlineData(0UL, 1UL, 1UL, false)]
    public void CanRemoveAnyoneButYourselfAndTheHost(ulong id, ulong local, ulong host, bool expected) =>
        Assert.Equal(expected, KickRules.CanRemove(id, local, host));

    [Fact]
    public void OnlyAListedCrewmateLeaves()
    {
        var published = new[] { 7UL };
        Assert.True(KickRules.ShouldLeave(false, 7, published));
        Assert.False(KickRules.ShouldLeave(true, 7, published));
        Assert.False(KickRules.ShouldLeave(false, 8, published));
        Assert.False(KickRules.ShouldLeave(false, 7, null));
    }
}
