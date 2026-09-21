using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// End-to-end tests with Playwright covering reordering playlist entries by dragging them (manual sort
/// mode, <c>PlaylistEntriesList.razor</c> plus <c>wwwroot/js/playlistDragDrop.js</c>). Split out of
/// <see cref="PlaylistReorderE2ETests"/> - which keeps the sort-mode-change workflow and the
/// "An Anfang"/"An Ende" quick actions - so each file covers one topic.
/// </summary>
/// <remarks>
/// Every test here drives a REAL mouse (<c>Page.Mouse.Down/Move/Up</c> in many small steps) or real touch
/// input, never JS-dispatched synthetic events: the previously used
/// <c>element.dispatchEvent(new DragEvent(...))</c> approach called the Blazor handlers directly and
/// therefore could not notice that the feature was broken for actual users.
/// </remarks>
[Trait("Category", "E2E")]
public sealed class PlaylistDragDropReorderE2ETests : PlaylistsE2ETestBase
{
    private const string RowSelector = ".playlist-entries-list-wrap .playlist-entry-row";

    /// <summary>
    /// Verifies the main gesture: dragging the first entry onto the third entry's tile puts it at the
    /// third position, and the new order survives a full page reload (i.e. it was persisted server-side,
    /// not just rearranged in the browser).
    /// </summary>
    [Fact]
    public async Task E2E_DragWithMouse_OntoThirdEntry_ReordersAndPersistsAfterReload()
    {
        if (SkipBrowser)
            return;

        var (rowLocator, initialOrder) = await SetupManualPlaylistWithThreeEntriesAsync("Ziehen-Nach-Hinten");

        await DragEntryOntoEntryAsync(initialOrder[0], initialOrder[2]);

        // Der gezogene Titel steht auf der Position des Ziels, die beiden anderen ruecken auf.
        var expected = new[] { initialOrder[1], initialOrder[2], initialOrder[0] };
        Assert.Equal(expected, await ReadOrderAsync(rowLocator));

        await Page.ReloadAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await Page.WaitForTimeoutAsync(1500);

        Assert.Equal(expected, await ReadOrderAsync(rowLocator));
    }

    /// <summary>
    /// Verifies dragging towards the front: dragging the last entry onto the first entry's tile makes it
    /// the first entry.
    /// </summary>
    [Fact]
    public async Task E2E_DragWithMouse_OntoFirstEntry_MovesEntryToFront()
    {
        if (SkipBrowser)
            return;

        var (rowLocator, initialOrder) = await SetupManualPlaylistWithThreeEntriesAsync("Ziehen-Nach-Vorn");

        await DragEntryOntoEntryAsync(initialOrder[2], initialOrder[0]);

        Assert.Equal(new[] { initialOrder[2], initialOrder[0], initialOrder[1] }, await ReadOrderAsync(rowLocator));
    }

    /// <summary>
    /// Verifies that the gesture may be started on the poster image of a tile. Images are natively
    /// draggable, so without <c>draggable="false"</c> the browser would start its own image drag instead
    /// and abort the reorder gesture.
    /// </summary>
    [Fact]
    public async Task E2E_DragStartedOnPosterImage_ReordersEntry()
    {
        if (SkipBrowser)
            return;

        var (rowLocator, initialOrder) = await SetupManualPlaylistWithThreeEntriesAsync("Ziehen-Am-Bild");

        await DragEntryOntoEntryAsync(initialOrder[0], initialOrder[2], sourceChildSelector: ".playlist-entry-image");

        Assert.Equal(new[] { initialOrder[1], initialOrder[2], initialOrder[0] }, await ReadOrderAsync(rowLocator));
    }

    /// <summary>
    /// Verifies that the gesture may be started on the title text of a tile (text is natively selectable
    /// and, once selected, natively draggable - both must not get in the way).
    /// </summary>
    [Fact]
    public async Task E2E_DragStartedOnTitleText_ReordersEntry()
    {
        if (SkipBrowser)
            return;

        var (rowLocator, initialOrder) = await SetupManualPlaylistWithThreeEntriesAsync("Ziehen-Am-Text");

        await DragEntryOntoEntryAsync(initialOrder[0], initialOrder[2], sourceChildSelector: ".episode-title");

        Assert.Equal(new[] { initialOrder[1], initialOrder[2], initialOrder[0] }, await ReadOrderAsync(rowLocator));
    }

