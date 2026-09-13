using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// End-to-end tests with Playwright covering playback of titles from a playlist (Schritt 5): starting
/// playback, manual Next/Previous navigation, automatic advance on title end, end-of-playlist behavior,
/// skipping locked/collection entries, the playlist badge display, playback outside of a playlist, and
/// browser-reload context recovery.
/// </summary>
[Trait("Category", "E2E")]
public sealed class PlaylistPlaybackE2ETests : PlaylistsE2ETestBase
{
    /// <summary>
    /// Synthesizes the HTML5 <c>ended</c> event on the video element via JS, since the test environment
    /// has no real SFTP-backed media source to actually play a video to completion. This exercises the
    /// same <c>@onended</c>-triggered <c>OnMediaEndAsync</c> code path a real playback end would.
    /// </summary>
    private Task DispatchVideoEndedEventAsync()
        => Page.EvalOnSelectorAsync("#video-player-element", "el => el.dispatchEvent(new Event('ended'))");

    [Fact]
    public async Task PlaylistPlaybackStartTest()
    {
        if (SkipBrowser)
            return;

        var movieId = await SeedMovieAsync("Wiedergabe-Startfilm");
        await LoginAsync(UserAEmail);
        await GrantMediaSourceAccessForUserAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Meine Favoriten");
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await SelectSearchResultAsync("Wiedergabe-Startfilm", "Movie", movieId);

        await Page.ClickAsync(".playlist-entry-play-button");
        await Page.WaitForSelectorAsync("#video-player-element");

        await Expect(Page.Locator("#playlist-playback-badge")).ToBeVisibleAsync();
        await Expect(Page.Locator("#playlist-playback-badge")).ToContainTextAsync("Meine Favoriten: 1/1");
        await Expect(Page.Locator("#video-player-element")).ToHaveAttributeAsync("src", new System.Text.RegularExpressions.Regex($"/api/items/movie/{movieId}/stream"));
    }

    [Fact]
    public async Task PlaylistNextEntryE2ETest()
    {
        if (SkipBrowser)
            return;

        var movie1Id = await SeedMovieAsync("Weiterschalten-Film-1");
        await LoginAsync(UserAEmail);
        await GrantMediaSourceAccessForUserAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Weiterschalten-Playlist");
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await SelectSearchResultAsync("Weiterschalten-Film-1", "Movie", movie1Id);
        var movie2Id = await SeedMovieAsync("Weiterschalten-Film-2");
        await SelectSearchResultAsync("Weiterschalten-Film-2", "Movie", movie2Id);

        await Page.Locator($".playlist-entry-row[data-media-type='Movie'][data-media-id='{movie1Id}'] .playlist-entry-play-button").ClickAsync();
        await Page.WaitForSelectorAsync("#video-player-element");
        await Expect(Page.Locator("#playlist-playback-badge")).ToContainTextAsync("1/2");

        await Page.ClickAsync(".playlist-next-button");
        await Page.WaitForTimeoutAsync(500);

        await Expect(Page.Locator("#playlist-playback-badge")).ToContainTextAsync("2/2");
        await Expect(Page.Locator("#video-player-element")).ToHaveAttributeAsync("src", new System.Text.RegularExpressions.Regex($"/api/items/movie/{movie2Id}/stream"));
    }

    [Fact]
    public async Task PlaylistPreviousEntryE2ETest()
    {
        if (SkipBrowser)
            return;

        var movie1Id = await SeedMovieAsync("Zurueckschalten-Film-1");
        await LoginAsync(UserAEmail);
        await GrantMediaSourceAccessForUserAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Zurueckschalten-Playlist");
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await SelectSearchResultAsync("Zurueckschalten-Film-1", "Movie", movie1Id);
        var movie2Id = await SeedMovieAsync("Zurueckschalten-Film-2");
        await SelectSearchResultAsync("Zurueckschalten-Film-2", "Movie", movie2Id);

        await Page.Locator($".playlist-entry-row[data-media-type='Movie'][data-media-id='{movie2Id}'] .playlist-entry-play-button").ClickAsync();
        await Page.WaitForSelectorAsync("#video-player-element");
        await Expect(Page.Locator("#playlist-playback-badge")).ToContainTextAsync("2/2");

        await Page.ClickAsync(".playlist-previous-button");
        await Page.WaitForTimeoutAsync(500);

        await Expect(Page.Locator("#playlist-playback-badge")).ToContainTextAsync("1/2");
        await Expect(Page.Locator("#video-player-element")).ToHaveAttributeAsync("src", new System.Text.RegularExpressions.Regex($"/api/items/movie/{movie1Id}/stream"));
    }

