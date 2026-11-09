using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for <see cref="SftpProbeUriBuilder"/> — URI construction, percent-encoding of
/// credentials and path segments, and credential masking for log output.
/// </summary>
public class SftpProbeUriBuilderTests
{
    [Fact]
    public void TryBuildUri_BuildsExpectedUrl()
    {
        var uri = SftpProbeUriBuilder.TryBuildUri("nas.local", 22, "user", "s3cret", "/media/episode.mkv");
        Assert.Equal("sftp://user:s3cret@nas.local:22/media/episode.mkv", uri);
    }

    [Fact]
    public void TryBuildUri_EncodesSpecialCharactersInCredentialsAndPath()
    {
        var uri = SftpProbeUriBuilder.TryBuildUri("nas.local", 22, "u@x:1", "p@ss:w/rd%", "/media/film & co/ep 1.mp4");
        Assert.Equal("sftp://u%40x%3A1:p%40ss%3Aw%2Frd%25@nas.local:22/media/film%20%26%20co/ep%201.mp4", uri);
    }

    [Fact]
    public void TryBuildUri_OmitsPort_WhenNotPositive()
    {
        var uri = SftpProbeUriBuilder.TryBuildUri("nas.local", 0, "user", "pass", "/media/x.mp4");
        Assert.Equal("sftp://user:pass@nas.local/media/x.mp4", uri);
    }

    [Fact]
    public void TryBuildUri_BracketsIpv6Host()
    {
        var uri = SftpProbeUriBuilder.TryBuildUri("fe80::1", 22, "user", "pass", "/media/x.mp4");
        Assert.Equal("sftp://user:pass@[fe80::1]:22/media/x.mp4", uri);
    }

    [Theory]
    [InlineData(null, "user", "pass", "/media/x.mp4")]
    [InlineData("", "user", "pass", "/media/x.mp4")]
    [InlineData("nas.local", null, "pass", "/media/x.mp4")]
    [InlineData("nas.local", "", "pass", "/media/x.mp4")]
    [InlineData("nas.local", "user", null, "/media/x.mp4")]
    [InlineData("nas.local", "user", "", "/media/x.mp4")]
    [InlineData("nas.local", "user", "pass", null)]
    [InlineData("nas.local", "user", "pass", "")]
    [InlineData("nas.local", "user", "pass", "relative/media/x.mp4")]
    public void TryBuildUri_MissingOrRelativeInput_ReturnsNull(
        string? host, string? username, string? password, string? remotePath)
    {
        Assert.Null(SftpProbeUriBuilder.TryBuildUri(host, 22, username, password, remotePath));
    }

    [Fact]
    public void MaskCredentials_MasksUserInfo()
    {
        Assert.Equal(
            "sftp://***@nas.local:22/media/x.mp4",
            SftpProbeUriBuilder.MaskCredentials("sftp://user:s3cret@nas.local:22/media/x.mp4"));
    }

    [Fact]
    public void MaskCredentialsInText_MasksEmbeddedUri_LeavesRest()
    {
        var masked = SftpProbeUriBuilder.MaskCredentialsInText(
            "sftp://user:s3cret@nas.local:22/media/x.mp4: moov atom not found");
        Assert.Equal("sftp://***@nas.local:22/media/x.mp4: moov atom not found", masked);
    }

    [Fact]
    public void MaskCredentialsInText_NoUri_ReturnsInput()
    {
        Assert.Equal("video.mp4: Invalid data found", SftpProbeUriBuilder.MaskCredentialsInText("video.mp4: Invalid data found"));
        Assert.Equal(string.Empty, SftpProbeUriBuilder.MaskCredentialsInText(null));
    }

    [Theory]
    [InlineData("/media", "ep.mkv", "/media/ep.mkv")]
    [InlineData("/media/", "/ep.mkv", "/media/ep.mkv")]
    [InlineData("", "ep.mkv", "ep.mkv")]
    [InlineData("/media", "", "/media")]
    [InlineData(null, null, "")]
    public void CombinePath_JoinsLikeReader(string? dir, string? name, string expected)
    {
        Assert.Equal(expected, SftpProbeUriBuilder.CombinePath(dir, name));
    }
}
