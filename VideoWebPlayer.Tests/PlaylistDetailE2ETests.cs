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
        await row.Locator(".playlist-open-button").ClickAsync();
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
        await row.Locator(".playlist-open-button").ClickAsync();
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
        await row.Locator(".playlist-open-button").ClickAsync();
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
        await row.Locator(".playlist-open-button").ClickAsync();
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
        await row.Locator(".playlist-open-button").ClickAsync();
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
        await row.Locator(".playlist-open-button").ClickAsync();
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
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await Page.WaitForTimeoutAsync(1500);

        var renderedCount = await Page.Locator(".playlist-entry-row").CountAsync();
        Assert.True(renderedCount is > 0 and < 25, $"Erwartete eine virtualisierte Teilmenge der 25 Eintraege, aber es wurden {renderedCount} gerendert.");
        await Expect(Page.Locator("#playlist-entries-more-available")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task PlaylistDetail_LoadsNextPage_OnScrollNearEnd()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Infinity-Naechste-Seite");
        await SeedMoviesIntoPlaylistAsync("Infinity-Naechste-Seite", 25);
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await Page.WaitForTimeoutAsync(1500);

        // Page 1 (pageSize 20) only ever contains media ids 1-20; a row referencing an id beyond
        // that can only appear once the component has lazy-loaded page 2 in reaction to scrolling.
        var beyondFirstPageRow = Page.Locator(".playlist-entry-row[data-media-id='21']");
        var scrollContainer = Page.Locator(".playlist-entries-scroll");
        await scrollContainer.HoverAsync();

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
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await Page.WaitForTimeoutAsync(1500);

        await Expect(Page.Locator(".playlist-entry-row")).ToHaveCountAsync(5);
        await Expect(Page.Locator("#playlist-entries-more-available")).ToHaveCountAsync(0);
    }

    /// <summary>
    /// Verifies the appearance of a genuinely accessible entry: a TV show that has been explicitly
    /// unlocked for the current user via the unlocked-media service is expected to be
    /// rendered without the reduced-opacity styling reserved for inaccessible content.
    /// </summary>
    [Fact]
    public async Task PlaylistDetail_DoesNotShowReducedOpacity_WhenEntryIsUnlocked()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Zugriffsstatus-Freigeschaltet");
        var showId = await SeedTvShowIntoPlaylistAsync("Zugriffsstatus-Freigeschaltet", "Freigeschaltete Serie");
        await UnlockMediaForUserAsync(UserAEmail, MediaTypeValues.TVShow, showId);
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await Page.WaitForTimeoutAsync(1500);

        await Expect(Page.Locator($".playlist-entry-row[data-media-id='{showId}']")).Not.ToHaveClassAsync(new Regex("opacity-50"));
    }

    /// <summary>
    /// Verifies the actual graying behavior for entries without a real unlock: a TV show that has
    /// not been explicitly unlocked for the current user is rendered with the reduced-opacity styling,
    /// while a sibling entry that has been unlocked is not.
    /// </summary>
    [Fact]
    public async Task PlaylistDetail_ShowsReducedOpacity_WhenEntryNotAccessible()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Zugriffsstatus-Gemischt");
        var lockedShowId = await SeedTvShowIntoPlaylistAsync("Zugriffsstatus-Gemischt", "Gesperrte Serie");
        var unlockedShowId = await SeedTvShowIntoPlaylistAsync("Zugriffsstatus-Gemischt", "Freigeschaltete Serie");
        await UnlockMediaForUserAsync(UserAEmail, MediaTypeValues.TVShow, unlockedShowId);
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await Page.WaitForTimeoutAsync(1500);

        await Expect(Page.Locator($".playlist-entry-row[data-media-id='{lockedShowId}']")).ToHaveClassAsync(new Regex("opacity-50"));
        await Expect(Page.Locator($".playlist-entry-row[data-media-id='{unlockedShowId}']")).Not.ToHaveClassAsync(new Regex("opacity-50"));
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
        var lockedShowId = await SeedTvShowIntoPlaylistAsync("Entfernen-Trotz-Sperre", "Gesperrte Serie");
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await Page.WaitForTimeoutAsync(1500);

        var lockedRow = Page.Locator($".playlist-entry-row[data-media-id='{lockedShowId}']");
        await Expect(lockedRow).ToHaveClassAsync(new Regex("opacity-50"));
        var removeButton = lockedRow.Locator(".playlist-entry-remove-button");
        await Expect(removeButton).ToBeVisibleAsync();
        await Expect(removeButton).ToBeEnabledAsync();

        await removeButton.ClickAsync();
        await Page.WaitForTimeoutAsync(1000);

        await Expect(Page.Locator($".playlist-entry-row[data-media-id='{lockedShowId}']")).ToHaveCountAsync(0);
    }

    /// <summary>
    /// Verifies the corrected `hasSourceAccess OR isUnlocked` accessibility rule: a TV show the user
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
        var showId = await SeedTvShowIntoPlaylistAsync("Zugriffsstatus-Quellenzugriff", "Serie mit Quellenzugriff");
        await GrantMediaSourceAccessForUserAsync(UserAEmail);
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await Page.WaitForTimeoutAsync(1500);

        await Expect(Page.Locator($".playlist-entry-row[data-media-id='{showId}']")).Not.ToHaveClassAsync(new Regex("opacity-50"));
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
        await row.Locator(".playlist-open-button").ClickAsync();
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
        await row.Locator(".playlist-open-button").ClickAsync();
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
        var showWithoutPosterId = await SeedTvShowIntoPlaylistAsync("Bildspalte-Test", "Serie Ohne Poster");
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await Page.WaitForTimeoutAsync(1500);

        // Movie and TVShow ids are independently auto-incremented, so the media type must be
        // included in the selector to uniquely identify each row.
        var posterImage = Page.Locator($".playlist-entry-row[data-media-type='{MediaTypeValues.Movie}'][data-media-id='{moviePosterId}'] .playlist-entry-image");
        await Expect(posterImage).ToHaveCountAsync(1);
        await Expect(posterImage).ToHaveAttributeAsync("src", new Regex("/api/pictures/"));

        var placeholderImage = Page.Locator($".playlist-entry-row[data-media-type='{MediaTypeValues.TVShow}'][data-media-id='{showWithoutPosterId}'] .playlist-entry-image");
        await Expect(placeholderImage).ToHaveCountAsync(1);
        await Expect(placeholderImage).ToHaveAttributeAsync("src", new Regex("/images/placeholder.png"));

        Assert.Empty(failedImageResponses);
    }
}
