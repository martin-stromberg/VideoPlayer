using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VideoWebPlayer.Configuration;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class DiscoveryConfigurationTests
{
    [Fact]
    public void DiscoverySection_BindsToOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Discovery:PublicBaseUrl"] = "https://videos.example.com/videoplayer/"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<DiscoveryOptions>().Bind(configuration.GetSection("Discovery"));
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<DiscoveryOptions>>().Value;

        Assert.Equal("https://videos.example.com/videoplayer/", options.PublicBaseUrl);
    }
}
