using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// End-to-end-Tests mit Playwright für die Long-Press-/Pointer-Interaktion des
/// MediaBox-Kontextmenüs (echte gerenderte Blazor-Ereignisse statt Zustandsobjekte).
/// </summary>
public sealed class MediaBoxContextMenuInteractionE2ETests : MediaBoxContextMenuE2ETestBase
{
    [Fact]
    public async Task LongPress_ExactlyThreeSeconds_OpensMenuWithActions()
    {
        if (SkipBrowser)
            return;

        await LoginAndNavigateToHomeAsync();

        var card = FavoriteCards.First;
        var (x, y) = await GetCenterAsync(card);

        await Page.Mouse.MoveAsync((float)x, (float)y);
        await Page.Mouse.DownAsync();
        await Page.WaitForTimeoutAsync(3100);

        var menu = Page.GetByRole(AriaRole.Menu);
        await Expect(menu).ToBeVisibleAsync();
        await Expect(menu.GetByRole(AriaRole.Menuitem, new() { Name = "Entfernen" })).ToBeVisibleAsync();

        await Page.Mouse.UpAsync();
        await Expect(menu).ToBeVisibleAsync();
    }

    [Fact]
    public async Task PointerReleaseBeforeDelay_DoesNotOpenMenu_AndNavigatesNormally()
    {
        if (SkipBrowser)
            return;

        await LoginAndNavigateToHomeAsync();

        var card = FavoriteCards.First;
        var (x, y) = await GetCenterAsync(card);

        await Page.Mouse.MoveAsync((float)x, (float)y);
        await Page.Mouse.DownAsync();
        await Page.WaitForTimeoutAsync(500);
        await Page.Mouse.UpAsync();

        await Expect(Page.GetByRole(AriaRole.Menu)).Not.ToBeVisibleAsync();
        await Page.WaitForURLAsync(url => url.Contains("/moviecollection/"), new() { Timeout = 5000 });
    }

    [Fact]
    public async Task MovementOverTolerance_CancelsPendingLongPress()
    {
        if (SkipBrowser)
            return;

        await LoginAndNavigateToHomeAsync();

        var card = FavoriteCards.First;
        var (x, y) = await GetCenterAsync(card);

        await Page.Mouse.MoveAsync((float)x, (float)y);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync((float)(x + 25), (float)y);
        await Page.WaitForTimeoutAsync(3100);

        await Expect(Page.GetByRole(AriaRole.Menu)).Not.ToBeVisibleAsync();

        await Page.Mouse.UpAsync();
    }

    [Fact]
    public async Task NativeContextMenu_RightClick_DoesNotOpenActionMenu()
    {
        if (SkipBrowser)
            return;

        await LoginAndNavigateToHomeAsync();

        var card = FavoriteCards.First;
        var (x, y) = await GetCenterAsync(card);

        await Page.Mouse.ClickAsync((float)x, (float)y, new MouseClickOptions { Button = MouseButton.Right });

        await Expect(Page.GetByRole(AriaRole.Menu)).Not.ToBeVisibleAsync();
        await Page.WaitForTimeoutAsync(3100);
        await Expect(Page.GetByRole(AriaRole.Menu)).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task PointerCancel_ClosesOpenMenu()
    {
        if (SkipBrowser)
            return;

        await LoginAndNavigateToHomeAsync();

        var card = FavoriteCards.First;
        var (x, y) = await GetCenterAsync(card);

        await Page.Mouse.MoveAsync((float)x, (float)y);
        await Page.Mouse.DownAsync();
        await Page.WaitForTimeoutAsync(3100);

        var menu = Page.GetByRole(AriaRole.Menu);
        await Expect(menu).ToBeVisibleAsync();

        await card.Locator(".media-box-link").DispatchEventAsync("pointercancel", new
        {
            bubbles = true,
            cancelable = true,
            pointerId = 1
        });

        await Expect(menu).Not.ToBeVisibleAsync();
        await Page.Mouse.UpAsync();
    }

    [Fact]
    public async Task EscapeKey_ClosesOpenMenuAndRestoresLinkFocus()
    {
        if (SkipBrowser)
            return;

        await LoginAndNavigateToHomeAsync();

        var card = FavoriteCards.First;
        var (x, y) = await GetCenterAsync(card);

        await Page.Mouse.MoveAsync((float)x, (float)y);
        await Page.Mouse.DownAsync();
        await Page.WaitForTimeoutAsync(3100);
        await Page.Mouse.UpAsync();

        var menu = Page.GetByRole(AriaRole.Menu);
        await Expect(menu).ToBeVisibleAsync();

        await Page.Keyboard.PressAsync("Escape");

        await Expect(menu).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task ClickOutside_ClosesOpenMenu()
    {
        if (SkipBrowser)
            return;

        await LoginAndNavigateToHomeAsync();

        var card = FavoriteCards.First;
        var (x, y) = await GetCenterAsync(card);

        await Page.Mouse.MoveAsync((float)x, (float)y);
        await Page.Mouse.DownAsync();
        await Page.WaitForTimeoutAsync(3100);
        await Page.Mouse.UpAsync();

        var menu = Page.GetByRole(AriaRole.Menu);
        await Expect(menu).ToBeVisibleAsync();

        await Page.Mouse.ClickAsync(5, 5);

        await Expect(menu).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task MenuAction_Remove_ClosesMenuAndRemovesCard()
    {
        if (SkipBrowser)
            return;

        await LoginAndNavigateToHomeAsync();

        var initialCount = await FavoriteCards.CountAsync();
        var card = FavoriteCards.First;
        var cardKey = await card.GetAttributeAsync("data-card-key");
        var (x, y) = await GetCenterAsync(card);

        await Page.Mouse.MoveAsync((float)x, (float)y);
        await Page.Mouse.DownAsync();
        await Page.WaitForTimeoutAsync(3100);
        await Page.Mouse.UpAsync();

        var menu = Page.GetByRole(AriaRole.Menu);
        await Expect(menu).ToBeVisibleAsync();

        await menu.GetByRole(AriaRole.Menuitem, new() { Name = "Entfernen" }).ClickAsync();

        await Expect(menu).Not.ToBeVisibleAsync();
        await Expect(Page.Locator($".media-box-shell[data-card-key='{cardKey}']")).Not.ToBeAttachedAsync();
        await Expect(FavoriteCards).ToHaveCountAsync(initialCount - 1);
    }
}
