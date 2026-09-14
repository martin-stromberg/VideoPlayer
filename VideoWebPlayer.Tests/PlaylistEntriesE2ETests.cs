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
    [Fact]
    public async Task AddMovie_HappyPath_AppearsInList()
    {
        if (SkipBrowser)
            return;

        var movieId = await SeedMovieAsync("Hinzufuegen-Testfilm");

        await LoginAsync(UserAEmail);
        await GrantMediaSourceAccessForUserAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Playlist-Fuer-Hinzufuegen");
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");

        await SelectSearchResultAsync("Hinzufuegen-Testfilm", "Movie", movieId);

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
        await GrantMediaSourceAccessForUserAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Playlist-Fuer-Entfernen");
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await SelectSearchResultAsync("Entfernen-Testfilm", "Movie", movieId);
        await Expect(Page.Locator($".playlist-entry-row[data-media-type='Movie'][data-media-id='{movieId}']")).ToBeVisibleAsync();

        await Page.ClickAsync($".playlist-entry-row[data-media-type='Movie'][data-media-id='{movieId}'] .playlist-entry-remove-button");
        await Page.WaitForTimeoutAsync(1000);

        await Expect(Page.Locator($".playlist-entry-row[data-media-type='Movie'][data-media-id='{movieId}']")).ToHaveCountAsync(0);
    }

    /// <summary>
    /// Verifies the corrected `hasSourceAccess OR isUnlocked` accessibility rule for newly added
    /// entries: a movie the user has regular access to via
    /// <see cref="VideoWebPlayer.Data.MediaSourceUser"/> is rendered without the reduced-opacity
    /// styling right after being added, even without an explicit individual unlock.
    /// </summary>
    [Fact]
    public async Task AddMovie_WithSourceAccess_AppearsAccessible()
    {
        if (SkipBrowser)
            return;

        var movieId = await SeedMovieAsync("Quellenzugriff-Testfilm");

        await LoginAsync(UserAEmail);
        await GrantMediaSourceAccessForUserAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Playlist-Fuer-Quellenzugriff");
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");

        await SelectSearchResultAsync("Quellenzugriff-Testfilm", "Movie", movieId);

        await Expect(Page.Locator($".playlist-entry-row[data-media-type='Movie'][data-media-id='{movieId}']"))
            .Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("opacity-50"));
    }

    [Fact]
    public async Task AddMovie_Duplicate_ShowsSuccessMessage()
    {
        if (SkipBrowser)
            return;

        var movieId = await SeedMovieAsync("Duplikat-Testfilm");

        await LoginAsync(UserAEmail);
        await GrantMediaSourceAccessForUserAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Playlist-Fuer-Duplikat");
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await SelectSearchResultAsync("Duplikat-Testfilm", "Movie", movieId);

        await SelectSearchResultAsync("Duplikat-Testfilm", "Movie", movieId);

        await Expect(Page.Locator("#playlist-entries-status")).ToContainTextAsync("Alle 1 Titel waren bereits vorhanden.");
        await Expect(Page.Locator("#playlist-entries-status")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("alert-success"));
    }

    /// <summary>
    /// Verifies that adding a whole TV show still cascade-adds the show itself and all of its seasons
    /// server-side (proven by the still-3 episode tiles rendering correctly with resolved parent-show
    /// context - see <c>PlaylistServiceTests_AddMedia</c> for the full 1+2+3 = 6-entry server-side
    /// assertion), but per the Kachel-UI redesign (Kundenfeedback: "Wurde eine ganze Serie hinzugefuegt,
    /// so darf es keine Kachel fuer die Serie selbst oder die Staffeln geben") only the resulting episodes
    /// get their own tile in the UI - the TVShow and TVShowSeason entries stay purely organizational and
    /// render no tile at all (see <c>PlaylistEntriesList.IsRenderableEntry</c>).
    /// </summary>
    [Fact]
    public async Task AddTVShow_CascadesSeasonsAndEpisodes()
    {
        if (SkipBrowser)
            return;

        var showId = await SeedTvShowWithSeasonsAsync("Kaskaden-Testserie",
            ("Staffel 1", 2),
            ("Staffel 2", 1));

        await LoginAsync(UserAEmail);
        await GrantMediaSourceAccessForUserAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Playlist-Fuer-Kaskade");
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");

        await SelectSearchResultAsync("Kaskaden-Testserie", "TVShow", showId);

        // Nur die 3 Episoden erhalten eine Kachel; die Serie selbst und ihre 2 Staffeln (rein
        // organisatorisch im Hintergrund weiterhin gespeichert) bleiben ohne eigene Kachel.
        await Expect(Page.Locator(".playlist-entry-row")).ToHaveCountAsync(3);
        await Expect(Page.Locator($".playlist-entry-row[data-media-type='TVShow'][data-media-id='{showId}']")).ToHaveCountAsync(0);
        await Expect(Page.Locator(".playlist-entry-row[data-media-type='TVShowSeason']")).ToHaveCountAsync(0);
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
