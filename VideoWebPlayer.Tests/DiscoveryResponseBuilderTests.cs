using System.Net;
using Microsoft.Extensions.Configuration;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class DiscoveryResponseBuilderTests
{
    private static readonly IPAddress LanAddress = IPAddress.Parse("192.168.1.20");

    [Fact]
    public void Build_ReturnsAdminBaseUrl_WhenSet()
    {
        var options = new DiscoveryOptions { PublicBaseUrl = "http://configured.example/" };
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Host:Address"] = "192.168.1.10"
        });

        var url = DiscoveryResponseBuilder.Build(
            options, configuration, new[] { "http://192.168.1.5:5000" }, new[] { LanAddress },
            null, "https://videos.example.com/videoplayer/");

        Assert.Equal("https://videos.example.com/videoplayer/", url);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_UsesPublicBaseUrl_WhenAdminEmpty(string? adminPublicBaseUrl)
    {
        var options = new DiscoveryOptions { PublicBaseUrl = "https://configured.example/app/" };

        var url = DiscoveryResponseBuilder.Build(options, EmptyConfiguration(), null, null, null, adminPublicBaseUrl);

        Assert.Equal("https://configured.example/app/", url);
    }

    [Theory]
    [InlineData("notaurl")]
    [InlineData("ftp://x")]
    public void Build_IgnoresInvalidAdminBaseUrl(string adminPublicBaseUrl)
    {
        var options = new DiscoveryOptions { PublicBaseUrl = "http://configured.example/" };

        var url = DiscoveryResponseBuilder.Build(options, EmptyConfiguration(), null, null, null, adminPublicBaseUrl);

        Assert.Equal("http://configured.example/", url);
    }

    [Fact]
    public void Build_ReturnsPublicBaseUrl_WhenConfigured()
    {
        var options = new DiscoveryOptions { PublicBaseUrl = "https://example.com:8443/videoplayer/" };
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Host:Address"] = "192.168.1.10"
        });

        var url = DiscoveryResponseBuilder.Build(
            options, configuration, new[] { "http://192.168.1.5:5000" }, new[] { LanAddress }, null, null);

        Assert.Equal("https://example.com:8443/videoplayer/", url);
    }

    [Fact]
    public void Build_DerivesLanAddress_WhenNothingConfigured()
    {
        var url = DiscoveryResponseBuilder.Build(
            new DiscoveryOptions(), EmptyConfiguration(), null,
            new[] { IPAddress.Loopback, LanAddress }, null, null);

        Assert.Equal("http://192.168.1.20:5000", url);
        Assert.DoesNotContain("localhost", url);
    }

    [Fact]
    public void Build_UsesHostAddress_WhenLanAddress()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Host:Address"] = "192.168.1.10"
        });

        var url = DiscoveryResponseBuilder.Build(
            new DiscoveryOptions(), configuration, new[] { "http://0.0.0.0:5002" }, new[] { LanAddress }, null, null);

        Assert.Equal("http://192.168.1.10:5002", url);
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("[::1]")]
    [InlineData("[0:0:0:0:0:0:0:1]")]
    [InlineData("[fe80::1]")]
    [InlineData("[::]")]
    [InlineData("0.0.0.0")]
    [InlineData("*")]
    public void Build_IgnoresLoopbackHostAddress(string hostAddress)
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Host:Address"] = hostAddress
        });

        var url = DiscoveryResponseBuilder.Build(
            new DiscoveryOptions(), configuration, null, new[] { LanAddress }, null, null);

        Assert.Equal("http://192.168.1.20:5000", url);
    }

    [Fact]
    public void Build_UsesBoundLiteralAddress()
    {
        var url = DiscoveryResponseBuilder.Build(
            new DiscoveryOptions(), EmptyConfiguration(), new[] { "http://192.168.1.5:5000" },
            new[] { LanAddress }, null, null);

        Assert.Equal("http://192.168.1.5:5000", url);
    }

    [Fact]
    public void Build_UsesBoundWildcardPort_ButDnsHost()
    {
        var url = DiscoveryResponseBuilder.Build(
            new DiscoveryOptions(), EmptyConfiguration(), new[] { "http://0.0.0.0:5002" },
            new[] { LanAddress }, null, null);

        Assert.Equal("http://192.168.1.20:5002", url);
    }

    [Fact]
    public void Build_UsesKestrelEndpointUrl()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Kestrel:Endpoints:Http:Url"] = "http://*:5002"
        });

        var url = DiscoveryResponseBuilder.Build(
            new DiscoveryOptions(), configuration, null, new[] { LanAddress }, null, null);

        Assert.Equal("http://192.168.1.20:5002", url);
    }

    [Fact]
    public void Build_UsesKestrelHttpsScheme()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Kestrel:Endpoints:Https:Url"] = "https://*:5443"
        });

        var url = DiscoveryResponseBuilder.Build(
            new DiscoveryOptions(), configuration, null, new[] { LanAddress }, null, null);

        Assert.Equal("https://192.168.1.20:5443", url);
    }

    [Fact]
    public void Build_UsesHostPortFallback()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Host:Port"] = "5050"
        });

        var url = DiscoveryResponseBuilder.Build(
            new DiscoveryOptions(), configuration, null, new[] { LanAddress }, null, null);

        Assert.Equal("http://192.168.1.20:5050", url);
    }

    [Fact]
    public void Build_UsesDefaultPort5000()
    {
        var url = DiscoveryResponseBuilder.Build(
            new DiscoveryOptions(), EmptyConfiguration(), null, new[] { LanAddress }, null, null);

        Assert.Equal("http://192.168.1.20:5000", url);
    }

    [Fact]
    public void Build_SkipsLoopbackBoundAddress_ForHost()
    {
        var url = DiscoveryResponseBuilder.Build(
            new DiscoveryOptions(), EmptyConfiguration(), new[] { "http://localhost:5000" },
            new[] { LanAddress }, null, null);

        Assert.Equal("http://192.168.1.20:5000", url);
    }

    [Fact]
    public void Build_UsesHostName_WhenNoLanAddress()
    {
        var url = DiscoveryResponseBuilder.Build(
            new DiscoveryOptions(), EmptyConfiguration(), null, null, "testhost", null);

        Assert.Equal("http://testhost:5000", url);
    }

    [Fact]
    public void Build_UsesLocalhost_WhenNoHostNameAvailable()
    {
        var url = DiscoveryResponseBuilder.Build(
            new DiscoveryOptions(), EmptyConfiguration(), null, null, null, null);

        Assert.Equal("http://localhost:5000", url);
    }

    [Fact]
    public void Build_SkipsIpv6Addresses()
    {
        var url = DiscoveryResponseBuilder.Build(
            new DiscoveryOptions(), EmptyConfiguration(), null,
            new[] { IPAddress.Parse("fe80::1"), IPAddress.IPv6Loopback }, "testhost", null);

        Assert.Equal("http://testhost:5000", url);
        Assert.DoesNotContain("[", url);
    }

    private static IConfiguration EmptyConfiguration()
        => CreateConfiguration(new Dictionary<string, string?>());

    private static IConfiguration CreateConfiguration(Dictionary<string, string?> settings)
        => new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
}
