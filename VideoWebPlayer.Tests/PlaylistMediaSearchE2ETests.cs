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
    /// <summary>
    /// Verifies that adding a season cascade-adds only that season's episodes server-side (the season
    /// itself is added too, as organizational bookkeeping, and the show is not added at all). Per the
    /// Kachel-UI redesign (see <c>PlaylistEntriesList.IsRenderableEntry</c>), only the 2 resulting
    /// episodes render a tile - the season entry stays purely organizational and renders no tile.
    /// </summary>
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

        // Nur die 2 Episoden erhalten eine Kachel; die Staffel selbst (organisatorisch mitgespeichert)
        // und die Serie (gar nicht hinzugefuegt) bleiben ohne eigene Kachel.
        await Expect(Page.Locator(".playlist-entry-row")).ToHaveCountAsync(2);
        await Expect(Page.Locator($".playlist-entry-row[data-media-type='TVShowSeason'][data-media-id='{seasonId}']")).ToHaveCountAsync(0);
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

    /// <summary>
    /// Verifies that adding an (empty) movie collection succeeds server-side (confirmed via the
    /// "hinzugefuegt" success status, since a collection without movies has no cascade children to prove
    /// the addition via). Per the Kachel-UI redesign (see <c>PlaylistEntriesList.IsRenderableEntry</c>),
    /// a MovieCollection entry - organizational only, like a TVShow/TVShowSeason - renders no tile.
    /// </summary>
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

        await Expect(Page.Locator("#playlist-entries-status")).ToContainTextAsync("1 Titel hinzugefuegt.");
        await Expect(Page.Locator(".playlist-entry-row")).ToHaveCountAsync(0);
        await Expect(Page.Locator($".playlist-entry-row[data-media-type='MovieCollection'][data-media-id='{collectionId}']")).ToHaveCountAsync(0);
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

    [Fact]
    public async Task AddMedia_SearchCaseInsensitive_FindsMedia()
    {
        if (SkipBrowser)
            return;

        var movieId = await SeedMovieAsync("Breaking Bad");

        await LoginAsync(UserAEmail);
        await GrantMediaSourceAccessForUserAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Playlist-Fuer-GrossKleinschreibung-Suche");
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");

        await SelectSearchResultAsync("breaking bad", "Movie", movieId);

        await Expect(Page.Locator($".playlist-entry-row[data-media-type='Movie'][data-media-id='{movieId}']")).ToBeVisibleAsync();
        await Expect(Page.Locator($".playlist-entry-row[data-media-type='Movie'][data-media-id='{movieId}']")).ToContainTextAsync("Breaking Bad");
    }

    [Fact]
    public async Task AddMedia_Search_ReturnsAll5Types()
    {
        if (SkipBrowser)
            return;

        var movieId = await SeedMovieAsync("Fuenftypen Film");
        var collectionId = await SeedMovieCollectionAsync("Fuenftypen Sammlung");
        var (showId, seasonId, episodeIds) = await SeedTvShowWithSingleSeasonAsync("Fuenftypen Serie", "Fuenftypen Staffel", 1);

        await LoginAsync(UserAEmail);
        await GrantMediaSourceAccessForUserAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Playlist-Fuer-Fuenf-Typen-Suche");
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");

        await Page.FillAsync(".media-search-input", "Fuenftypen");
        await Page.WaitForTimeoutAsync(1000);

        await Expect(Page.Locator($".media-search-result[data-media-type='Movie'][data-media-id='{movieId}']")).ToBeVisibleAsync();
        await Expect(Page.Locator($".media-search-result[data-media-type='MovieCollection'][data-media-id='{collectionId}']")).ToBeVisibleAsync();
        await Expect(Page.Locator($".media-search-result[data-media-type='TVShow'][data-media-id='{showId}']")).ToBeVisibleAsync();
        await Expect(Page.Locator($".media-search-result[data-media-type='TVShowSeason'][data-media-id='{seasonId}']")).ToBeVisibleAsync();
        await Expect(Page.Locator($".media-search-result[data-media-type='TVShowEpisode'][data-media-id='{episodeIds[0]}']")).ToBeVisibleAsync();
    }
}
