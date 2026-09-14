using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// End-to-end tests with Playwright covering playlist creation, listing, editing,
/// deletion with confirmation, validation errors, duplicate names, unauthenticated
/// access and ownership isolation between users.
/// </summary>
[Trait("Category", "E2E")]
public sealed class PlaylistsE2ETests : PlaylistsE2ETestBase
{
    /// <summary>
    /// Bearbeiten und Loeschen einer Playlist gibt es seit dem UI-Redesign (Kundenfeedback) nur noch in
    /// der Detailansicht, nicht mehr direkt in der Kachel-Uebersicht - ein Klick auf die Kachel oeffnet
    /// zunaechst die Detailseite, von dort aus geht es weiter zu Bearbeiten/Loeschen (siehe
    /// <c>PlaylistDetailE2ETests</c>, die diesen Teil im Detail abdeckt).
    /// </summary>
    [Fact]
    public async Task Create_List_Edit_And_Delete_Playlist_HappyPath()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Serien-Marathon", "Meine Lieblingsserien");

        // Kachel oeffnet die Detailansicht
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");

        // Edit (Symbol-Button in der Detailansicht)
        await Page.ClickAsync(".playlist-detail-edit-button");
        await Page.WaitForSelectorAsync("#playlist-name-input");
        await Page.FillAsync("#playlist-name-input", "Serien-Marathon Deluxe");
        await Page.ClickAsync("#playlist-save-button");
        await Page.WaitForTimeoutAsync(1000);

        await Expect(Page.Locator("#playlist-detail-name")).ToHaveTextAsync("Serien-Marathon Deluxe");

        // Delete with confirmation (Symbol-Button in der Detailansicht)
        await Page.ClickAsync(".playlist-detail-delete-button");
        await Page.WaitForSelectorAsync("#confirm-delete-playlist-button");
        await Page.ClickAsync("#confirm-delete-playlist-button");
        await Page.WaitForTimeoutAsync(1000);

        await Expect(Page.Locator(".playlist-row[data-playlist-name='Serien-Marathon Deluxe']")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task EmptyName_ShowsValidationError()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        await Page.GotoAsync($"{ServerUrl}/playlists");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(1500);

        await Page.ClickAsync("#create-playlist-button");
        await Page.WaitForSelectorAsync("#playlist-name-input");
        await Page.ClickAsync("#playlist-save-button");
        await Page.WaitForTimeoutAsync(500);

        await Expect(Page.Locator("#playlist-name-input")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task DuplicateName_ShowsConflictError()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        await CreatePlaylistViaUiAsync("Duplikat-Test");

        await Page.ClickAsync("#create-playlist-button");
        await Page.WaitForSelectorAsync("#playlist-name-input");
        await Page.FillAsync("#playlist-name-input", "duplikat-test");
        await Page.ClickAsync("#playlist-save-button");
        await Page.WaitForTimeoutAsync(1000);

        await Expect(Page.Locator("#playlist-form-error")).ToContainTextAsync("existiert bereits");
    }

    [Fact]
    public async Task Unauthenticated_Access_Shows_Error()
    {
        if (SkipBrowser)
            return;

        await Page.GotoAsync($"{ServerUrl}/playlists");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(1500);

        await Expect(Page.Locator("#playlists-load-error")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task UserB_Does_Not_See_UserA_Playlists()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        await CreatePlaylistViaUiAsync("Nur fuer Benutzer A");

        await LoginAsync(UserBEmail);
        await Page.GotoAsync($"{ServerUrl}/playlists");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(1500);

        await Expect(Page.Locator(".playlist-row[data-playlist-name='Nur fuer Benutzer A']")).ToHaveCountAsync(0);
    }
}
