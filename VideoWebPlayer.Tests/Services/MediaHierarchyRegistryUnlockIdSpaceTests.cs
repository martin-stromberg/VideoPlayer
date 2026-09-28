using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Pins which unlock id space each media type resolves to. <c>MovieCollections</c> and <c>TVShows</c> are two
/// independent tables that both start at id 1, so mixing the spaces up lets a non-unlocked title look unlocked
/// (the id-space collision found in Entwicklungsschritt 3).
/// </summary>
public class MediaHierarchyRegistryUnlockIdSpaceTests
{
    /// <summary>
    /// Every media type resolves to exactly the id space its unlock entries are stored in.
    /// </summary>
    /// <param name="mediaType">The media type.</param>
    /// <param name="expected">The name of the id space (the enum is internal, so the theory passes its member name).</param>
    [Theory]
    [InlineData(MediaType.Movie, "MovieCollection")]
    [InlineData(MediaType.MovieCollection, "MovieCollection")]
    [InlineData(MediaType.TVShow, "TVShow")]
    [InlineData(MediaType.TVShowSeason, "TVShow")]
    [InlineData(MediaType.TVShowEpisode, "TVShow")]
    public void EveryMediaType_ResolvesToItsUnlockIdSpace(MediaType mediaType, string expected)
    {
        Assert.Equal(Enum.Parse<UnlockIdSpace>(expected), MediaHierarchyRegistry.Handlers[mediaType].UnlockIdSpace);
    }

    [Fact]
    public void EveryMediaTypeHasAHandler_AndBothIdSpacesAreInUse()
    {
        foreach (var mediaType in Enum.GetValues<MediaType>())
            Assert.True(MediaHierarchyRegistry.Handlers.ContainsKey(mediaType), $"Kein Handler für {mediaType}.");

        var spaces = MediaHierarchyRegistry.Handlers.Values.Select(h => h.UnlockIdSpace).Distinct().ToList();
        Assert.Contains(UnlockIdSpace.MovieCollection, spaces);
        Assert.Contains(UnlockIdSpace.TVShow, spaces);
    }
}
