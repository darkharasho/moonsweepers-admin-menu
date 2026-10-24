using AdminMenu;
using Xunit;

public class MoneyInputTests
{
    [Theory]
    [InlineData("2500", 2500)]
    [InlineData("$2,500", 2500)]
    [InlineData(" 2.5k ", 2500)]
    [InlineData("3M", 3_000_000)]
    [InlineData("1b", 1_000_000_000)]
    [InlineData("12.345", 12.34)]
    [InlineData("0", 0)]
    public void ReadsAmounts(string text, double expected)
    {
        Assert.True(MoneyInput.TryParse(text, out var dollars));
        Assert.Equal((decimal)expected, dollars);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("k")]
    [InlineData("-5")]
    [InlineData("+5")]
    [InlineData("abc")]
    [InlineData("1.2.3")]
    [InlineData("2000b")]
    public void RejectsOthers(string text)
    {
        Assert.False(MoneyInput.TryParse(text, out _));
    }
}
