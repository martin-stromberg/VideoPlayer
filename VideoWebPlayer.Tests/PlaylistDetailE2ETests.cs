using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// End-to-end tests with Playwright covering the playlist detail page: navigation from the
/// list, display of metadata, editing, deletion with confirmation, back navigation and error
/// handling for unauthenticated, foreign and nonexistent playlists.
/// </summary>
[Trait("Category", "E2E")]
public sealed class PlaylistDetailE2ETests : PlaylistsE2ETestBase
{
    [Fact]
    public async Task Load_Detail_Page_Unauthenticated_Shows_Error()
    {
        if (SkipBrowser)
            return;

        await Page.GotoAsync($"{ServerUrl}/playlists/1");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(1500);

        await Expect(Page.Locator("#playlist-detail-error")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Load_Detail_Page_ValidPlaylist_ShowsMetadata()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Metadaten-Test", "Beschreibung fuer Metadaten-Test");
        await row.ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");

        await Expect(Page.Locator("#playlist-detail-name")).ToHaveTextAsync("Metadaten-Test");
        await Expect(Page.Locator("#playlist-detail-description")).ToHaveTextAsync("Beschreibung fuer Metadaten-Test");
        await Expect(Page.Locator("#playlist-detail-sortmode")).ToHaveTextAsync("Nach Erscheinungsdatum");

        var createdText = await Page.Locator("#playlist-detail-created").InnerTextAsync();
        var updatedText = await Page.Locator("#playlist-detail-updated").InnerTextAsync();
        Assert.False(string.IsNullOrWhiteSpace(createdText));
        Assert.False(string.IsNullOrWhiteSpace(updatedText));
    }

    [Fact]
    public async Task Open_Button_In_List_Navigates_To_Detail()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Oeffnen-Navigation");
        await row.ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");

        await Expect(Page.Locator("#playlist-detail-name")).ToHaveTextAsync("Oeffnen-Navigation");
    }

    [Fact]
    public async Task Detail_Page_Edit_Opens_Form_And_Saves()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Bearbeiten-Von-Detail");
        await row.ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");

        await Page.ClickAsync(".playlist-detail-edit-button");
        await Page.WaitForSelectorAsync("#playlist-name-input");
        await Page.FillAsync("#playlist-name-input", "Bearbeiten-Von-Detail Aktualisiert");
        await Page.ClickAsync("#playlist-save-button");
        await Page.WaitForTimeoutAsync(1000);

