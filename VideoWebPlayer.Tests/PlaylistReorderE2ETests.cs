using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// End-to-end tests with Playwright covering manual sort order on the playlist detail page: switching
/// sort mode, the "An Anfang"/"An Ende" quick actions, and drag & drop reordering. Split out of
/// <see cref="PlaylistDetailE2ETests"/> (which otherwise mixed general detail-page navigation/CRUD tests
/// with sorting-specific ones) to keep each test file focused on one topic, matching the project's
/// existing <c>PlaylistsE2ETests</c>/<c>PlaylistEntriesE2ETests</c> naming convention.
/// </summary>
[Trait("Category", "E2E")]
public sealed class PlaylistReorderE2ETests : PlaylistsE2ETestBase
{
    /// <summary>
    /// Verifies that switching a playlist's sort mode from ByReleaseDate to Manual (via the sort-mode UI)
    /// activates the manual-mode quick-action controls. Split off from a test that also covered switching
    /// back (see <see cref="E2E_SortModeChange_ManualToByReleaseDate_ShowsWarningModal_AndConfirmingDeactivatesControls"/>),
    /// so each test covers one independent behavior.
    /// </summary>
    [Fact]
    public async Task E2E_SortModeChange_ByReleaseDateToManual_ActivatesManualModeControls()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Sortiermodus-Wechsel-Aktivierung");
        await SeedMoviesIntoPlaylistAsync("Sortiermodus-Wechsel-Aktivierung", 2);
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await Page.WaitForTimeoutAsync(1500);

        await Expect(Page.Locator(".playlist-entry-move-start-button")).ToHaveCountAsync(0);

        await Page.SelectOptionAsync("#playlist-sortmode-change-select", PlaylistSortModeValues.Manual);
        await Page.ClickAsync(".playlist-sortmode-apply-button");
        await Page.WaitForTimeoutAsync(1000);

