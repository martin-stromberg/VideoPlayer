using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using VideoWebPlayer.Extensions;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class MdnsRegistrationTests
{
    [Fact]
    public void AddVideoWebPlayerServices_DoesNotRegisterWorker_InTesting()
    {
        Assert.False(RegistersWorker("Testing"));
    }

    [Fact]
    public void AddVideoWebPlayerServices_RegistersWorker_OutsideTesting()
    {
        Assert.True(RegistersWorker("Development"));
    }

    private static bool RegistersWorker(string environmentName)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environmentName
        });

        builder.AddVideoWebPlayerServices();

        return builder.Services.Any(descriptor =>
            descriptor.ServiceType == typeof(IHostedService)
            && descriptor.ImplementationType == typeof(MdnsAdvertiserWorker));
    }
}