        await Expect(Page.Locator("#playlist-detail-name")).ToHaveTextAsync("Bearbeiten-Von-Detail Aktualisiert");
    }

    [Fact]
    public async Task Detail_Page_Delete_Shows_Confirmation_And_Deletes()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Loeschen-Von-Detail");
        await row.ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");

        await Page.ClickAsync(".playlist-detail-delete-button");
        await Page.WaitForSelectorAsync("#confirm-delete-playlist-button");
        await Page.ClickAsync("#confirm-delete-playlist-button");
        await Page.WaitForTimeoutAsync(1000);

        var currentUrl = new Uri(Page.Url);
        Assert.Equal("/playlists", currentUrl.AbsolutePath);
        await Expect(Page.Locator(".playlist-row[data-playlist-name='Loeschen-Von-Detail']")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Detail_Page_Back_Button_Navigates_To_List()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Zurueck-Von-Detail");
        await row.ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");

        await Page.ClickAsync(".playlist-detail-back-button");
        await Page.WaitForTimeoutAsync(1000);

        var currentUrl = new Uri(Page.Url);
        Assert.Equal("/playlists", currentUrl.AbsolutePath);
    }

    [Fact]
    public async Task Detail_Page_Foreign_Playlist_Shows_403_Error()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Fremde-Playlist-Test");
        await row.ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");

        var detailUrl = Page.Url;

        await LoginAsync(UserBEmail);
        await Page.GotoAsync(detailUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(1500);

        await Expect(Page.Locator("#playlist-detail-error")).ToContainTextAsync("Zugriff auf diese Playlist verweigert");
    }

    [Fact]
    public async Task Detail_Page_Nonexistent_Playlist_Shows_404_Error()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        await Page.GotoAsync($"{ServerUrl}/playlists/999999999");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(1500);

        await Expect(Page.Locator("#playlist-detail-error")).ToContainTextAsync("Playlist nicht gefunden");
    }

    [Fact]
    public async Task PlaylistDetail_LoadsFirstPage_OnInitialize()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Infinity-Erste-Seite");
        await SeedMoviesIntoPlaylistAsync("Infinity-Erste-Seite", 25);
        await row.ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await Page.WaitForTimeoutAsync(1500);

        // Der statische Hinweistext ("Weitere Eintraege werden beim Scrollen geladen.") wurde per
        // Kundenfeedback entfernt (als selbstverstaendlich empfunden); das Laden in Seiten bleibt
        // funktional bestehen und zeigt sich hier daran, dass die erste gerenderte Teilmenge kleiner als
        // die Gesamtzahl der Eintraege ist.
        var renderedCount = await Page.Locator(".playlist-entry-row").CountAsync();
        Assert.True(renderedCount is > 0 and < 25, $"Erwartete eine virtualisierte Teilmenge der 25 Eintraege, aber es wurden {renderedCount} gerendert.");
    }

    [Fact]
    public async Task PlaylistDetail_LoadsNextPage_OnScrollNearEnd()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Infinity-Naechste-Seite");
        await SeedMoviesIntoPlaylistAsync("Infinity-Naechste-Seite", 25);
        await row.ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await Page.WaitForTimeoutAsync(1500);

        // Page 1 (pageSize 20) only ever contains media ids 1-20; a row referencing an id beyond
        // that can only appear once the component has lazy-loaded page 2 in reaction to scrolling.
        var beyondFirstPageRow = Page.Locator(".playlist-entry-row[data-media-id='21']");
        // Die Eintragsliste hat keine eigene Scrollbox mehr - die ganze Seite scrollt (wie auf der
        // Serien-Detailseite). Das Hovern ueber der Liste reicht dennoch: Wheel-Events auf einem nicht
        // scrollbaren Element gehen an den naechsten scrollbaren Vorfahren, d. h. das Dokument.
        var entriesArea = Page.Locator(".playlist-entries-list-wrap");
        await entriesArea.HoverAsync();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (await beyondFirstPageRow.CountAsync() == 0 && stopwatch.Elapsed < TimeSpan.FromSeconds(30))
        {
            await Page.Mouse.WheelAsync(0, 600);
            await Page.WaitForTimeoutAsync(200);
        }

        await Expect(beyondFirstPageRow).ToBeVisibleAsync();
    }

    [Fact]
    public async Task PlaylistDetail_StopsLoading_WhenHasNextPageFalse()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Infinity-Keine-Weiteren-Seiten");
        await SeedMoviesIntoPlaylistAsync("Infinity-Keine-Weiteren-Seiten", 5);
        await row.ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await Page.WaitForTimeoutAsync(1500);

        await Expect(Page.Locator(".playlist-entry-row")).ToHaveCountAsync(5);
        await Expect(Page.Locator("#playlist-entries-more-available")).ToHaveCountAsync(0);
    }

    // Hinweis zum UI-Redesign (Kundenfeedback): Seit der Umstellung auf die Kachel-UI erhalten nur noch
    // Filme und Episoden eine eigene Kachel (siehe PlaylistEntriesList.IsRenderableEntry); eine TVShow
    // rendert keine eigene Zeile mehr. Ein zuvor hier vorhandener Test, der eine explizit ueber
    // UnlockedMediaEntries freigeschaltete TVShow direkt als Zeile pruefte
    // ("DoesNotShowReducedOpacity_WhenEntryIsUnlocked"), ist damit ohne UI-Aequivalent - dieses Szenario
    // (Zugriff ueber die uebergeordnete Sammlung/Serie erben) deckt bereits
    // PlaylistDetail_DoesNotShowReducedOpacity_WhenMovieCollectionIsUnlocked bzw.
    // PlaylistDetail_DoesNotShowReducedOpacity_WhenEpisodeShowIsUnlocked unten ab. Die folgenden Tests
    // pruefen den gemischten Zugriffsstatus sowie den Entfernen-Button daher anhand von Filmen, die
    // (anders als eine TVShow) weiterhin eine eigene Kachel erhalten.

    /// <summary>
    /// Verifies the actual graying behavior for entries without real access: a movie seeded on a
    /// dedicated, never-granted media source is rendered with the reduced-opacity styling, while a
    /// sibling movie the user has regular source access to is not.
    /// </summary>
    [Fact]
    public async Task PlaylistDetail_ShowsReducedOpacity_WhenEntryNotAccessible()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Zugriffsstatus-Gemischt");
        var lockedMovieId = await SeedLockedMovieIntoPlaylistAsync("Zugriffsstatus-Gemischt", "Gesperrter Film");
        await GrantMediaSourceAccessForUserAsync(UserAEmail);
        var unlockedMovieId = await SeedMovieAsync("Freigeschalteter Film");
        await row.ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await SelectSearchResultAsync("Freigeschalteter Film", MediaTypeValues.Movie, unlockedMovieId);

        await Expect(Page.Locator($".playlist-entry-row[data-media-type='{MediaTypeValues.Movie}'][data-media-id='{lockedMovieId}']")).ToHaveClassAsync(new Regex("opacity-50"));
        await Expect(Page.Locator($".playlist-entry-row[data-media-type='{MediaTypeValues.Movie}'][data-media-id='{unlockedMovieId}']")).Not.ToHaveClassAsync(new Regex("opacity-50"));
    }

    /// <summary>
    /// Verifies that the "Entfernen" button stays enabled for inaccessible entries, so the playlist
    /// owner can still remove them even though they are grayed out.
    /// </summary>
    [Fact]
    public async Task PlaylistDetail_RemoveButton_EnabledForAllEntries()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Entfernen-Trotz-Sperre");
        var lockedMovieId = await SeedLockedMovieIntoPlaylistAsync("Entfernen-Trotz-Sperre", "Gesperrter Film");
        await row.ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await Page.WaitForTimeoutAsync(1500);

        var lockedRow = Page.Locator($".playlist-entry-row[data-media-type='{MediaTypeValues.Movie}'][data-media-id='{lockedMovieId}']");
        await Expect(lockedRow).ToHaveClassAsync(new Regex("opacity-50"));
        var removeButton = lockedRow.Locator(".playlist-entry-remove-button");
        await Expect(removeButton).ToBeVisibleAsync();
        await Expect(removeButton).ToBeEnabledAsync();

        await removeButton.ClickAsync();
        await Page.WaitForTimeoutAsync(1000);

        await Expect(Page.Locator($".playlist-entry-row[data-media-type='{MediaTypeValues.Movie}'][data-media-id='{lockedMovieId}']")).ToHaveCountAsync(0);
    }

    /// <summary>
    /// Verifies the corrected `hasSourceAccess OR isUnlocked` accessibility rule: a movie the user
    /// has regular access to via <see cref="VideoWebPlayer.Data.MediaSourceUser"/> is rendered without
    /// the reduced-opacity styling, even without an explicit individual unlock.
    /// </summary>
    [Fact]
    public async Task PlaylistDetail_DoesNotShowReducedOpacity_WhenUserHasSourceAccess()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Zugriffsstatus-Quellenzugriff");
        var movieId = await SeedMovieAsync("Film mit Quellenzugriff");
        await GrantMediaSourceAccessForUserAsync(UserAEmail);
        await row.ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await SelectSearchResultAsync("Film mit Quellenzugriff", MediaTypeValues.Movie, movieId);

        await Expect(Page.Locator($".playlist-entry-row[data-media-type='{MediaTypeValues.Movie}'][data-media-id='{movieId}']")).Not.ToHaveClassAsync(new Regex("opacity-50"));
    }

    /// <summary>
    /// Verifies the movie-to-collection unlock hierarchy resolution: a movie belonging to an unlocked
    /// movie collection is rendered without the reduced-opacity styling, even though the movie itself
    /// cannot be individually unlocked.
    /// </summary>
    [Fact]
    public async Task PlaylistDetail_DoesNotShowReducedOpacity_WhenMovieCollectionIsUnlocked()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Zugriffsstatus-Sammlung-Freigeschaltet");
        var (movieId, collectionId) = await SeedMovieInCollectionIntoPlaylistAsync(
            "Zugriffsstatus-Sammlung-Freigeschaltet", "Freigeschaltete Sammlung", "Film in Sammlung");
        await UnlockMediaForUserAsync(UserAEmail, MediaTypeValues.MovieCollection, collectionId);
        await row.ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await Page.WaitForTimeoutAsync(1500);

        await Expect(Page.Locator($".playlist-entry-row[data-media-id='{movieId}']")).Not.ToHaveClassAsync(new Regex("opacity-50"));
    }

    /// <summary>
    /// Verifies the episode-to-show unlock hierarchy resolution: a TV show episode whose show is
    /// unlocked is rendered without the reduced-opacity styling, even though the episode itself cannot
    /// be individually unlocked.
    /// </summary>
    [Fact]
    public async Task PlaylistDetail_DoesNotShowReducedOpacity_WhenEpisodeShowIsUnlocked()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Zugriffsstatus-Episode-Serie-Freigeschaltet");
        var (episodeId, showId) = await SeedTvShowEpisodeIntoPlaylistAsync(
            "Zugriffsstatus-Episode-Serie-Freigeschaltet", "Serie fuer Episode");
        await UnlockMediaForUserAsync(UserAEmail, MediaTypeValues.TVShow, showId);
        await row.ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await Page.WaitForTimeoutAsync(1500);

        await Expect(Page.Locator($".playlist-entry-row[data-media-id='{episodeId}']")).Not.ToHaveClassAsync(new Regex("opacity-50"));
    }

    /// <summary>
    /// Verifies that each playlist entry displays a title image: an entry with a poster picture
    /// resolves it via the pictures API, and an entry without one falls back to the placeholder image.
    /// </summary>
    [Fact]
    public async Task PlaylistDetail_DisplaysImageForEachEntry()
    {
        if (SkipBrowser)
            return;

        var failedImageResponses = new List<string>();
        Page.Response += (_, response) =>
        {
            if (response.Status >= 400 && response.Url.Contains("/api/pictures/"))
                failedImageResponses.Add($"{response.Status} {response.Url}");
        };

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Bildspalte-Test");
        var moviePosterId = await SeedMovieWithPosterIntoPlaylistAsync("Bildspalte-Test", "Film Mit Poster");
        // Eine Episode ohne eigenes Poster statt einer TVShow direkt: seit dem UI-Redesign (Kundenfeedback)
        // erhalten nur noch Filme/Episoden eine eigene Kachel, eine TVShow-Kachel gibt es nicht mehr (siehe
        // PlaylistEntriesList.IsRenderableEntry).
        var (episodeWithoutPosterId, _) = await SeedTvShowEpisodeIntoPlaylistAsync("Bildspalte-Test", "Serie Ohne Poster");
        await row.ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await Page.WaitForTimeoutAsync(1500);

        // Movie and TVShowEpisode ids are independently auto-incremented, so the media type must be
        // included in the selector to uniquely identify each row.
        var posterImage = Page.Locator($".playlist-entry-row[data-media-type='{MediaTypeValues.Movie}'][data-media-id='{moviePosterId}'] .playlist-entry-image");
        await Expect(posterImage).ToHaveCountAsync(1);
        await Expect(posterImage).ToHaveAttributeAsync("src", new Regex("/api/pictures/"));

        var placeholderImage = Page.Locator($".playlist-entry-row[data-media-type='{MediaTypeValues.TVShowEpisode}'][data-media-id='{episodeWithoutPosterId}'] .playlist-entry-image");
        await Expect(placeholderImage).ToHaveCountAsync(1);
        await Expect(placeholderImage).ToHaveAttributeAsync("src", new Regex("/images/placeholder.png"));

        Assert.Empty(failedImageResponses);
    }
}
