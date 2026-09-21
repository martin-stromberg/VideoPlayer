using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// End-to-end tests with Playwright covering manual sort order on the playlist detail page: switching
/// sort mode and the "An Anfang"/"An Ende" quick actions. Reordering by dragging a tile has its own file,
/// <see cref="PlaylistDragDropReorderE2ETests"/>. Split out of
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
        await row.ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await Page.WaitForTimeoutAsync(1500);

        await Expect(Page.Locator(".playlist-entry-move-start-button")).ToHaveCountAsync(0);

        await Page.ClickAsync(".playlist-sortmode-toggle-button");
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
        await row.ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await Page.WaitForTimeoutAsync(1500);

        await Page.ClickAsync(".playlist-sortmode-toggle-button");
        await Page.WaitForTimeoutAsync(1000);

        await Page.ClickAsync(".playlist-sortmode-toggle-button");
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
        await row.ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await Page.WaitForTimeoutAsync(1500);

        await Page.ClickAsync(".playlist-sortmode-toggle-button");
        await Page.WaitForTimeoutAsync(1000);

        await Page.ClickAsync(".playlist-sortmode-toggle-button");
        await Page.WaitForSelectorAsync("#cancel-sortmode-change-button");
        await Page.ClickAsync("#cancel-sortmode-change-button");
        await Page.WaitForTimeoutAsync(500);

        await Expect(Page.Locator("#playlist-detail-sortmode")).ToHaveTextAsync("Manuell");
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
}
