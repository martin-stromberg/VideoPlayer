using VideoWebPlayer.Extensions;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class KestrelLimitsTests
{
    [Fact]
    public void ParseMaxRequestBodySize_WithPositiveValue_ReturnsLimit()
    {
        Assert.Equal(1024L, KestrelLimits.ParseMaxRequestBodySize("1024"));
    }

    [Fact]
    public void ParseMaxRequestBodySize_WithZero_ReturnsNull()
    {
        Assert.Null(KestrelLimits.ParseMaxRequestBodySize("0"));
    }

    [Fact]
    public void ParseMaxRequestBodySize_WithNegativeValue_ReturnsNull()
    {
        Assert.Null(KestrelLimits.ParseMaxRequestBodySize("-1"));
    }

    [Fact]
    public void ParseMaxRequestBodySize_WithNonNumericValue_ThrowsInvalidOperationException()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => KestrelLimits.ParseMaxRequestBodySize("5GB"));

        Assert.Contains("Kestrel:Limits:MaxRequestBodySize", exception.Message, StringComparison.Ordinal);
        Assert.Contains("5GB", exception.Message, StringComparison.Ordinal);
    }
}
