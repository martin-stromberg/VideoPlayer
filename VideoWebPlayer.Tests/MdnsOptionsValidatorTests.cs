using VideoWebPlayer.Configuration;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class MdnsOptionsValidatorTests
{
    private readonly MdnsOptionsValidator _validator = new();

    [Fact]
    public void Validate_AcceptsDefaults()
    {
        var result = _validator.Validate(null, new MdnsOptions());

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void Validate_RejectsInvalidPort(int port)
    {
        var result = _validator.Validate(null, new MdnsOptions { Port = port });

        Assert.True(result.Failed);
        Assert.Contains("Mdns:Port", result.FailureMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("videowebplayer._tcp")]
    [InlineData("_videowebplayer._sctp")]
    [InlineData("_abcdefghijklmnop._tcp")]
    [InlineData("_videowebplayer._tcp.local.extra")]
    [InlineData("_video_webplayer._tcp")]
    // RFC 6335: Label darf nicht mit Bindestrich beginnen oder enden.
    [InlineData("_-x._tcp")]
    [InlineData("_x-._tcp")]
    [InlineData("_-abc-._udp")]
    public void Validate_RejectsInvalidServiceType(string serviceType)
    {
        var result = _validator.Validate(null, new MdnsOptions { ServiceType = serviceType });

        Assert.True(result.Failed);
        Assert.Contains("Mdns:ServiceType", result.FailureMessage);
    }

    [Theory]
    [InlineData("_videowebplayer._tcp")]
    [InlineData("_videowebplayer._tcp.local")]
    [InlineData("_videowebplayer._tcp.local.")]
    [InlineData("_abc-123._udp")]
    [InlineData("_1._tcp")]
    [InlineData("_x--y._udp")]
    public void Validate_AcceptsValidServiceType(string serviceType)
    {
        var result = _validator.Validate(null, new MdnsOptions { ServiceType = serviceType });

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RejectsEmptyInstanceName(string instanceName)
    {
        var result = _validator.Validate(null, new MdnsOptions { InstanceName = instanceName });

        Assert.True(result.Failed);
        Assert.Contains("Mdns:InstanceName", result.FailureMessage);
    }

    [Fact]
    public void Validate_RejectsInstanceNameLongerThan63Characters()
    {
        var result = _validator.Validate(null, new MdnsOptions { InstanceName = new string('a', 64) });

        Assert.True(result.Failed);
        Assert.Contains("Mdns:InstanceName", result.FailureMessage);
    }

    [Fact]
    public void Validate_AcceptsEmptyInstanceName_WhenDisabled()
    {
        var result = _validator.Validate(null, new MdnsOptions { Enabled = false, InstanceName = "" });

        Assert.True(result.Succeeded, result.FailureMessage);
    }
}
