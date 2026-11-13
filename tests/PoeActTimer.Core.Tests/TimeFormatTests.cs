using PoeActTimer.Core;
using Xunit;

namespace PoeActTimer.Core.Tests;

public class TimeFormatTests
{
    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(5, "0:05")]
    [InlineData(65, "1:05")]
    [InlineData(3599, "59:59")]
    [InlineData(3600, "1:00:00")]
    [InlineData(4 * 3600 + 12 * 60 + 37, "4:12:37")]
    [InlineData(-10, "0:00")]
    public void FormatsSeconds(int seconds, string expected) =>
        Assert.Equal(expected, TimeFormat.Format(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void TruncatesFractions() =>
        Assert.Equal("0:09", TimeFormat.Format(TimeSpan.FromMilliseconds(9999)));
}
