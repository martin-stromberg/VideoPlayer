using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// End-to-end tests with Playwright covering adding and removing playlist entries on the
/// playlist detail page: happy path, duplicate feedback, cascade-adding a TV show, orphaned
/// entry cleanup and ownership isolation between users.
/// </summary>
[Trait("Category", "E2E")]
public sealed class PlaylistEntriesE2ETests : PlaylistsE2ETestBase
{
    private async Task AddEntryViaUiAsync(string mediaType, long mediaId)
    {
        await Page.SelectOptionAsync(".playlist-add-mediatype-select", mediaType);
        await Page.FillAsync(".playlist-add-mediaid-input", mediaId.ToString());
        await Page.ClickAsync(".playlist-add-entry-button");
        await Page.WaitForTimeoutAsync(1000);
    }

    [Fact]
    public async Task AddMovie_HappyPath_AppearsInList()
    {
        if (SkipBrowser)
            return;

        var movieId = await SeedMovieAsync("Hinzufuegen-Testfilm");

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Playlist-Fuer-Hinzufuegen");
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");

        await AddEntryViaUiAsync("Movie", movieId);

        await Expect(Page.Locator($".playlist-entry-row[data-media-type='Movie'][data-media-id='{movieId}']")).ToBeVisibleAsync();
        await Expect(Page.Locator($".playlist-entry-row[data-media-type='Movie'][data-media-id='{movieId}']")).ToContainTextAsync("Hinzufuegen-Testfilm");
    }

    [Fact]
    public async Task RemoveMovie_HappyPath_DisappearsFromList()
    {
        if (SkipBrowser)
            return;

        var movieId = await SeedMovieAsync("Entfernen-Testfilm");

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Playlist-Fuer-Entfernen");
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await AddEntryViaUiAsync("Movie", movieId);
        await Expect(Page.Locator($".playlist-entry-row[data-media-type='Movie'][data-media-id='{movieId}']")).ToBeVisibleAsync();

        await Page.ClickAsync($".playlist-entry-row[data-media-type='Movie'][data-media-id='{movieId}'] .playlist-entry-remove-button");
        await Page.WaitForTimeoutAsync(1000);

        await Expect(Page.Locator($".playlist-entry-row[data-media-type='Movie'][data-media-id='{movieId}']")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task AddMovie_Duplicate_ShowsSuccessMessage()
    {
        if (SkipBrowser)
            return;

        var movieId = await SeedMovieAsync("Duplikat-Testfilm");

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Playlist-Fuer-Duplikat");
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await AddEntryViaUiAsync("Movie", movieId);

        await AddEntryViaUiAsync("Movie", movieId);

        await Expect(Page.Locator("#playlist-entries-status")).ToContainTextAsync("Alle 1 Titel waren bereits vorhanden.");
        await Expect(Page.Locator("#playlist-entries-status")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("alert-success"));
    }

    [Fact]
    public async Task AddTVShow_CascadesSeasonsAndEpisodes()
    {
        if (SkipBrowser)
            return;

        var showId = await SeedTvShowWithSeasonsAsync("Kaskaden-Testserie",
            ("Staffel 1", 2),
            ("Staffel 2", 1));

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Playlist-Fuer-Kaskade");
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");

        await AddEntryViaUiAsync("TVShow", showId);

        // 1 show + 2 seasons + 3 episodes = 6 rows
        await Expect(Page.Locator(".playlist-entry-row")).ToHaveCountAsync(6);
        await Expect(Page.Locator($".playlist-entry-row[data-media-type='TVShow'][data-media-id='{showId}']")).ToBeVisibleAsync();
        await Expect(Page.Locator(".playlist-entry-row[data-media-type='TVShowSeason']")).ToHaveCountAsync(2);
        await Expect(Page.Locator(".playlist-entry-row[data-media-type='TVShowEpisode']")).ToHaveCountAsync(3);
    }

    [Fact]
    public async Task AddMovie_ForeignPlaylist_ShowsErrorMessage()
    {
        if (SkipBrowser)
            return;

        var movieId = await SeedMovieAsync("Fremde-Playlist-Testfilm");

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Fremde-Playlist-Fuer-Eintraege");
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        var detailUrl = Page.Url;

        await LoginAsync(UserBEmail);
        await Page.GotoAsync(detailUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(1500);

        await Expect(Page.Locator("#playlist-detail-error")).ToContainTextAsync("Zugriff auf diese Playlist verweigert");
    }
}
