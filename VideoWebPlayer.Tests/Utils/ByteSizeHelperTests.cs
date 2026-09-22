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

    [Theory]
    [InlineData(5368709120L, 5, "GB")]
    [InlineData(1073741824L, 1, "GB")]
    [InlineData(1610612736L, 1.5, "GB")]
    [InlineData(536870912L, 512, "MB")]
    [InlineData(1048576L, 1, "MB")]
    public void SplitBytes_PicksReadableUnit(long bytes, double expectedValue, string expectedUnit)
    {
        var (value, unit) = ByteSizeHelper.SplitBytes(bytes);

        Assert.Equal((decimal)expectedValue, value);
        Assert.Equal(expectedUnit, unit);
    }

    [Theory]
    [InlineData(5, "GB", 5368709120L)]
    [InlineData(512, "MB", 536870912L)]
    [InlineData(1.5, "GB", 1610612736L)]
    [InlineData(1, "KB-oder-unbekannt", 1048576L)]
    [InlineData(1, null, 1048576L)]
    public void ToBytes_ConvertsUnitValueToBytes(double value, string? unit, long expected)
    {
        Assert.Equal(expected, ByteSizeHelper.ToBytes((decimal)value, unit));
    }

    [Fact]
    public void ToBytes_WithHugeValue_ClampstoLongMax()
    {
        Assert.Equal(long.MaxValue, ByteSizeHelper.ToBytes(decimal.MaxValue, "GB"));
    }
}
