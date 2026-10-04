using Microsoft.Extensions.Configuration;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class MdnsServiceProfileBuilderTests
{
    [Fact]
    public void Build_UsesDefaults_WhenNothingConfigured()
    {
        var advertisement = MdnsServiceProfileBuilder.Build(new MdnsOptions(), EmptyConfiguration(), null);

        Assert.Equal("VideoWebPlayer", advertisement.InstanceName);
        Assert.Equal("_videowebplayer._tcp.local.", advertisement.ServiceType);
        Assert.Equal(5000, advertisement.Port);
        Assert.Equal("/", advertisement.TxtRecords["path"]);
        Assert.Equal("VideoWebPlayer", advertisement.TxtRecords["app"]);
    }

    [Fact]
    public void Build_PrefersMdnsPort_OverAllSources()
    {
        var options = new MdnsOptions { Port = 1234 };
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Kestrel:Endpoints:Http:Url"] = "http://*:5002",
            ["Host:Port"] = "5050"
        });

        var advertisement = MdnsServiceProfileBuilder.Build(options, configuration, new[] { "http://127.0.0.1:7777" });

        Assert.Equal(1234, advertisement.Port);
    }

    [Fact]
    public void Build_UsesBoundServerAddressPort()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Kestrel:Endpoints:Http:Url"] = "http://*:5002",
            ["Host:Port"] = "5050"
        });

        var advertisement = MdnsServiceProfileBuilder.Build(new MdnsOptions(), configuration, new[] { "http://127.0.0.1:4711" });

        Assert.Equal(4711, advertisement.Port);
    }

    [Fact]
    public void Build_UsesKestrelEndpointUrlPort()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Kestrel:Endpoints:Http:Url"] = "http://*:5002",
            ["Host:Port"] = "5050"
        });

        var advertisement = MdnsServiceProfileBuilder.Build(new MdnsOptions(), configuration, null);

        Assert.Equal(5002, advertisement.Port);
    }

    [Fact]
    public void Build_UsesHostPortFallback()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Host:Port"] = "5050"
        });

        var advertisement = MdnsServiceProfileBuilder.Build(new MdnsOptions(), configuration, null);

        Assert.Equal(5050, advertisement.Port);
    }

    [Fact]
    public void Build_UsesConfiguredServiceType()
    {
        var options = new MdnsOptions { ServiceType = "_custom._udp.local." };

        var advertisement = MdnsServiceProfileBuilder.Build(options, EmptyConfiguration(), null);

        Assert.Equal("_custom._udp.local.", advertisement.ServiceType);
    }

    [Fact]
    public void Build_UsesConfiguredInstanceName()
    {
        var options = new MdnsOptions { InstanceName = "MeinServer" };

        var advertisement = MdnsServiceProfileBuilder.Build(options, EmptyConfiguration(), null);

        Assert.Equal("MeinServer", advertisement.InstanceName);
    }

    private static IConfiguration EmptyConfiguration()
        => CreateConfiguration(new Dictionary<string, string?>());

    private static IConfiguration CreateConfiguration(Dictionary<string, string?> settings)
        => new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
}
