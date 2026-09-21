using VideoWebPlayer.Utils;
using Xunit;

namespace VideoWebPlayer.Tests.Utils;

public sealed class ByteSizeHelperTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1048576, "1 MB")]
    [InlineData(5368709120, "5 GB")]
    public void FormatBytes_UsesLargestFittingUnit(long bytes, string expected)
    {
        var expectedInvariant = expected.Replace(".", System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);

        Assert.Equal(expectedInvariant, ByteSizeHelper.FormatBytes(bytes));
    }

    [Fact]
    public void FormatBytes_WithValueAboveTerabytes_StopsAtGigabytes()
    {
        Assert.Equal("2048 GB", ByteSizeHelper.FormatBytes(2199023255552L));
    }
}
