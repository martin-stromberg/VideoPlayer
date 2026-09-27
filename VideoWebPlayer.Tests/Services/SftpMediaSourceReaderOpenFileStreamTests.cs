using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Covers <see cref="SftpMediaSourceReader.OpenFileStream"/> for a media item without a file name (an
/// empty <c>fileName</c> would otherwise combine with the collection path to the collection's own
/// directory, and <c>SftpClient.OpenRead</c> on a directory throws instead of returning a stream). The
/// media source in these tests points at an unreachable host on purpose: the fix must return
/// <c>null</c> before any connection is attempted, so the test never depends on network access. Without
/// the fix, <c>OpenFileStream</c> tries to connect and the test fails with a connection exception
/// instead of the expected <c>null</c> (see <c>continue.md</c> Gegenprobe).
/// </summary>
public class SftpMediaSourceReaderOpenFileStreamTests
{
    [Fact]
    public void OpenFileStream_WithEmptyFileName_ReturnsNullWithoutConnecting()
    {
        var reader = new SftpMediaSourceReader();
        var collection = CreateCollection();

        var result = reader.OpenFileStream(collection, string.Empty);

        Assert.Null(result);
    }

    [Fact]
    public void OpenFileStream_WithWhitespaceFileName_ReturnsNullWithoutConnecting()
    {
        var reader = new SftpMediaSourceReader();
        var collection = CreateCollection();

        var result = reader.OpenFileStream(collection, "   ");

        Assert.Null(result);
    }

    private static MediaCollection CreateCollection()
    {
        var source = new MediaSource
        {
            Name = "Unreachable",
            Host = "host.invalid",
            Port = 22,
            Username = "user",
            Password = "pass",
            SourceType = MediaSourceType.Sftp
        };

        return new MediaCollection
        {
            Name = "Collection",
            Path = "/remote/collection",
            MediaSource = source,
            MediaSourceId = source.Id
        };
    }
}
