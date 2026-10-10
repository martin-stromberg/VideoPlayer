using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class PairingUrlBuilderTests
{
    [Fact]
    public void BuildPairingUrl_AppendsPairingSegmentAndTicket()
    {
        var url = PairingUrlBuilder.BuildPairingUrl("http://192.168.10.230:83", "abc123");

        Assert.Equal("http://192.168.10.230:83/pairing?t=abc123", url);
    }

    [Fact]
    public void BuildPairingUrl_ToleratesTrailingSlash()
    {
        var url = PairingUrlBuilder.BuildPairingUrl("http://server.lan:5000/", "t1");

        Assert.Equal("http://server.lan:5000/pairing?t=t1", url);
    }

    [Fact]
    public void BuildPairingUrl_PreservesBasePath()
    {
        // Reverse-Proxy-Unterpfad muss erhalten bleiben — die App schneidet
        // nur das abschließende "pairing"-Segment ab.
        var url = PairingUrlBuilder.BuildPairingUrl("https://host/videoplayer/", "t1");

        Assert.Equal("https://host/videoplayer/pairing?t=t1", url);
    }

    [Fact]
    public void BuildPairingUrl_EscapesTicket()
    {
        var url = PairingUrlBuilder.BuildPairingUrl("http://s/", "a b+c&d");

        Assert.Equal("http://s/pairing?t=a%20b%2Bc%26d", url);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildPairingUrl_RejectsEmptyBaseUrl(string? baseUrl)
    {
        Assert.ThrowsAny<ArgumentException>(() => PairingUrlBuilder.BuildPairingUrl(baseUrl!, "t"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildPairingUrl_RejectsEmptyTicket(string? ticket)
    {
        Assert.ThrowsAny<ArgumentException>(() => PairingUrlBuilder.BuildPairingUrl("http://s/", ticket!));
    }
}
