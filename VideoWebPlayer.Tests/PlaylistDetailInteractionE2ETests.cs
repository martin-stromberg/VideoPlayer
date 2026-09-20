using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Real-browser tests of the reworked playlist detail page (Kundenrückmeldung zur Detailansicht): selecting a
/// title (information in the header, keyboard, clearing), the separated content areas with the mode toggle, the
/// cover panel (upload, generated preview, "Anwenden", removal, validation errors), the publish button symbols
/// and the strictly read-only view of a foreign public playlist.
/// </summary>
[Trait("Category", "E2E")]
public sealed class PlaylistDetailInteractionE2ETests : PlaylistsE2ETestBase
{
    private async Task<long> OpenNewPlaylistAsync(string name, string? description = null)
    {
        await LoginAsync(UserAEmail);
        await GrantMediaSourceAccessForUserAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync(name, description);
        await row.ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        var match = Regex.Match(Page.Url, @"/playlists/(\d+)");
        return long.Parse(match.Groups[1].Value);
    }

    private async Task ReloadDetailAsync(long playlistId)
    {
        await Page.GotoAsync($"{ServerUrl}/playlists/{playlistId}");
        await Page.WaitForSelectorAsync("#playlist-detail-header");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(500);
    }

    // ----- Titelauswahl (D8) ---------------------------------------------------------------------------------