    [Fact]
    public async Task PlaylistAutoAdvanceE2ETest()
    {
        if (SkipBrowser)
            return;

        var movie1Id = await SeedMovieAsync("Autoadvance-Film-1");
        await LoginAsync(UserAEmail);
        await GrantMediaSourceAccessForUserAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Autoadvance-Playlist");
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await SelectSearchResultAsync("Autoadvance-Film-1", "Movie", movie1Id);
        var movie2Id = await SeedMovieAsync("Autoadvance-Film-2");
        await SelectSearchResultAsync("Autoadvance-Film-2", "Movie", movie2Id);

        await Page.Locator($".playlist-entry-row[data-media-type='Movie'][data-media-id='{movie1Id}'] .playlist-entry-play-button").ClickAsync();
        await Page.WaitForSelectorAsync("#video-player-element");
        await Expect(Page.Locator("#playlist-playback-badge")).ToContainTextAsync("1/2");

        await DispatchVideoEndedEventAsync();
        await Page.WaitForTimeoutAsync(500);

        await Expect(Page.Locator("#playlist-playback-badge")).ToContainTextAsync("2/2");
        await Expect(Page.Locator("#video-player-element")).ToHaveAttributeAsync("src", new System.Text.RegularExpressions.Regex($"/api/items/movie/{movie2Id}/stream"));
    }

