using VideoWebPlayer.Components.Pages.TV;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Regressionstest für den access_token-Fix: <see cref="TVShowDetails.BuildEpisodeBackgroundImageUrl"/>
/// ist die von <c>GetHeaderBackgroundUrl()</c> verwendete, reine Hilfsmethode zum Aufbau der
/// Hintergrundbild-URL und wird dank <c>InternalsVisibleTo</c> direkt getestet (ohne Reflection).
/// </summary>
public sealed class TVShowDetailsBackgroundImageUrlBuilderTests
{
    [Fact]
    public void BuildEpisodeBackgroundImageUrl_AppendsAccessToken()
    {
        var url = TVShowDetails.BuildEpisodeBackgroundImageUrl(42, "token-123");

        Assert.Equal("/api/episodes/42/background-image?access_token=token-123", url);
    }

    [Fact]
    public void BuildEpisodeBackgroundImageUrl_WithNullAccessToken_StillIncludesQueryParameter()
    {
        var url = TVShowDetails.BuildEpisodeBackgroundImageUrl(42, null);

        Assert.Equal("/api/episodes/42/background-image?access_token=", url);
    }
}
