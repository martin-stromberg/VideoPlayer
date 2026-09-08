using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// End-to-end tests with Playwright covering the <c>MediaSearchSelector</c> name-search experience for
/// adding playlist entries: cascade behavior for TV show seasons/episodes, movie collections, access
/// control (a medium the user cannot access must not appear in search results) and the empty-results
/// message. The plain movie/TV-show happy-path and cascade scenarios are covered by
/// <see cref="PlaylistEntriesE2ETests"/>, which already exercises the same search UI.
/// </summary>
[Trait("Category", "E2E")]
public sealed class PlaylistMediaSearchE2ETests : PlaylistsE2ETestBase
{
    [Fact]
    public async Task SelectSeason_CascadesOnlyThatSeasonsEpisodes()
    {
        if (SkipBrowser)
            return;

        var (showId, seasonId, episodeIds) = await SeedTvShowWithSingleSeasonAsync("Staffel-Suchserie", "Gesuchte Staffel", 2);

        await LoginAsync(UserAEmail);
        await GrantMediaSourceAccessForUserAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Playlist-Fuer-Staffel-Suche");
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");

        await SelectSearchResultAsync("Gesuchte Staffel", "TVShowSeason", seasonId);

        // 1 season + 2 episodes = 3 rows; the show itself is not added.
        await Expect(Page.Locator(".playlist-entry-row")).ToHaveCountAsync(3);
        await Expect(Page.Locator($".playlist-entry-row[data-media-type='TVShowSeason'][data-media-id='{seasonId}']")).ToBeVisibleAsync();
        await Expect(Page.Locator(".playlist-entry-row[data-media-type='TVShowEpisode']")).ToHaveCountAsync(2);
        await Expect(Page.Locator($".playlist-entry-row[data-media-type='TVShow'][data-media-id='{showId}']")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task SelectEpisode_AddsOnlyThatEpisode_NoCascade()
    {
        if (SkipBrowser)
            return;

        var (_, _, episodeIds) = await SeedTvShowWithSingleSeasonAsync("Episoden-Suchserie", "Staffel Eins", 1);
        var episodeId = episodeIds[0];

        await LoginAsync(UserAEmail);
        await GrantMediaSourceAccessForUserAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Playlist-Fuer-Episoden-Suche");
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");

        await SelectSearchResultAsync("Staffel Eins Episode 1", "TVShowEpisode", episodeId);

        await Expect(Page.Locator(".playlist-entry-row")).ToHaveCountAsync(1);
        await Expect(Page.Locator($".playlist-entry-row[data-media-type='TVShowEpisode'][data-media-id='{episodeId}']")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task SelectMovieCollection_AddsCollection()
    {
        if (SkipBrowser)
            return;

        var collectionId = await SeedMovieCollectionAsync("Gesuchte Filmsammlung");

        await LoginAsync(UserAEmail);
        await GrantMediaSourceAccessForUserAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Playlist-Fuer-Sammlung-Suche");
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");

        await SelectSearchResultAsync("Gesuchte Filmsammlung", "MovieCollection", collectionId);

        await Expect(Page.Locator(".playlist-entry-row")).ToHaveCountAsync(1);
        await Expect(Page.Locator($".playlist-entry-row[data-media-type='MovieCollection'][data-media-id='{collectionId}']")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Search_UserWithoutAccess_MediumNotInResults()
    {
        if (SkipBrowser)
            return;

        await SeedMovieAsync("Nicht Zugreifbarer Film");

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Playlist-Fuer-Zugriffsschutz-Suche");
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");

        await Page.FillAsync(".media-search-input", "Nicht Zugreifbarer Film");
        await Page.WaitForTimeoutAsync(1000);

        await Expect(Page.Locator(".media-search-result")).ToHaveCountAsync(0);
        await Expect(Page.Locator(".media-search-empty")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Search_NoMatches_ShowsEmptyResultsMessage()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Playlist-Fuer-Leere-Suche");
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");

        await Page.FillAsync(".media-search-input", "Ein-Suchbegriff-Der-Garantiert-Nichts-Findet");
        await Page.WaitForTimeoutAsync(1000);

        await Expect(Page.Locator(".media-search-empty")).ToBeVisibleAsync();
    }
}