        await Expect(Page.Locator("#playlist-detail-sortmode")).ToHaveTextAsync("Manuell");
        await Expect(Page.Locator(".playlist-entry-move-start-button").First).ToBeVisibleAsync();
    }

    /// <summary>
    /// Verifies that switching a Manual-mode playlist back to ByReleaseDate shows the data-loss warning
    /// modal, and that confirming it applies the mode change and deactivates the manual-mode controls
    /// again. Split off from a test that also covered activating manual mode in the first place (see
    /// <see cref="E2E_SortModeChange_ByReleaseDateToManual_ActivatesManualModeControls"/>).
    /// </summary>
    [Fact]
    public async Task E2E_SortModeChange_ManualToByReleaseDate_ShowsWarningModal_AndConfirmingDeactivatesControls()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Sortiermodus-Wechsel-Warnung");
        await SeedMoviesIntoPlaylistAsync("Sortiermodus-Wechsel-Warnung", 2);
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await Page.WaitForTimeoutAsync(1500);

        await Page.SelectOptionAsync("#playlist-sortmode-change-select", PlaylistSortModeValues.Manual);
        await Page.ClickAsync(".playlist-sortmode-apply-button");
        await Page.WaitForTimeoutAsync(1000);

        await Page.SelectOptionAsync("#playlist-sortmode-change-select", PlaylistSortModeValues.ByReleaseDate);
        await Page.ClickAsync(".playlist-sortmode-apply-button");
        await Page.WaitForSelectorAsync("#confirm-sortmode-change-button");

        await Page.ClickAsync("#confirm-sortmode-change-button");
        await Page.WaitForTimeoutAsync(1000);

        await Expect(Page.Locator("#playlist-detail-sortmode")).ToHaveTextAsync("Nach Erscheinungsdatum");
        await Expect(Page.Locator(".playlist-entry-move-start-button")).ToHaveCountAsync(0);
    }

    /// <summary>
    /// Verifies that cancelling the data-loss warning modal (shown when switching from Manual to
    /// ByReleaseDate) leaves the playlist in Manual mode.
    /// </summary>
    [Fact]
    public async Task E2E_SortModeChange_Manual_ToByReleaseDate_Cancel_KeepsManualMode()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync("Sortiermodus-Abbrechen");
        await SeedMoviesIntoPlaylistAsync("Sortiermodus-Abbrechen", 2);
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await Page.WaitForTimeoutAsync(1500);

        await Page.SelectOptionAsync("#playlist-sortmode-change-select", PlaylistSortModeValues.Manual);
        await Page.ClickAsync(".playlist-sortmode-apply-button");
        await Page.WaitForTimeoutAsync(1000);

        await Page.SelectOptionAsync("#playlist-sortmode-change-select", PlaylistSortModeValues.ByReleaseDate);
        await Page.ClickAsync(".playlist-sortmode-apply-button");
        await Page.WaitForSelectorAsync("#cancel-sortmode-change-button");
        await Page.ClickAsync("#cancel-sortmode-change-button");
        await Page.WaitForTimeoutAsync(500);

        await Expect(Page.Locator("#playlist-detail-sortmode")).ToHaveTextAsync("Manuell");
    }

    /// <summary>
    /// Sets up a Manual-mode playlist with 3 movies for the quick-action and drag & drop tests below, and
    /// returns the row locator plus the entries' initial media-id order (front-to-back, i.e. ascending
    /// SortOrder).
    /// </summary>
    /// <param name="playlistName">The name to create the playlist with.</param>
    /// <returns>The entry row locator and the initial front-to-back media-id order.</returns>
    private async Task<(ILocator RowLocator, string[] InitialOrder)> SetupManualPlaylistWithThreeEntriesAsync(string playlistName)
    {
        await LoginAsync(UserAEmail);
        var row = await CreatePlaylistViaUiAsync(playlistName);
        await SeedMoviesIntoPlaylistAsync(playlistName, 3);
        await row.Locator(".playlist-open-button").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await Page.WaitForTimeoutAsync(1500);

        await Page.SelectOptionAsync("#playlist-sortmode-change-select", PlaylistSortModeValues.Manual);
        await Page.ClickAsync(".playlist-sortmode-apply-button");
        await Page.WaitForTimeoutAsync(1000);

        var rowLocator = Page.Locator(".playlist-entries-scroll .playlist-entry-row");
        var initialOrder = await rowLocator.EvaluateAllAsync<string[]>("els => els.map(e => e.getAttribute('data-media-id'))");
        Assert.Equal(3, initialOrder.Length);

        return (rowLocator, initialOrder);
    }

    /// <summary>
    /// Verifies the "An Ende" quick-action button: clicking it on the first entry makes it the last row.
    /// Split off from a test that also chained the "An Anfang" button afterwards (see
    /// <see cref="E2E_QuickActionButton_MoveToBeginning_MovesEntryToFirstPosition"/>), so each test covers
    /// one independent action.
    /// </summary>
    [Fact]
    public async Task E2E_QuickActionButton_MoveToEnd_MovesEntryToLastPosition()
    {
        if (SkipBrowser)
            return;

        var (rowLocator, initialOrder) = await SetupManualPlaylistWithThreeEntriesAsync("Schnellaktion-Ans-Ende");
        var firstMediaId = initialOrder[0];

        await Page.Locator($".playlist-entry-row[data-media-id='{firstMediaId}'] .playlist-entry-move-end-button").ClickAsync();
        await Page.WaitForTimeoutAsync(1000);

        var afterMoveToEnd = await rowLocator.EvaluateAllAsync<string[]>("els => els.map(e => e.getAttribute('data-media-id'))");
        Assert.Equal(firstMediaId, afterMoveToEnd[^1]);
    }

    /// <summary>
    /// Verifies the "An Anfang" quick-action button: clicking it on the last entry makes it the first row.
    /// Split off from a test that also chained the "An Ende" button beforehand (see
    /// <see cref="E2E_QuickActionButton_MoveToEnd_MovesEntryToLastPosition"/>), so each test covers one
    /// independent action.
    /// </summary>
    [Fact]
    public async Task E2E_QuickActionButton_MoveToBeginning_MovesEntryToFirstPosition()
    {
        if (SkipBrowser)
            return;

        var (rowLocator, initialOrder) = await SetupManualPlaylistWithThreeEntriesAsync("Schnellaktion-An-Anfang");
        var lastMediaId = initialOrder[^1];

        await Page.Locator($".playlist-entry-row[data-media-id='{lastMediaId}'] .playlist-entry-move-start-button").ClickAsync();
        await Page.WaitForTimeoutAsync(1000);

        var afterMoveToBeginning = await rowLocator.EvaluateAllAsync<string[]>("els => els.map(e => e.getAttribute('data-media-id'))");
        Assert.Equal(lastMediaId, afterMoveToBeginning[0]);
    }

    /// <summary>
    /// Primary functional proof (see plan.md, "E2E-Tests (primärer Funktionsnachweis)") for drag & drop
    /// reordering: dragging the first entry onto the third entry - exercising the actual
    /// <c>dragstart</c>/<c>dragover</c>/<c>drop</c> events wired up on <c>.playlist-entry-row</c> in
    /// <c>PlaylistEntriesList.razor</c> - changes the rendered order, and a full page reload afterwards
    /// proves the new order was actually persisted server-side (via <c>OnEntryDropAsync</c> calling
    /// <c>ReorderPlaylistEntryAsync</c>), not just held in transient client-side state.
    /// </summary>
    /// <remarks>
    /// Uses JS-dispatched native <c>DragEvent</c>s (via <see cref="IPage.EvalOnSelectorAsync"/>) rather
    /// than <c>Locator.DragToAsync</c> or raw <see cref="IPage.Mouse"/> moves: both of those simulate the
    /// drag via physical mouse input, which headless Chromium does not reliably translate into a native
    /// HTML5 drag gesture (confirmed by hand - both left the row order completely unchanged, with no
    /// dragstart/drop round-trip reaching the server at all). Dispatching real <c>DragEvent</c> objects
    /// with a shared <c>DataTransfer</c> directly triggers the same <c>dragstart</c>/<c>dragover</c>/
    /// <c>drop</c>/<c>dragend</c> sequence the browser would fire during a real drag, which
    /// <c>PlaylistEntriesList.razor</c>'s <c>@ondragstart</c>/<c>@ondragover:preventDefault</c>/
    /// <c>@ondrop</c>/<c>@ondragend</c> bindings handle identically either way.
    /// </remarks>
    [Fact]
    public async Task E2E_DragDropReorder_ManualMode_PersistsSortOrder()
    {
        if (SkipBrowser)
            return;

        var (rowLocator, initialOrder) = await SetupManualPlaylistWithThreeEntriesAsync("DragDrop-Umordnung");
        var sourceMediaId = initialOrder[0];
        var targetMediaId = initialOrder[2];

        await Page.EvalOnSelectorAsync($".playlist-entry-row[data-media-id='{sourceMediaId}']",
            @"(source, targetId) => {
                const target = document.querySelector(`.playlist-entry-row[data-media-id='${targetId}']`);
                const dt = new DataTransfer();
                source.dispatchEvent(new DragEvent('dragstart', { bubbles: true, cancelable: true, dataTransfer: dt }));
                target.dispatchEvent(new DragEvent('dragover', { bubbles: true, cancelable: true, dataTransfer: dt }));
                target.dispatchEvent(new DragEvent('drop', { bubbles: true, cancelable: true, dataTransfer: dt }));
                source.dispatchEvent(new DragEvent('dragend', { bubbles: true, cancelable: true, dataTransfer: dt }));
            }",
            targetMediaId);
        await Page.WaitForTimeoutAsync(1000);

        var afterDrop = await rowLocator.EvaluateAllAsync<string[]>("els => els.map(e => e.getAttribute('data-media-id'))");
        Assert.NotEqual(initialOrder, afterDrop);
        Assert.NotEqual(sourceMediaId, afterDrop[0]);

        await Page.ReloadAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await Page.WaitForTimeoutAsync(1500);

        var afterReload = await rowLocator.EvaluateAllAsync<string[]>("els => els.map(e => e.getAttribute('data-media-id'))");
        Assert.Equal(afterDrop, afterReload);
    }
}
