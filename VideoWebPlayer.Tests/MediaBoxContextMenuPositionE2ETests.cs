using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// End-to-end-Tests mit Playwright für die responsive Randpositionierung des
/// MediaBox-Kontextmenüs bei erster/letzter Karte auf Desktop- und mobilem Viewport.
/// </summary>
[Trait("Category", "E2E")]
public sealed class MediaBoxContextMenuPositionE2ETests : MediaBoxContextMenuE2ETestBase
{
    [Theory]
    [InlineData(1280, 800)]
    [InlineData(375, 667)]
    public async Task OpenMenu_OnFirstCard_StaysWithinViewportBounds(int width, int height)
    {
        if (SkipBrowser)
            return;

        await Page.SetViewportSizeAsync(width, height);
        await LoginAndNavigateToHomeAsync();

        await AssertMenuStaysWithinViewportAsync(FavoriteCards.First, width, height);
    }

    [Theory]
    [InlineData(1280, 800)]
    [InlineData(375, 667)]
    public async Task OpenMenu_OnLastCard_StaysWithinViewportBounds(int width, int height)
    {
        if (SkipBrowser)
            return;

        await Page.SetViewportSizeAsync(width, height);
        await LoginAndNavigateToHomeAsync();

        await AssertMenuStaysWithinViewportAsync(FavoriteCards.Last, width, height);
    }

    private async Task AssertMenuStaysWithinViewportAsync(ILocator card, int viewportWidth, int viewportHeight)
    {
        var (x, y) = await GetCenterAsync(card);

        await Page.Mouse.MoveAsync((float)x, (float)y);
        await Page.Mouse.DownAsync();
        await Page.WaitForTimeoutAsync(3100);
        await Page.Mouse.UpAsync();

        var menu = Page.GetByRole(AriaRole.Menu);
        await Expect(menu).ToBeVisibleAsync();

        var box = await menu.BoundingBoxAsync();
        Assert.NotNull(box);

        const double tolerance = 1.0;
        Assert.True(box!.X >= -tolerance, $"Menü ragt links aus dem Viewport heraus (x={box.X}).");
        Assert.True(box.Y >= -tolerance, $"Menü ragt oben aus dem Viewport heraus (y={box.Y}).");
        Assert.True(box.X + box.Width <= viewportWidth + tolerance,
            $"Menü ragt rechts aus dem Viewport heraus (x+width={box.X + box.Width}, viewport={viewportWidth}).");
        Assert.True(box.Y + box.Height <= viewportHeight + tolerance,
            $"Menü ragt unten aus dem Viewport heraus (y+height={box.Y + box.Height}, viewport={viewportHeight}).");
    }
}