    [Fact]
    public async Task PlaylistEndBehaviorE2ETest()
    {
        if (SkipBrowser)
            return;

        var movie1Id = await SeedMovieAsync("Ende-Film-1");
        await LoginAsync(UserAEmail);
        await GrantMediaSourceAccessForUserAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Ende-Playlist");
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await SelectSearchResultAsync("Ende-Film-1", "Movie", movie1Id);

        await Page.Locator($".playlist-entry-row[data-media-type='Movie'][data-media-id='{movie1Id}'] .playlist-entry-play-button").ClickAsync();
        await Page.WaitForSelectorAsync("#video-player-element");
        await Expect(Page.Locator("#playlist-playback-badge")).ToContainTextAsync("1/1");

        await Page.ClickAsync(".playlist-next-button");
        await Page.WaitForTimeoutAsync(500);

        await Expect(Page.Locator("#playlist-end-reached")).ToBeVisibleAsync();
        await Expect(Page.Locator("#playlist-end-reached")).ToContainTextAsync("Ende der Playlist erreicht");
        // The player must still be present (no crash) and still shows the last playable title.
        await Expect(Page.Locator("#video-player-element")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task PlaylistSkipLockedEntriesE2ETest()
    {
        if (SkipBrowser)
            return;

        var movie1Id = await SeedMovieAsync("Uebersprung-Film-1");
        await LoginAsync(UserAEmail);
        await GrantMediaSourceAccessForUserAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Uebersprung-Playlist");
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await SelectSearchResultAsync("Uebersprung-Film-1", "Movie", movie1Id);
        // Locked movie between the two accessible ones, seeded directly (never granted source access).
        // Must be seeded (and thus AddedAt-stamped) before the third movie is added, since the default
        // sort mode falls back to AddedAt order when release dates tie (all null here) - seeding it
        // afterwards would place it last instead of between the two accessible movies.
        await SeedLockedMovieIntoPlaylistAsync("Uebersprung-Playlist", "Uebersprung-Film-2-Gesperrt");
        var movie3Id = await SeedMovieAsync("Uebersprung-Film-3");
        await SelectSearchResultAsync("Uebersprung-Film-3", "Movie", movie3Id);
        await Page.ReloadAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");

        await Page.Locator($".playlist-entry-row[data-media-type='Movie'][data-media-id='{movie1Id}'] .playlist-entry-play-button").ClickAsync();
        await Page.WaitForSelectorAsync("#video-player-element");

        await Page.ClickAsync(".playlist-next-button");
        await Page.WaitForTimeoutAsync(500);

        // The locked movie is skipped entirely; navigation lands directly on the third (accessible) movie.
        await Expect(Page.Locator("#video-player-element")).ToHaveAttributeAsync("src", new System.Text.RegularExpressions.Regex($"/api/items/movie/{movie3Id}/stream"));
        // The playlist has 3 entries in total (including the skipped locked movie), so the badge must show
        // the third movie's actual position (3/3), not the previous position incremented by one (2/3).
        await Expect(Page.Locator("#playlist-playback-badge")).ToContainTextAsync("3/3");
    }

    [Fact]
    public async Task PlaylistBadgeDisplayE2ETest()
    {
        if (SkipBrowser)
            return;

        var movieId = await SeedMovieAsync("Badge-Film");
        await LoginAsync(UserAEmail);
        await GrantMediaSourceAccessForUserAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Badge-Anzeige-Playlist");
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await SelectSearchResultAsync("Badge-Film", "Movie", movieId);

        await Page.ClickAsync(".playlist-entry-play-button");
        await Page.WaitForSelectorAsync("#video-player-element");

        await Expect(Page.Locator("#playlist-playback-badge")).ToBeVisibleAsync();
        await Expect(Page.Locator("#playlist-playback-badge")).ToContainTextAsync("Badge-Anzeige-Playlist: 1/1");
        await Expect(Page.Locator(".playlist-next-button")).ToBeVisibleAsync();
        await Expect(Page.Locator(".playlist-previous-button")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task VideoPlaybackOutsidePlaylistE2ETest()
    {
        if (SkipBrowser)
            return;

        var movieId = await SeedMovieCollectionAsync("Bibliothek-Sammlung");
        await LoginAsync(UserAEmail);
        await GrantMediaSourceAccessForUserAsync(UserAEmail);

        await Page.GotoAsync($"{ServerUrl}/moviecollection/{movieId}");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(1000);

        // No playlist context: neither the badge nor the playlist navigation buttons should be present,
        // regardless of whether a player is currently shown.
        await Expect(Page.Locator("#playlist-playback-badge")).ToHaveCountAsync(0);
        await Expect(Page.Locator(".playlist-next-button")).ToHaveCountAsync(0);
        await Expect(Page.Locator(".playlist-previous-button")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task PlaylistSkipCollectionEntriesE2ETest()
    {
        if (SkipBrowser)
            return;

        var movie1Id = await SeedMovieAsync("Sammel-Uebersprung-Film-1");
        await LoginAsync(UserAEmail);
        await GrantMediaSourceAccessForUserAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Sammel-Uebersprung-Playlist");
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await SelectSearchResultAsync("Sammel-Uebersprung-Film-1", "Movie", movie1Id);
        // A TVShow entry seeded directly between the two movies (not resolvable via the cascade-add UI,
        // simulating a collection entry directly present in the playlist). Must be seeded (and thus
        // AddedAt-stamped) before the second movie is added, since the default sort mode falls back to
        // AddedAt order when release dates tie (all null here) - seeding it afterwards would place it
        // last instead of between the two movies.
        await SeedTvShowIntoPlaylistAsync("Sammel-Uebersprung-Playlist", "Sammel-Uebersprung-Serie");
        var movie2Id = await SeedMovieAsync("Sammel-Uebersprung-Film-2");
        await SelectSearchResultAsync("Sammel-Uebersprung-Film-2", "Movie", movie2Id);
        await Page.ReloadAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");

        await Page.Locator($".playlist-entry-row[data-media-type='Movie'][data-media-id='{movie1Id}'] .playlist-entry-play-button").ClickAsync();
        await Page.WaitForSelectorAsync("#video-player-element");

        await Page.ClickAsync(".playlist-next-button");
        await Page.WaitForTimeoutAsync(500);

        await Expect(Page.Locator("#video-player-element")).ToHaveAttributeAsync("src", new System.Text.RegularExpressions.Regex($"/api/items/movie/{movie2Id}/stream"));
        // The playlist has 3 entries in total (including the skipped, non-playable TVShow entry), so the
        // badge must show the second movie's actual position (3/3), not the previous position incremented
        // by one (2/3).
        await Expect(Page.Locator("#playlist-playback-badge")).ToContainTextAsync("3/3");
    }

    [Fact]
    public async Task PlaylistReloadContextRecoveryE2ETest()
    {
        if (SkipBrowser)
            return;

        var movie1Id = await SeedMovieAsync("Reload-Film-1");
        await LoginAsync(UserAEmail);
        await GrantMediaSourceAccessForUserAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Reload-Playlist");
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await SelectSearchResultAsync("Reload-Film-1", "Movie", movie1Id);
        var movie2Id = await SeedMovieAsync("Reload-Film-2");
        await SelectSearchResultAsync("Reload-Film-2", "Movie", movie2Id);

        await Page.Locator($".playlist-entry-row[data-media-type='Movie'][data-media-id='{movie1Id}'] .playlist-entry-play-button").ClickAsync();
        await Page.WaitForSelectorAsync("#video-player-element");
        await Page.ClickAsync(".playlist-next-button");
        await Page.WaitForTimeoutAsync(500);
        await Expect(Page.Locator("#playlist-playback-badge")).ToContainTextAsync("2/2");
        Assert.Contains("entryId=", Page.Url);

        await Page.ReloadAsync();
        await Page.WaitForSelectorAsync("#video-player-element");
        await Page.WaitForTimeoutAsync(500);

        await Expect(Page.Locator("#playlist-playback-badge")).ToContainTextAsync("2/2");
        await Expect(Page.Locator("#video-player-element")).ToHaveAttributeAsync("src", new System.Text.RegularExpressions.Regex($"/api/items/movie/{movie2Id}/stream"));
    }
}
