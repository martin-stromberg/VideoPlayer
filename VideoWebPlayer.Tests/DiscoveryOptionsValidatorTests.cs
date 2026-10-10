using VideoWebPlayer.Configuration;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class DiscoveryOptionsValidatorTests
{
    private readonly DiscoveryOptionsValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_AcceptsNullOrEmpty(string? publicBaseUrl)
    {
        var result = _validator.Validate(null, new DiscoveryOptions { PublicBaseUrl = publicBaseUrl });

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Fact]
    public void Validate_AcceptsAbsoluteHttpUrl()
    {
        var result = _validator.Validate(null, new DiscoveryOptions { PublicBaseUrl = "http://server.lan:5000/" });

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Fact]
    public void Validate_AcceptsUrlWithPathAndPort()
    {
        var result = _validator.Validate(null, new DiscoveryOptions { PublicBaseUrl = "https://example.com:8443/videoplayer/" });

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Theory]
    [InlineData("/videoplayer/")]
    [InlineData("notaurl")]
    public void Validate_RejectsRelativeUrl(string publicBaseUrl)
    {
        var result = _validator.Validate(null, new DiscoveryOptions { PublicBaseUrl = publicBaseUrl });

        Assert.True(result.Failed);
        Assert.Contains("Discovery:PublicBaseUrl", result.FailureMessage);
    }

    [Theory]
    [InlineData("ftp://server.lan/")]
    [InlineData("dns://server.lan/")]
    public void Validate_RejectsNonHttpScheme(string publicBaseUrl)
    {
        var result = _validator.Validate(null, new DiscoveryOptions { PublicBaseUrl = publicBaseUrl });

        Assert.True(result.Failed);
        Assert.Contains("Discovery:PublicBaseUrl", result.FailureMessage);
    }
}
