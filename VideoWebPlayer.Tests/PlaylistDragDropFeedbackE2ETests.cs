using Microsoft.Playwright;
using System.Text.RegularExpressions;
using static Microsoft.Playwright.Assertions;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// End-to-end tests with Playwright for everything that can go wrong while reordering playlist entries by
/// dragging: letting go next to a tile instead of on it, letting go far outside the list, cancelling with
/// Escape, and a reorder the server refuses. Split out of <see cref="PlaylistDragDropReorderE2ETests"/>
/// (which covers the successful gesture) so each file covers one topic.
/// </summary>
/// <remarks>
/// The common thread of this file: a drag must never end without any consequence. The tile layout is a
/// multi-column grid with gaps, so "let go slightly beside the tile" is a normal thing to happen - it used
/// to do nothing at all, which is indistinguishable from a broken feature. All tests drive a real mouse.
/// </remarks>
[Trait("Category", "E2E")]
public sealed class PlaylistDragDropFeedbackE2ETests : PlaylistsE2ETestBase
{
    /// <summary>
    /// Verifies that letting go in the gap between two tiles moves the entry to the nearest tile instead
    /// of doing nothing. The tiles sit in a grid with a 1rem gap, so this is exactly where a drag that
    /// "looks right" to the user ends up.
    /// </summary>
    [Fact]
    public async Task E2E_DropInGapBetweenTiles_MovesEntryToNearestTile()
    {
        if (SkipBrowser)
            return;

        var (rowLocator, initialOrder) = await SetupManualPlaylistWithThreeEntriesAsync("Ziehen-In-Die-Luecke");

        var source = Page.Locator($"{EntryRowSelector}[data-media-id='{initialOrder[0]}']");
        var neighbour = Page.Locator($"{EntryRowSelector}[data-media-id='{initialOrder[1]}']");
        await source.ScrollIntoViewIfNeededAsync();
        var sourceBox = await RequireBoundingBoxAsync(source);
        var neighbourBox = await RequireBoundingBoxAsync(neighbour);

        // Mitte der Lücke zwischen der ersten und der zweiten Kachel - dort ist keine Kachel.
        var gapX = (sourceBox.X + sourceBox.Width + neighbourBox.X) / 2f;
        var gapY = neighbourBox.Y + neighbourBox.Height * 0.3f;
        Assert.True(gapX > sourceBox.X + sourceBox.Width, "Zwischen den Kacheln liegt keine Lücke.");

        await DragFromTileToPointAsync(source, gapX, gapY);

        // Der gezogene Titel nimmt die Position der nächstliegenden Kachel ein.
        Assert.Equal(new[] { initialOrder[1], initialOrder[0], initialOrder[2] }, await ReadEntryOrderAsync(rowLocator));
    }

    /// <summary>
    /// Verifies that letting go far away from the list changes nothing but says so, instead of leaving the
    /// user with a gesture that had no effect and no explanation.
    /// </summary>
    [Fact]
    public async Task E2E_DropFarOutsideTheList_KeepsOrder_AndShowsHint()
    {
        if (SkipBrowser)
            return;

        var (rowLocator, initialOrder) = await SetupManualPlaylistWithThreeEntriesAsync("Ziehen-Weit-Daneben");

        var source = Page.Locator($"{EntryRowSelector}[data-media-id='{initialOrder[0]}']");
        await source.ScrollIntoViewIfNeededAsync();
        var sourceBox = await RequireBoundingBoxAsync(source);

        // Weit oberhalb der Liste, im Kopfbereich der Seite.
        await DragFromTileToPointAsync(source, sourceBox.X + sourceBox.Width / 2, 120f);

        Assert.Equal(initialOrder, await ReadEntryOrderAsync(rowLocator));
        var hint = Page.Locator("#playlist-reorder-hint");
        await Expect(hint).ToHaveClassAsync(new Regex("playlist-reorder-hint-visible"));
        await Expect(hint).ToContainTextAsync("Nicht umsortiert");
    }

