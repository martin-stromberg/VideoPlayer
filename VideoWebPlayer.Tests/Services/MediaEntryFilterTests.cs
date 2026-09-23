using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

// Regressionstest für die zentralisierte Ignorier-Regel: IsIgnoredEntry war zuvor
// identisch in LocalMediaSourceReader und SftpMediaSourceReader dupliziert.
public class MediaEntryFilterTests
{
    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData(".actors")]
    [InlineData(".hidden")]
    public void IsIgnoredEntry_DotPrefixedName_ReturnsTrue(string name)
    {
        Assert.True(MediaEntryFilter.IsIgnoredEntry(name));
    }

    [Theory]
    [InlineData("movie.mp4")]
    [InlineData("folder")]
    [InlineData("file.txt.bak")]
    public void IsIgnoredEntry_RegularName_ReturnsFalse(string name)
    {
        Assert.False(MediaEntryFilter.IsIgnoredEntry(name));
    }
}
