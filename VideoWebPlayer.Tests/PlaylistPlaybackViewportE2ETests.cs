using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Regression test for the layout of the player's error message: it is shown whenever the browser cannot
/// load the stream — which in the test environment is always, since no real media source backs the
/// titles. As a block in the text flow it pushed the playlist controls out of the visible area and made
/// them unclickable. The message therefore has to be an overlay that takes no height
/// (<c>.player-stream-error</c> in <c>app.css</c>); this test fails if that rule is taken away.
/// </summary>
[Trait("Category", "E2E")]
public sealed class PlaylistPlaybackViewportE2ETests : PlaylistsE2ETestBase
{
    [Fact]
    public async Task StreamErrorMessage_KeepsPlaylistControlsInTheViewport()
    {
        if (SkipBrowser)
            return;

        var movieId = await SeedMovieAsync("Einblendung-Film-1");
        await LoginAsync(UserAEmail);
        await GrantMediaSourceAccessForUserAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Einblendung-Playlist");
        await row.ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await SelectSearchResultAsync("Einblendung-Film-1", "Movie", movieId);
        var secondMovieId = await SeedMovieAsync("Einblendung-Film-2");
        await SelectSearchResultAsync("Einblendung-Film-2", "Movie", secondMovieId);

        await PlayEntryFromHeaderAsync("Movie", movieId);
        await Page.WaitForSelectorAsync("#video-player-element");

        // The stream cannot be played here, so the message appears - exactly the situation in which the
        // controls must stay reachable.
        await Expect(Page.Locator("#player-stream-error")).ToBeVisibleAsync();
        await Expect(Page.Locator(".playlist-next-button")).ToBeInViewportAsync();
        await Expect(Page.Locator(".playlist-previous-button")).ToBeInViewportAsync();

        // The two assertions above only catch the symptom once the message is tall enough for this
        // window size. The load-bearing rule itself is that the message is taken out of the text flow,
        // so it is asserted directly and independently of the screen size.
        var position = await Page.EvalOnSelectorAsync<string>("#player-stream-error", "el => getComputedStyle(el).position");
        Assert.Equal("absolute", position);
    }
}
