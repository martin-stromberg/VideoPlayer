using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VideoWebPlayer.Configuration;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class MdnsConfigurationTests
{
    [Fact]
    public void MdnsSection_BindsToOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mdns:Enabled"] = "false",
                ["Mdns:InstanceName"] = "TestServer",
                ["Mdns:ServiceType"] = "_test._udp",
                ["Mdns:Port"] = "5353"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<MdnsOptions>().Bind(configuration.GetSection("Mdns"));
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<MdnsOptions>>().Value;

        Assert.False(options.Enabled);
        Assert.Equal("TestServer", options.InstanceName);
        Assert.Equal("_test._udp", options.ServiceType);
        Assert.Equal(5353, options.Port);
    }
}
