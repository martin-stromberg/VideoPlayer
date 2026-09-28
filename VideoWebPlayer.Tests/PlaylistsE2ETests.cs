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
        await row.ClickAsync();
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

    /// <summary>
    /// Regressionstest fuer einen realen Anwenderbefund: ein Klick auf die Playlist-Kachel in der
    /// Uebersicht oeffnete trotz einer (fehlerhaft grünen) vorherigen automatisierten Pruefung nicht die
    /// Detailansicht. Jener Test klickte nur auf eine interne Implementierungsdetail-Schaltflaeche, die
    /// zwar selbst funktionierte, aber nicht zuverlaessig die komplette sichtbare Kachel abdeckte. Dieser
    /// Test klickt stattdessen gezielt auf die tatsaechlich sichtbaren Elemente, auf die ein Anwender mit
    /// Maus/Finger klickt/tippt (Titeltext, Cover-Grafik, Metazeile mit Sortiersymbol/Datumsangaben) und
    /// haette den Bug erkannt, waere er erneut aufgetreten - jedes einzelne Element muss zur
    /// Detailansicht navigieren, nicht nur ein unsichtbares Overlay-Element.
    /// </summary>
    [Fact]
    public async Task Click_On_Any_Visible_Part_Of_The_Tile_Opens_Detail()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);

        var rowForTitle = await CreatePlaylistViaUiAsync("Kachel-Klick-Titel", "Beschreibung");
        await rowForTitle.Locator(".media-title-text").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await Expect(Page.Locator("#playlist-detail-name")).ToHaveTextAsync("Kachel-Klick-Titel");

        var rowForCover = await CreatePlaylistViaUiAsync("Kachel-Klick-Cover", "Beschreibung");
        await rowForCover.Locator(".playlist-card-cover").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await Expect(Page.Locator("#playlist-detail-name")).ToHaveTextAsync("Kachel-Klick-Cover");

        // Der Verlaufsbereich am unteren Rand (Titel-Overlay) ist ebenfalls Teil des Links.
        var rowForOverlay = await CreatePlaylistViaUiAsync("Kachel-Klick-Overlay", "Beschreibung");
        await rowForOverlay.Locator(".media-card-overlay").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await Expect(Page.Locator("#playlist-detail-name")).ToHaveTextAsync("Kachel-Klick-Overlay");
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
        await CreatePlaylistViaUiAsync("Nur für Benutzer A");

        await LoginAsync(UserBEmail);
        await Page.GotoAsync($"{ServerUrl}/playlists");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(1500);

        await Expect(Page.Locator(".playlist-row[data-playlist-name='Nur für Benutzer A']")).ToHaveCountAsync(0);
    }
}