    [Fact]
    public async Task SelectingATitle_ShowsItsInformationInTheHeader_MarksTheTile_AndCanBeCleared()
    {
        if (SkipBrowser)
            return;

        var playlistId = await OpenNewPlaylistAsync("Titelauswahl", "Playlist-Beschreibung");
        var movieId = await SeedDetailedMovieIntoPlaylistAsync("Titelauswahl", "Der lange Film", TestImages.Png(300, 450));
        await ReloadDetailAsync(playlistId);
        var tile = Page.Locator($".playlist-entry-row[data-media-type='Movie'][data-media-id='{movieId}']");
        await Expect(tile).ToHaveAttributeAsync("aria-selected", "false");
        await Expect(Page.Locator("#playlist-detail-name")).ToHaveTextAsync("Titelauswahl");

        await tile.ClickAsync();

        await Expect(Page.Locator("#playlist-detail-entry-title")).ToHaveTextAsync("Der lange Film");
        await Expect(Page.Locator("#playlist-detail-entry-type")).ToHaveTextAsync("Film");
        await Expect(Page.Locator("#playlist-detail-entry-year")).ToHaveTextAsync("2018");
        await Expect(Page.Locator("#playlist-detail-entry-plot")).ToContainTextAsync("Handlung von Der lange Film");
        await Expect(Page.Locator("#playlist-detail-entry-poster")).ToBeVisibleAsync();
        await Expect(Page.Locator("#playlist-detail-play-entry-button")).ToBeVisibleAsync();
        await Expect(Page.Locator("#playlist-detail-remove-entry-button")).ToBeVisibleAsync();
        await Expect(Page.Locator("#playlist-detail-name")).ToHaveCountAsync(0);
        await Expect(tile).ToHaveAttributeAsync("aria-selected", "true");
        await Expect(tile).ToHaveClassAsync(new Regex("selected"));

        // Clearing via the back arrow brings the playlist information back.
        await Page.ClickAsync("#playlist-detail-back-button");
        await Expect(Page.Locator("#playlist-detail-name")).ToHaveTextAsync("Titelauswahl");
        await Expect(tile).ToHaveAttributeAsync("aria-selected", "false");
        await Expect(Page.Locator("#playlist-detail-play-entry-button")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task SelectingATitle_WorksWithTheKeyboard_EnterSelects_EscapeClears()
    {
        if (SkipBrowser)
            return;

        var playlistId = await OpenNewPlaylistAsync("Tastatur");
        var movieId = await SeedDetailedMovieIntoPlaylistAsync("Tastatur", "Tastaturfilm", TestImages.Png(300, 450));
        await ReloadDetailAsync(playlistId);
        var tile = Page.Locator($".playlist-entry-row[data-media-id='{movieId}']");

        await tile.FocusAsync();
        await Page.Keyboard.PressAsync("Enter");
        await Expect(tile).ToHaveAttributeAsync("aria-selected", "true");
        await Expect(Page.Locator("#playlist-detail-entry-title")).ToHaveTextAsync("Tastaturfilm");

        await Page.Keyboard.PressAsync("Escape");
        await Expect(tile).ToHaveAttributeAsync("aria-selected", "false");
        await Expect(Page.Locator("#playlist-detail-name")).ToBeVisibleAsync();

        await tile.FocusAsync();
        await Page.Keyboard.PressAsync("Space");
        await Expect(tile).ToHaveAttributeAsync("aria-selected", "true");
    }

    [Fact]
    public async Task Selection_DoesNotChangeTheUrl_AndTilesHaveNoButtonsExceptTheReorderControls()
    {
        if (SkipBrowser)
            return;

        var playlistId = await OpenNewPlaylistAsync("Nur Auswahl");
        var movieId = await SeedDetailedMovieIntoPlaylistAsync("Nur Auswahl", "Auswahlfilm", TestImages.Png(300, 450));
        await ReloadDetailAsync(playlistId);
        var urlBefore = Page.Url;

        await Page.ClickAsync($".playlist-entry-row[data-media-id='{movieId}']");

        await Expect(Page.Locator("#playlist-detail-selected-entry")).ToBeVisibleAsync();
        Assert.Equal(urlBefore, Page.Url);
        Assert.DoesNotContain("entryId", Page.Url);
        // Sorted by release date (no manual order): the tile carries no button at all.
        await Expect(Page.Locator(".playlist-entry-row button")).ToHaveCountAsync(0);
    }

    // ----- Moduswechsel (D9) ---------------------------------------------------------------------------------

    [Fact]
    public async Task EmptyPlaylist_StartsInAddMode_AddingStaysThere_ListShowsTheTitles_RemovingTheLastOneReturnsToAdding()
    {
        if (SkipBrowser)
            return;

        var first = await SeedMovieAsync("Erster Titel");
        var second = await SeedMovieAsync("Zweiter Titel");
        await OpenNewPlaylistAsync("Moduswechsel");

        // Empty playlist: adding is offered at once, the list is not shown.
        await Expect(Page.Locator("#playlist-add-area")).ToBeVisibleAsync();
        await Expect(Page.Locator("#playlist-mode-add-button")).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(Page.Locator(".playlist-entries-list")).ToHaveCountAsync(0);

        // Several titles can be added in a row without leaving the add area.
        await SelectSearchResultAsync("Erster Titel", "Movie", first);
        await Expect(Page.Locator("#playlist-add-area")).ToBeVisibleAsync();
        await SelectSearchResultAsync("Zweiter Titel", "Movie", second);
        await Expect(Page.Locator("#playlist-add-area")).ToBeVisibleAsync();
        await Expect(Page.Locator(".playlist-entry-row")).ToHaveCountAsync(0);

        // Switching shows the titles and hides the search.
        await Page.ClickAsync("#playlist-mode-entries-button");
        await Expect(Page.Locator("#playlist-mode-entries-button")).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(Page.Locator(".playlist-entry-row")).ToHaveCountAsync(2);
        await Expect(Page.Locator(".media-search-input")).ToHaveCountAsync(0);

        // Remove both titles via the header: after the last one the add area is shown again.
        await SelectEntryAsync("Movie", first);
        await Page.ClickAsync("#playlist-detail-remove-entry-button");
        await Expect(Page.Locator(".playlist-entry-row")).ToHaveCountAsync(1);
        await Expect(Page.Locator("#playlist-add-area")).ToHaveCountAsync(0);
        await SelectEntryAsync("Movie", second);
        await Page.ClickAsync("#playlist-detail-remove-entry-button");
        await Expect(Page.Locator("#playlist-add-area")).ToBeVisibleAsync();
        await Expect(Page.Locator("#playlist-mode-add-button")).ToHaveAttributeAsync("aria-pressed", "true");
    }

    [Fact]
    public async Task NonEmptyPlaylist_StartsInListMode_TogglingSwitchesAreasAndClearsTheSelection()
    {
        if (SkipBrowser)
            return;

        var playlistId = await OpenNewPlaylistAsync("Listenvorrang");
        var movieId = await SeedDetailedMovieIntoPlaylistAsync("Listenvorrang", "Vorhandener Film", TestImages.Png(300, 450));
        await ReloadDetailAsync(playlistId);

        await Expect(Page.Locator("#playlist-mode-entries-button")).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(Page.Locator(".playlist-entry-row")).ToHaveCountAsync(1);
        await Expect(Page.Locator(".media-search-input")).ToHaveCountAsync(0);

        await Page.ClickAsync($".playlist-entry-row[data-media-id='{movieId}']");
        await Expect(Page.Locator("#playlist-detail-selected-entry")).ToBeVisibleAsync();

        await Page.ClickAsync("#playlist-mode-add-button");
        await Expect(Page.Locator(".media-search-input")).ToBeVisibleAsync();
        await Expect(Page.Locator(".playlist-entry-row")).ToHaveCountAsync(0);
        // Leaving the list clears the selection: the header shows the playlist again.
        await Expect(Page.Locator("#playlist-detail-name")).ToBeVisibleAsync();
    }

    // ----- Bild-Panel (D3) -----------------------------------------------------------------------------------

    [Fact]
    public async Task CoverPanel_UploadFile_PreviewThenHochladen_SavesAnUploadedCover()
    {
        if (SkipBrowser)
            return;

        await OpenNewPlaylistAsync("Bild Upload");
        Assert.Null((await GetPlaylistCoverStateAsync("Bild Upload")).CoverPictureId);
        await Expect(Page.Locator("#playlist-detail-upload-cover-button")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#playlist-detail-regenerate-cover-button")).ToHaveCountAsync(0);

        await Page.ClickAsync("#playlist-detail-cover-button");
        await Expect(Page.Locator("#playlist-cover-panel")).ToBeVisibleAsync();
        await Page.SetInputFilesAsync("#playlist-cover-upload-input", new FilePayload { Name = "bild.png", MimeType = "image/png", Buffer = TestImages.Png(1200, 400) });

        // The chosen file shows as a preview and is NOT saved yet.
        await Expect(Page.Locator("#playlist-cover-preview")).ToBeVisibleAsync();
        await Expect(Page.Locator("#playlist-cover-apply-button")).ToHaveTextAsync("Hochladen");
        Assert.Null((await GetPlaylistCoverStateAsync("Bild Upload")).CoverPictureId);

        await Page.ClickAsync("#playlist-cover-apply-button");

        await Expect(Page.Locator("#playlist-cover-panel")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#playlist-detail-cover-image")).ToBeVisibleAsync();
        var state = await GetPlaylistCoverStateAsync("Bild Upload");
        Assert.NotNull(state.CoverPictureId);
        Assert.True(state.IsUserUploaded);
    }

    [Fact]
    public async Task CoverPanel_Generate_PreviewIsNotSavedUntilAnwenden()
    {
        if (SkipBrowser)
            return;

        await OpenNewPlaylistAsync("Bild Erzeugen");
        await SeedMovieWithPosterIntoPlaylistAsync("Bild Erzeugen", "Film mit Poster");

        await Page.ClickAsync("#playlist-detail-cover-button");
        await Page.ClickAsync("#playlist-cover-generate-button");

        await Expect(Page.Locator("#playlist-cover-preview")).ToBeVisibleAsync();
        await Expect(Page.Locator("#playlist-cover-apply-button")).ToHaveTextAsync("Anwenden");
        await Expect(Page.Locator("#playlist-cover-preview-caption")).ToContainTextAsync("noch nicht gespeichert");
        // The preview is only shown - nothing is stored.
        Assert.Null((await GetPlaylistCoverStateAsync("Bild Erzeugen")).CoverPictureId);

        // Closing without applying keeps the playlist unchanged.
        await Page.ClickAsync("#playlist-cover-cancel-button");
        await Expect(Page.Locator("#playlist-cover-panel")).ToHaveCountAsync(0);
        Assert.Null((await GetPlaylistCoverStateAsync("Bild Erzeugen")).CoverPictureId);

        // Applying stores the generated image (not flagged as uploaded).
        await Page.ClickAsync("#playlist-detail-cover-button");
        await Page.ClickAsync("#playlist-cover-generate-button");
        await Expect(Page.Locator("#playlist-cover-preview")).ToBeVisibleAsync();
        await Page.ClickAsync("#playlist-cover-apply-button");

        await Expect(Page.Locator("#playlist-cover-panel")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#playlist-detail-cover-image")).ToBeVisibleAsync();
        var state = await GetPlaylistCoverStateAsync("Bild Erzeugen");
        Assert.NotNull(state.CoverPictureId);
        Assert.False(state.IsUserUploaded);
    }

    [Fact]
    public async Task CoverPanel_Generate_WithoutPosterImages_ShowsAMessageInThePanel()
    {
        if (SkipBrowser)
            return;

        await OpenNewPlaylistAsync("Bild Leer");

        await Page.ClickAsync("#playlist-detail-cover-button");
        await Page.ClickAsync("#playlist-cover-generate-button");

        await Expect(Page.Locator("#playlist-cover-error")).ToContainTextAsync("Keine Bilder verfügbar");
        await Expect(Page.Locator("#playlist-cover-preview")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#playlist-cover-apply-button")).ToBeDisabledAsync();
    }

    [Fact]
    public async Task CoverPanel_ReplacingAnUploadedCover_WarnsFirst_AndApplyReplacesIt()
    {
        if (SkipBrowser)
            return;

        await OpenNewPlaylistAsync("Bild Ersetzen");
        await SeedMovieWithPosterIntoPlaylistAsync("Bild Ersetzen", "Film mit Poster");
        var playlistId = await SetPlaylistCoverAsync("Bild Ersetzen", TestImages.Png(900, 300), isUserUploaded: true);
        await ReloadDetailAsync(playlistId);

        await Page.ClickAsync("#playlist-detail-cover-button");
        await Expect(Page.Locator("#playlist-cover-current")).ToBeVisibleAsync();
        await Page.ClickAsync("#playlist-cover-generate-button");

        // The panel announces that the uploaded image will be replaced BEFORE anything happens.
        await Expect(Page.Locator("#playlist-cover-replace-warning")).ToContainTextAsync("hochgeladene Bild wird ersetzt");
        Assert.True((await GetPlaylistCoverStateAsync("Bild Ersetzen")).IsUserUploaded);

        await Page.ClickAsync("#playlist-cover-apply-button");

        await Expect(Page.Locator("#playlist-cover-panel")).ToHaveCountAsync(0);
        Assert.False((await GetPlaylistCoverStateAsync("Bild Ersetzen")).IsUserUploaded);
    }

    [Fact]
    public async Task CoverPanel_RemoveUploadedCover_AsksFirst_GeneratedCoverIsRemovedDirectly()
    {
        if (SkipBrowser)
            return;

        await OpenNewPlaylistAsync("Bild Entfernen");
        var playlistId = await SetPlaylistCoverAsync("Bild Entfernen", TestImages.Png(900, 300), isUserUploaded: true);
        await ReloadDetailAsync(playlistId);

        await Page.ClickAsync("#playlist-detail-cover-button");
        await Page.ClickAsync("#playlist-cover-remove-button");
        await Expect(Page.Locator("#confirm-remove-cover-button")).ToBeVisibleAsync();
        Assert.NotNull((await GetPlaylistCoverStateAsync("Bild Entfernen")).CoverPictureId);

        await Page.ClickAsync("#cancel-remove-cover-button");
        await Expect(Page.Locator("#confirm-remove-cover-button")).ToHaveCountAsync(0);
        Assert.NotNull((await GetPlaylistCoverStateAsync("Bild Entfernen")).CoverPictureId);

        await Page.ClickAsync("#playlist-cover-remove-button");
        await Page.ClickAsync("#confirm-remove-cover-button");
        await Expect(Page.Locator("#playlist-cover-panel")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#playlist-detail-cover-image")).ToHaveCountAsync(0);
        Assert.Null((await GetPlaylistCoverStateAsync("Bild Entfernen")).CoverPictureId);

        // A merely generated image is removed without a question.
        await SetPlaylistCoverAsync("Bild Entfernen", TestImages.Png(900, 300), isUserUploaded: false);
        await ReloadDetailAsync(playlistId);
        await Page.ClickAsync("#playlist-detail-cover-button");
        await Page.ClickAsync("#playlist-cover-remove-button");
        await Expect(Page.Locator("#playlist-cover-panel")).ToHaveCountAsync(0);
        Assert.Null((await GetPlaylistCoverStateAsync("Bild Entfernen")).CoverPictureId);
    }

    [Fact]
    public async Task CoverPanel_InvalidFile_ShowsTheServerValidationMessageInThePanel()
    {
        if (SkipBrowser)
            return;

        await OpenNewPlaylistAsync("Bild Ungültig");

        await Page.ClickAsync("#playlist-detail-cover-button");
        // Claims to be a PNG but is not an image: the client-side check passes, the server validation rejects it.
        await Page.SetInputFilesAsync("#playlist-cover-upload-input", new FilePayload { Name = "kaputt.png", MimeType = "image/png", Buffer = System.Text.Encoding.UTF8.GetBytes("das ist kein bild") });
        await Expect(Page.Locator("#playlist-cover-apply-button")).ToBeEnabledAsync();
        await Page.ClickAsync("#playlist-cover-apply-button");

        await Expect(Page.Locator("#playlist-cover-error")).ToContainTextAsync("Fehler beim Hochladen");
        await Expect(Page.Locator("#playlist-cover-panel")).ToBeVisibleAsync();
        Assert.Null((await GetPlaylistCoverStateAsync("Bild Ungültig")).CoverPictureId);
    }

    // ----- Veröffentlichen-Button (D10) ------------------------------------------------------------------------

    [Fact]
    public async Task PublishButton_AdminOwner_ShowsDifferentSymbolsForPrivateAndPublic()
    {
        if (SkipBrowser)
            return;

        await MakeUserAdminAsync(UserAEmail);
        await OpenNewPlaylistAsync("Veröffentlichen");
        var button = Page.Locator("#playlist-detail-toggle-public-button");

        await Expect(button).ToHaveAttributeAsync("aria-pressed", "false");
        await Expect(button).ToHaveAttributeAsync("title", new Regex("^Privat"));
        await Expect(button.Locator("svg.playlist-private-icon")).ToHaveCountAsync(1);
        var privateSymbol = await button.Locator("svg").InnerHTMLAsync();

        await button.ClickAsync();

        await Expect(button).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(button).ToHaveAttributeAsync("title", new Regex("^Öffentlich"));
        await Expect(button.Locator("svg.playlist-public-icon")).ToHaveCountAsync(1);
        Assert.NotEqual(privateSymbol, await button.Locator("svg").InnerHTMLAsync());
        Assert.Equal(await button.GetAttributeAsync("title"), await button.GetAttributeAsync("aria-label"));

        await button.ClickAsync();
        await Expect(button).ToHaveAttributeAsync("aria-pressed", "false");
        await Expect(button.Locator("svg.playlist-private-icon")).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task PublishButton_RegularUser_IsNotRendered()
    {
        if (SkipBrowser)
            return;

        await OpenNewPlaylistAsync("Kein Veröffentlichen");

        await Expect(Page.Locator("#playlist-detail-toggle-public-button")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#playlist-detail-cover-button")).ToBeVisibleAsync();
    }

    // ----- Nur-Lese-Ansicht (Regression Schritt 11) ------------------------------------------------------------

    [Fact]
    public async Task ForeignPublicPlaylist_Viewer_SeesNoEditingElements_ButCanSelectAndPlay()
    {
        if (SkipBrowser)
            return;

        await MakeUserAdminAsync(UserBEmail);
        var playlistId = await OpenNewPlaylistAsync("Fremd öffentlich", "Für alle sichtbar");
        var movieId = await SeedDetailedMovieIntoPlaylistAsync("Fremd öffentlich", "Öffentlicher Film", TestImages.Png(300, 450));
        await MakePlaylistPublicAsync("Fremd öffentlich");
        await GrantMediaSourceAccessForUserAsync(UserBEmail);

        await Page.Context.ClearCookiesAsync();
        await LoginAsync(UserBEmail);
        await ReloadDetailAsync(playlistId);

        // Playlist view: nothing editable, not even for an administrator who is not the owner.
        await Expect(Page.Locator("#playlist-detail-name")).ToHaveTextAsync("Fremd öffentlich");
        foreach (var selector in new[]
        {
            ".metadata-action-bar", "#playlist-detail-cover-button", ".playlist-detail-edit-button", ".playlist-detail-delete-button",
            "#playlist-detail-toggle-public-button", ".playlist-sortmode-toggle-button", "#playlist-detail-edit-genres-button",
            "#playlist-content-mode-group", "#playlist-mode-add-button", ".media-search-input"
        })
        {
            await Expect(Page.Locator(selector)).ToHaveCountAsync(0);
        }

        // Selecting works and offers playing, but no removal.
        await Page.ClickAsync($".playlist-entry-row[data-media-id='{movieId}']");
        await Expect(Page.Locator("#playlist-detail-entry-title")).ToHaveTextAsync("Öffentlicher Film");
        await Expect(Page.Locator("#playlist-detail-remove-entry-button")).ToHaveCountAsync(0);
        await Expect(Page.Locator(".metadata-action-bar")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#playlist-detail-play-entry-button")).ToBeVisibleAsync();

        await Page.ClickAsync("#playlist-detail-play-entry-button");
        await Page.WaitForSelectorAsync("#video-player-element");
        await Expect(Page.Locator("#playlist-playback-badge")).ToContainTextAsync("Fremd öffentlich: 1/1");
    }
}