    /// <summary>
    /// Verifies that cancelling with Escape keeps the order AND does not select the entry: the browser
    /// still fires a click when the button is released afterwards, and after an explicit cancel that click
    /// must not reach the tile.
    /// </summary>
    [Fact]
    public async Task E2E_EscapeDuringDrag_KeepsOrder_AndDoesNotSelectEntry()
    {
        if (SkipBrowser)
            return;

        var (rowLocator, initialOrder) = await SetupManualPlaylistWithThreeEntriesAsync("Ziehen-Abbrechen");

        // Innerhalb derselben Kachel ziehen und dort wieder loslassen: so trifft der Klick, den der
        // Browser nach dem Loslassen erzeugt, genau diese Kachel - der Fall, in dem ein Abbruch sonst
        // doch noch eine Auswahl ausloest.
        var source = Page.Locator($"{EntryRowSelector}[data-media-id='{initialOrder[0]}']");
        await source.ScrollIntoViewIfNeededAsync();
        var sourceBox = await RequireBoundingBoxAsync(source);
        var startX = sourceBox.X + sourceBox.Width * 0.55f;
        var startY = sourceBox.Y + sourceBox.Height * 0.3f;

        await Page.Mouse.MoveAsync(startX, startY);
        await Page.Mouse.DownAsync();
        await Page.WaitForTimeoutAsync(120);
        for (var step = 1; step <= 8; step++)
        {
            await Page.Mouse.MoveAsync(startX + 8f * step, startY);
            await Page.WaitForTimeoutAsync(40);
        }

        await Page.Keyboard.PressAsync("Escape");
        await Page.WaitForTimeoutAsync(300);
        await Page.Mouse.UpAsync();
        await Page.WaitForTimeoutAsync(1500);

        Assert.Equal(initialOrder, await ReadEntryOrderAsync(rowLocator));
        await Expect(Page.Locator("#playlist-detail-selected-entry")).ToHaveCountAsync(0);
    }

    /// <summary>
    /// Verifies that a reorder the server refuses - here because the entry was removed from the playlist
    /// while it was being dragged - shows an error message and reloads the list, so the removed tile does
    /// not stay on screen and the shown order matches the server again.
    /// </summary>
    [Fact]
    public async Task E2E_ServerRefusesReorder_ShowsError_AndReloadsList()
    {
        if (SkipBrowser)
            return;

        var (rowLocator, initialOrder) = await SetupManualPlaylistWithThreeEntriesAsync("Ziehen-Servermeldung");

        var source = Page.Locator($"{EntryRowSelector}[data-media-id='{initialOrder[0]}']");
        var target = Page.Locator($"{EntryRowSelector}[data-media-id='{initialOrder[2]}']");
        await BeginEntryDragAsync(source, target);

        // Während die Maustaste gedrückt ist, verschwindet der gezogene Eintrag serverseitig.
        await RemoveEntryFromDatabaseAsync("Ziehen-Servermeldung", long.Parse(initialOrder[0]));

        await Page.Mouse.UpAsync();
        await Page.WaitForTimeoutAsync(2500);

        await Expect(Page.Locator("#playlist-entries-status")).ToContainTextAsync("Fehler beim Umordnen");
        var afterDrop = await ReadEntryOrderAsync(rowLocator);
        Assert.Equal(new[] { initialOrder[1], initialOrder[2] }, afterDrop);
    }

    /// <summary>
    /// Drags the given tile with real mouse input and lets go at the given point of the viewport (which
    /// may be anywhere, in particular NOT on a tile).
    /// </summary>
    /// <param name="source">The tile to drag.</param>
    /// <param name="endX">The horizontal position to let go at.</param>
    /// <param name="endY">The vertical position to let go at.</param>
    private async Task DragFromTileToPointAsync(ILocator source, float endX, float endY)
    {
        var sourceBox = await RequireBoundingBoxAsync(source);
        var startX = sourceBox.X + sourceBox.Width * 0.75f;
        var startY = sourceBox.Y + sourceBox.Height * 0.3f;

        await Page.Mouse.MoveAsync(startX, startY);
        await Page.Mouse.DownAsync();
        await Page.WaitForTimeoutAsync(120);
        for (var step = 1; step <= 12; step++)
        {
            await Page.Mouse.MoveAsync(startX + (endX - startX) * step / 12f, startY + (endY - startY) * step / 12f);
            await Page.WaitForTimeoutAsync(40);
        }

        await Page.WaitForTimeoutAsync(200);
        await Page.Mouse.UpAsync();
        await Page.WaitForTimeoutAsync(2000);
    }
}