    /// <summary>
    /// Verifies the visual feedback during the gesture: the dragged tile is dimmed
    /// (<c>playlist-entry-dragging</c>) and the tile under the pointer is marked as the drop target
    /// (<c>playlist-entry-drop-target</c>), so it is visible where the title will land.
    /// </summary>
    [Fact]
    public async Task E2E_WhileDragging_SourceAndTargetTileAreMarked()
    {
        if (SkipBrowser)
            return;

        var (_, initialOrder) = await SetupManualPlaylistWithThreeEntriesAsync("Ziehen-Rueckmeldung");

        var source = Page.Locator($"{RowSelector}[data-media-id='{initialOrder[0]}']");
        var target = Page.Locator($"{RowSelector}[data-media-id='{initialOrder[2]}']");
        await BeginDragAsync(source, target);

        await Expect(source).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("playlist-entry-dragging"));
        await Expect(target).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("playlist-entry-drop-target"));

        await Page.Mouse.UpAsync();
        await Page.WaitForTimeoutAsync(1500);

        await Expect(source).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("playlist-entry-dragging"));
    }

    /// <summary>
    /// Verifies that dragging neither selects the entry (which would replace the playlist information in
    /// the header) nor starts playback - the gesture must swallow the click the browser fires after the
    /// mouse button is released.
    /// </summary>
    [Fact]
    public async Task E2E_Drag_DoesNotSelectEntry_AndDoesNotStartPlayback()
    {
        if (SkipBrowser)
            return;

        var (_, initialOrder) = await SetupManualPlaylistWithThreeEntriesAsync("Ziehen-Ohne-Auswahl");
        var detailUrl = Page.Url;

        await DragEntryOntoEntryAsync(initialOrder[0], initialOrder[2]);

        await Expect(Page.Locator("#playlist-detail-selected-entry")).ToHaveCountAsync(0);
        Assert.Equal(new Uri(detailUrl).AbsolutePath, new Uri(Page.Url).AbsolutePath);
    }

    /// <summary>
    /// Verifies that a plain click still selects an entry after a drag was performed before - the click
    /// suppression must apply to the drag's own click only.
    /// </summary>
    [Fact]
    public async Task E2E_ClickAfterDrag_StillSelectsEntry()
    {
        if (SkipBrowser)
            return;

        var (_, initialOrder) = await SetupManualPlaylistWithThreeEntriesAsync("Klick-Nach-Ziehen");

        await DragEntryOntoEntryAsync(initialOrder[0], initialOrder[2]);

        // Auf den Titeltext klicken: die Mitte der Kachel liegt im manuellen Modus ueber den
        // Schnellaktions-Schaltflaechen.
        await Page.ClickAsync($"{RowSelector}[data-media-id='{initialOrder[0]}'] .episode-title");
        await Expect(Page.Locator("#playlist-detail-selected-entry")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Verifies that reordering does not depend on an earlier message having reached the server: the
    /// first message the browser sends after the gesture has begun is held back for five seconds here,
    /// while everything sent afterwards goes out immediately - what an unreliable connection (a repeated
    /// frame, SignalR long polling without guaranteed order) does to the message order.
    /// </summary>
    /// <remarks>
    /// This is the regression guard for the reported bug. The previous implementation used native HTML5
    /// drag &amp; drop and needed the <c>dragstart</c> message - the first one of the gesture - to be
    /// processed by the server BEFORE the drop arrived, because it remembered the dragged entry in a
    /// server-side field. Arriving late, that field was still <see langword="null"/> when the drop was
    /// handled and the drop was discarded without any message: "dragging does nothing, only the buttons
    /// work". The gesture now runs entirely in the browser and reaches the server exactly once, on
    /// release, carrying both entry ids - so a delayed message only delays the reordering.
    /// </remarks>
    [Fact]
    public async Task E2E_DragWithMouse_FirstMessageDelayed_StillReordersEntry()
    {
        if (SkipBrowser)
            return;

        var (rowLocator, initialOrder) = await SetupManualPlaylistWithThreeEntriesAsync("Ziehen-Verzoegerte-Nachricht");

        await Page.EvaluateAsync(@"() => {
            window.__delayNextMessage = false;
            const originalSend = WebSocket.prototype.send;
            WebSocket.prototype.send = function (data) {
                if (window.__delayNextMessage) {
                    window.__delayNextMessage = false;
                    window.setTimeout(() => originalSend.call(this, data), 5000);
                    return;
                }
                return originalSend.call(this, data);
            };
        }");
        await Page.EvaluateAsync("() => { window.__delayNextMessage = true; }");

        await DragEntryOntoEntryAsync(initialOrder[0], initialOrder[2], settleMilliseconds: 12000);

        Assert.Equal(new[] { initialOrder[1], initialOrder[2], initialOrder[0] }, await ReadOrderAsync(rowLocator));
    }

    /// <summary>
    /// Verifies that reordering works with a finger as well: native HTML5 drag &amp; drop does not react
    /// to touch input at all, the pointer-based gesture starts after a short press and hold.
    /// </summary>
    [Fact]
    public async Task E2E_DragWithTouch_AfterLongPress_ReordersEntry()
    {
        if (SkipBrowser)
            return;

        var (rowLocator, initialOrder) = await SetupManualPlaylistWithThreeEntriesAsync("Ziehen-Mit-Finger");

        var source = Page.Locator($"{RowSelector}[data-media-id='{initialOrder[0]}']");
        var target = Page.Locator($"{RowSelector}[data-media-id='{initialOrder[2]}']");
        await source.ScrollIntoViewIfNeededAsync();
        var sourceBox = await RequireBoundingBoxAsync(source);
        var targetBox = await RequireBoundingBoxAsync(target);

        var cdp = await Page.Context.NewCDPSessionAsync(Page);
        await cdp.SendAsync("Emulation.setTouchEmulationEnabled", new Dictionary<string, object>
        {
            ["enabled"] = true,
            ["maxTouchPoints"] = 1
        });

        var startX = sourceBox.X + sourceBox.Width * 0.75f;
        var startY = sourceBox.Y + sourceBox.Height * 0.3f;
        var endX = targetBox.X + targetBox.Width * 0.75f;
        var endY = targetBox.Y + targetBox.Height * 0.3f;

        await DispatchTouchAsync(cdp, "touchStart", startX, startY);
        // Kurz halten: erst danach beginnt das Ziehen (vorher bliebe es beim Scrollen).
        await Page.WaitForTimeoutAsync(700);
        for (var step = 1; step <= 12; step++)
        {
            await DispatchTouchAsync(cdp, "touchMove", startX + (endX - startX) * step / 12f, startY + (endY - startY) * step / 12f);
            await Page.WaitForTimeoutAsync(50);
        }
        await DispatchTouchAsync(cdp, "touchEnd", endX, endY);
        await Page.WaitForTimeoutAsync(2000);

        Assert.Equal(new[] { initialOrder[1], initialOrder[2], initialOrder[0] }, await ReadOrderAsync(rowLocator));
    }

    /// <summary>
    /// Verifies that entries of a playlist sorted by release date cannot be dragged at all (the order is
    /// derived from the release date there, so a manual position would be meaningless).
    /// </summary>
    [Fact]
    public async Task E2E_DateSortMode_DraggingDoesNotChangeOrder()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        await Page.SetViewportSizeAsync(1440, 900);
        var row = await CreatePlaylistViaUiAsync("Ziehen-Datumsmodus");
        await SeedMoviesIntoPlaylistAsync("Ziehen-Datumsmodus", 3);
        await row.ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await Page.WaitForTimeoutAsync(1500);

        var rowLocator = Page.Locator(RowSelector);
        var initialOrder = await ReadOrderAsync(rowLocator);
        Assert.Equal(3, initialOrder.Length);
        await Expect(Page.Locator(".playlist-entries-draganddrop-hint")).ToHaveCountAsync(0);

        await DragEntryOntoEntryAsync(initialOrder[0], initialOrder[2]);

        Assert.Equal(initialOrder, await ReadOrderAsync(rowLocator));
    }

    /// <summary>
    /// Verifies that a user who is not the owner cannot drag the entries of a foreign public playlist,
    /// even though it is in manual sort mode.
    /// </summary>
    [Fact]
    public async Task E2E_ForeignPublicPlaylist_DraggingDoesNotChangeOrder()
    {
        if (SkipBrowser)
            return;

        var (_, initialOrder) = await SetupManualPlaylistWithThreeEntriesAsync("Ziehen-Fremde-Playlist");
        var detailUrl = Page.Url;
        await MakePlaylistPublicAsync("Ziehen-Fremde-Playlist");

        await LoginAsync(UserBEmail);
        await Page.GotoAsync(detailUrl);
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await Page.WaitForTimeoutAsync(1500);

        var rowLocator = Page.Locator(RowSelector);
        Assert.Equal(initialOrder, await ReadOrderAsync(rowLocator));
        await Expect(Page.Locator(".playlist-entries-draganddrop-hint")).ToHaveCountAsync(0);

        await DragEntryOntoEntryAsync(initialOrder[0], initialOrder[2]);

        Assert.Equal(initialOrder, await ReadOrderAsync(rowLocator));
    }

    /// <summary>
    /// Reads the current front-to-back media-id order of the rendered entry tiles.
    /// </summary>
    /// <param name="rowLocator">The locator matching the entry tiles.</param>
    /// <returns>The media ids in the rendered order.</returns>
    private static Task<string[]> ReadOrderAsync(ILocator rowLocator)
        => rowLocator.EvaluateAllAsync<string[]>("els => els.map(e => e.getAttribute('data-media-id'))");

    /// <summary>
    /// Drags the tile of the given media onto the tile of the other media with real mouse input, the way
    /// a person does it: press, many small moves with pauses in between, release.
    /// </summary>
    /// <param name="sourceMediaId">The media id of the tile to drag.</param>
    /// <param name="targetMediaId">The media id of the tile to drop it on.</param>
    /// <param name="sourceChildSelector">An element inside the source tile to start the gesture on (poster, title, ...); the tile itself when omitted.</param>
    /// <param name="settleMilliseconds">How long to wait for the reordered list after releasing the mouse.</param>
    private async Task DragEntryOntoEntryAsync(string sourceMediaId, string targetMediaId, string? sourceChildSelector = null, int settleMilliseconds = 2000)
    {
        var sourceRow = Page.Locator($"{RowSelector}[data-media-id='{sourceMediaId}']");
        var source = sourceChildSelector is null ? sourceRow : sourceRow.Locator(sourceChildSelector);
        var target = Page.Locator($"{RowSelector}[data-media-id='{targetMediaId}']");

        await BeginDragAsync(source, target);
        await Page.Mouse.UpAsync();
        await Page.WaitForTimeoutAsync(settleMilliseconds);
    }

    /// <summary>
    /// Presses the mouse on the source element and moves it onto the target element, leaving the button
    /// pressed, so the caller can inspect the running gesture before releasing it.
    /// </summary>
    /// <param name="source">The element to start the gesture on.</param>
    /// <param name="target">The tile to move the pointer onto.</param>
    private async Task BeginDragAsync(ILocator source, ILocator target)
    {
        await source.ScrollIntoViewIfNeededAsync();
        var sourceBox = await RequireBoundingBoxAsync(source);
        var targetBox = await RequireBoundingBoxAsync(target);

        // Oberer rechter Bereich der Kachel: dort liegen weder das Poster (links) noch die
        // Schnellaktions-Schaltflaechen (unten), wenn keine abweichende Startstelle verlangt wurde.
        var startX = sourceBox.X + sourceBox.Width * 0.75f;
        var startY = sourceBox.Y + sourceBox.Height * 0.3f;
        var endX = targetBox.X + targetBox.Width * 0.75f;
        var endY = targetBox.Y + targetBox.Height * 0.3f;

        await Page.Mouse.MoveAsync(startX, startY);
        await Page.Mouse.DownAsync();
        await Page.WaitForTimeoutAsync(120);
        for (var step = 1; step <= 12; step++)
        {
            await Page.Mouse.MoveAsync(startX + (endX - startX) * step / 12f, startY + (endY - startY) * step / 12f);
            await Page.WaitForTimeoutAsync(40);
        }

        await Page.WaitForTimeoutAsync(200);
    }

    /// <summary>
    /// Returns the bounding box of the given element, failing the test if it has none (not rendered).
    /// </summary>
    /// <param name="locator">The element to measure.</param>
    /// <returns>The element's bounding box.</returns>
    private static async Task<LocatorBoundingBoxResult> RequireBoundingBoxAsync(ILocator locator)
        => await locator.BoundingBoxAsync()
            ?? throw new InvalidOperationException("Das Element ist nicht sichtbar und hat keine Ausmaße.");

    /// <summary>
    /// Dispatches a single-finger touch event through the Chrome DevTools Protocol (Playwright's own
    /// touch API only offers taps, not a drag).
    /// </summary>
    /// <param name="cdp">The DevTools session of the page.</param>
    /// <param name="type">The touch event type (<c>touchStart</c>, <c>touchMove</c>, <c>touchEnd</c>).</param>
    /// <param name="x">The horizontal position of the finger.</param>
    /// <param name="y">The vertical position of the finger.</param>
    private static Task DispatchTouchAsync(ICDPSession cdp, string type, float x, float y)
        => cdp.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object>
        {
            ["type"] = type,
            ["touchPoints"] = type == "touchEnd"
                ? Array.Empty<object>()
                : new object[] { new Dictionary<string, object> { ["x"] = x, ["y"] = y, ["id"] = 1 } }
        });
}
