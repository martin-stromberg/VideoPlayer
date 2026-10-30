using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for <see cref="FfmpegErrorMarkers"/>: the stderr marker predicates that classify
/// ffmpeg/ffprobe failures — transport errors vs. inputs the demuxer cannot read at all.
/// </summary>
public sealed class FfmpegErrorMarkersTests
{
    [Theory]
    [InlineData("pipe:0: End of file\n[mov,mp4,m4a,3gp,3g2,mj2 @ 0x55] stream 0, offset 0x30: partial file")]
    [InlineData("pipe:0: Invalid data found when processing input")]
    [InlineData("Could not find codec parameters for stream 0 (Video: h264, none): unspecified pixel format")]
    [InlineData("Cannot determine format of input 0:0 after EOF")]
    [InlineData("[mov,mp4,m4a,3gp,3g2,mj2 @ 0x55] moov atom not found")]
    public void ContainsUndecodableInputError_DemuxMarkers_Match(string stderr)
    {
        Assert.True(FfmpegErrorMarkers.ContainsUndecodableInputError(stderr));
    }

    [Theory]
    [InlineData("Conversion failed!")]
    [InlineData("Server returned 404 Not Found")]
    [InlineData("")]
    [InlineData(null)]
    public void ContainsUndecodableInputError_NoMarkers_NoMatch(string? stderr)
    {
        Assert.False(FfmpegErrorMarkers.ContainsUndecodableInputError(stderr));
    }

    [Theory]
    [InlineData("sftp://u:p@h:22/f.mkv: Connection refused")]
    [InlineData("Connection timed out")]
    [InlineData("No route to host")]
    [InlineData("Failed to connect to host")]
    [InlineData("Permission denied, please try again.")]
    [InlineData("Authentication failed")]
    public void ContainsSftpInputError_Markers_Match(string stderr)
    {
        Assert.True(FfmpegErrorMarkers.ContainsSftpInputError(stderr));
    }

    [Theory]
    [InlineData("Cannot determine format of input 0:0 after EOF")]
    [InlineData("Conversion failed!")]
    [InlineData("")]
    [InlineData(null)]
    public void ContainsSftpInputError_NoMarkers_NoMatch(string? stderr)
    {
        Assert.False(FfmpegErrorMarkers.ContainsSftpInputError(stderr));
    }

    [Theory]
    [InlineData("sftp://u:p@h/f.mkv: Protocol not found")]
    [InlineData("Unknown protocol 'sftp'")]
    [InlineData("Protocol not on whitelist 'file,crypto,data'")]
    public void ContainsUnsupportedSchemeError_ProtocolMarkers_Match(string stderr)
    {
        Assert.True(FfmpegErrorMarkers.ContainsUnsupportedSchemeError(stderr));
    }

    [Theory]
    [InlineData("Connection refused")]
    [InlineData("")]
    [InlineData(null)]
    public void ContainsUnsupportedSchemeError_NoMarkers_NoMatch(string? stderr)
    {
        Assert.False(FfmpegErrorMarkers.ContainsUnsupportedSchemeError(stderr));
    }
}
