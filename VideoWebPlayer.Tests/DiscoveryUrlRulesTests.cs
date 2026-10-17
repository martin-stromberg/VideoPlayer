using VideoWebPlayer.Configuration;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class DiscoveryUrlRulesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValidPublicBaseUrl_AcceptsNullOrEmpty(string? value)
    {
        Assert.True(DiscoveryUrlRules.IsValidPublicBaseUrl(value));
    }

    [Theory]
    [InlineData("http://server.lan:5000/")]
    [InlineData("https://videos.example.com/videoplayer/")]
    [InlineData("https://example.com:8443/app")]
    public void IsValidPublicBaseUrl_AcceptsAbsoluteHttpUrls(string value)
    {
        Assert.True(DiscoveryUrlRules.IsValidPublicBaseUrl(value));
    }

    [Theory]
    [InlineData("notaurl")]
    [InlineData("/relative/path")]
    [InlineData("server.lan:5000")]
    [InlineData("ftp://server.lan/")]
    [InlineData("dns://server.lan/")]
    [InlineData("file:///c:/temp")]
    public void IsValidPublicBaseUrl_RejectsInvalidValues(string value)
    {
        Assert.False(DiscoveryUrlRules.IsValidPublicBaseUrl(value));
    }

    [Fact]
    public void NormalizePublicBaseUrl_TrimsValue()
    {
        Assert.Equal("https://example.com/videoplayer/", DiscoveryUrlRules.NormalizePublicBaseUrl("  https://example.com/videoplayer/  "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizePublicBaseUrl_MapsEmptyToNull(string? value)
    {
        Assert.Null(DiscoveryUrlRules.NormalizePublicBaseUrl(value));
    }
}
