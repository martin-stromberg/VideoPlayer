using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
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
    /// Verifies the baseline appearance of accessible entries: since license/access checking is not
    /// yet implemented, <c>DtoPlaylistEntry.IsAccessible</c> is currently always <c>true</c>, so no
    /// entry is expected to carry the reduced-opacity styling reserved for inaccessible content.
    /// </summary>
    [Fact]
    public async Task PlaylistDetail_DoesNotShowReducedOpacity_WhenAllEntriesAccessible()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Infinity-Zugriffsstatus");
        await SeedMoviesIntoPlaylistAsync("Infinity-Zugriffsstatus", 3);
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await Page.WaitForTimeoutAsync(1500);

        await Expect(Page.Locator(".playlist-entry-row.opacity-50")).ToHaveCountAsync(0);
    }
}
