using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Real-browser layout checks of the playlist overview (Kundenrückmeldung zur Übersicht): the filter bar and the
/// "Neue Playlist" button share one horizontal line and one style, the filter bar looks like a single panel with
/// three switch areas separated by vertical rules, and the tiles follow the film/series tiles - image over the
/// whole tile, the title as the only text in the overlay at the bottom, the status symbol in the top right
/// corner. (These are CSS effects that bUnit cannot see.)
/// </summary>
[Trait("Category", "E2E")]
public sealed class PlaylistsOverviewLayoutE2ETests : PlaylistsE2ETestBase
{
    private async Task OpenOverviewAsync(int width = 1440, int height = 900)
    {
        await Page.SetViewportSizeAsync(width, height);
        await Page.GotoAsync($"{ServerUrl}/playlists");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(1200);
    }

    private static double Px(string value)
        => double.Parse(value.Replace("px", string.Empty), System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// All four buttons (three filters plus "Neue Playlist") lie on one horizontal line and have the same height
    /// and the same corner radius - the customer rejected the previous mix of differently shaped buttons.
    /// </summary>
    [Fact]
    public async Task FilterButtonsAndCreateButton_ShareOneLine_AndTheSameHeightAndShape()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        await CreatePlaylistViaUiAsync("Leisten-Test");
        await OpenOverviewAsync();

        var boxes = new List<(string Name, float Top, float Height)>();
        foreach (var selector in new[] { "#playlist-filter-all", "#playlist-filter-own", "#playlist-filter-public", "#create-playlist-button" })
        {
            var box = await Page.Locator(selector).BoundingBoxAsync();
            Assert.NotNull(box);
            boxes.Add((selector, box!.Y, box.Height));
        }

        var referenceTop = boxes[0].Top;
        var referenceHeight = boxes[0].Height;
        foreach (var (name, top, height) in boxes)
        {
            Assert.True(Math.Abs(top - referenceTop) <= 1, $"{name} liegt nicht auf derselben Ebene (oben {top}px statt {referenceTop}px)");
            Assert.True(Math.Abs(height - referenceHeight) <= 1, $"{name} ist {height}px hoch statt {referenceHeight}px");
        }

        // Same rounded shape for the group and the create button.
        var groupRadius = await Page.Locator("#playlist-filter-group").EvaluateAsync<string>("e => getComputedStyle(e).borderTopLeftRadius");
        var createRadius = await Page.Locator("#create-playlist-button").EvaluateAsync<string>("e => getComputedStyle(e).borderTopLeftRadius");
        Assert.Equal(groupRadius, createRadius);
        Assert.True(Px(groupRadius) > 0, "Die Filterleiste hat keine Rundung");
    }

    /// <summary>
    /// The filter bar is ONE panel: the frame belongs to the group, the three areas have no frame of their own,
    /// they touch each other seamlessly and are separated by a vertical rule. The active area is filled.
    /// </summary>
    [Fact]
    public async Task FilterBar_IsOnePanelWithThreeSwitchAreasAndVerticalRules()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        await CreatePlaylistViaUiAsync("Filterleiste");
        await OpenOverviewAsync();

        var groupBorder = await Page.Locator("#playlist-filter-group").EvaluateAsync<string>("e => getComputedStyle(e).borderTopWidth");
        Assert.True(Px(groupBorder) >= 1, $"Die Filterleiste hat keinen eigenen Rahmen ({groupBorder})");

        // No individual frames, and the three areas touch without a gap (one panel, not three buttons).
        double? previousRight = null;
        foreach (var selector in new[] { "#playlist-filter-all", "#playlist-filter-own", "#playlist-filter-public" })
        {
            var border = await Page.Locator(selector).EvaluateAsync<string>("e => getComputedStyle(e).borderTopWidth");
            Assert.True(Px(border) == 0, $"{selector} hat einen eigenen Rahmen ({border})");

            var box = await Page.Locator(selector).BoundingBoxAsync();
            Assert.NotNull(box);
            if (previousRight is { } right)
                Assert.True(Math.Abs(box!.X - right) <= 1, $"{selector} haengt nicht am vorherigen Schaltbereich (Luecke {box.X - right}px)");
            previousRight = box!.X + box.Width;
        }

        // Vertical rule between the areas: the ::before pseudo element of a following area is visible.
        var ruleColor = await Page.Locator("#playlist-filter-public").EvaluateAsync<string>(
            "e => getComputedStyle(e, '::before').backgroundColor");
        Assert.NotEqual("rgba(0, 0, 0, 0)", ruleColor);

        // Exactly one area is marked as active, and it is filled with the accent colour.
        await Expect(Page.Locator(".playlist-filter-button.active")).ToHaveCountAsync(1);
        var activeBackground = await Page.Locator("#playlist-filter-all").EvaluateAsync<string>("e => getComputedStyle(e).backgroundColor");
        var inactiveBackground = await Page.Locator("#playlist-filter-own").EvaluateAsync<string>("e => getComputedStyle(e).backgroundColor");
        Assert.NotEqual(activeBackground, inactiveBackground);
        Assert.Equal("rgb(229, 9, 20)", activeBackground);
    }

    /// <summary>
    /// The tile shows the cover over its whole area and the title as its only text at the bottom - no dates, no
    /// status wording, no description, no sort-mode hint (Kundenrückmeldung zur Übersicht).
    /// </summary>
    [Fact]
    public async Task Tile_CoverFillsTheWholeTile_TitleAtTheBottom_NoFurtherTexts()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        await CreatePlaylistViaUiAsync("Kachelbild", "Eine Beschreibung, die nicht in die Kachel gehoert");
        await SetPlaylistCoverAsync("Kachelbild", TestImages.Png(1600, 520), isUserUploaded: true);
        await MakePlaylistPublicAsync("Kachelbild");
        await OpenOverviewAsync();

        var tile = Page.Locator(".playlist-row[data-playlist-name='Kachelbild']");
        var card = await tile.Locator(".media-box").BoundingBoxAsync();
        var cover = await tile.Locator(".playlist-card-cover-image").BoundingBoxAsync();
        Assert.NotNull(card);
        Assert.NotNull(cover);
        // Toleranz 2px: die Kachel hat einen 1px-Rahmen, das Bild fuellt deren Innenflaeche.
        Assert.True(Math.Abs(cover!.Width - card!.Width) <= 2, $"Das Bild ist {cover.Width}px breit, die Kachel {card.Width}px");
        Assert.True(Math.Abs(cover.Height - card.Height) <= 2, $"Das Bild ist {cover.Height}px hoch, die Kachel {card.Height}px");
        Assert.Equal("cover", await tile.Locator(".playlist-card-cover-image").EvaluateAsync<string>("e => getComputedStyle(e).objectFit"));

        // The title sits in the lower half of the tile.
        var title = await tile.Locator(".playlist-card-title").BoundingBoxAsync();
        Assert.NotNull(title);
        Assert.True(title!.Y > card.Y + card.Height / 2, "Der Titel steht nicht am unteren Rand der Kachel");

        // The title is the only text of the tile.
        var text = (await tile.InnerTextAsync()).Trim();
        Assert.Equal("Kachelbild", text);

        // Status symbol in the top right corner, above the cover.
        var badge = await tile.Locator(".playlist-tile-badge").BoundingBoxAsync();
        Assert.NotNull(badge);
        Assert.True(badge!.Y < card.Y + card.Height / 2, "Das Kennzeichen liegt nicht am oberen Rand");
        Assert.True(badge.X > card.X + card.Width / 2, "Das Kennzeichen liegt nicht am rechten Rand");
    }

    /// <summary>
    /// A very long name must stay completely readable: it wraps (hyphenated) instead of being cut off, and it
    /// may use the full width of the tile.
    /// </summary>
    [Fact]
    public async Task LongTitle_WrapsOverSeveralLines_AndUsesTheFullTileWidth()
    {
        if (SkipBrowser)
            return;

        const string longName = "Bildergalerie der allerbesten Donnerstagabendunterhaltungssendungen";
        await LoginAsync(UserAEmail);
        await CreatePlaylistViaUiAsync(longName);
        await OpenOverviewAsync();

        var tile = Page.Locator($".playlist-row[data-playlist-name='{longName}']");
        var card = await tile.Locator(".media-box").BoundingBoxAsync();
        var title = tile.Locator(".playlist-card-title");
        var titleBox = await title.BoundingBoxAsync();
        Assert.NotNull(card);
        Assert.NotNull(titleBox);

        // Der Titel darf nicht kuenstlich schmal sein: mindestens 80 % der Kachelbreite abzueglich der Innenabstaende.
        Assert.True(titleBox!.Width >= (card!.Width - 32) * 0.8,
            $"Der Titel ist nur {titleBox.Width}px breit, die Kachel {card.Width}px");

        // Mehrzeilig und vollstaendig sichtbar (nicht abgeschnitten).
        var metrics = await title.EvaluateAsync<System.Text.Json.JsonElement>(
            "e => { const s = getComputedStyle(e); const lh = parseFloat(s.lineHeight) || parseFloat(s.fontSize) * 1.2; return { lines: Math.round(e.getBoundingClientRect().height / lh), scrollH: e.scrollHeight, clientH: e.clientHeight, wrap: s.overflowWrap, hyphens: s.hyphens }; }");
        Assert.True(metrics.GetProperty("lines").GetInt32() >= 2, "Der lange Titel wird nicht umgebrochen");
        Assert.True(metrics.GetProperty("scrollH").GetDouble() <= metrics.GetProperty("clientH").GetDouble() + 1,
            "Der Titel wird abgeschnitten");
        Assert.Equal("anywhere", metrics.GetProperty("wrap").GetString());
        Assert.Equal("auto", metrics.GetProperty("hyphens").GetString());
        await Expect(title).ToHaveTextAsync(longName);
    }

    /// <summary>
    /// The placeholder for a playlist without an image stays discreet instead of dominating the tile with an
    /// oversized list symbol ("wirkt amateurhaft").
    /// </summary>
    [Fact]
    public async Task PlaceholderSymbol_StaysSmallComparedToTheTile()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        await CreatePlaylistViaUiAsync("Ohne Bild");
        await OpenOverviewAsync();

        var tile = Page.Locator(".playlist-row[data-playlist-name='Ohne Bild']");
        var card = await tile.Locator(".media-box").BoundingBoxAsync();
        var icon = await tile.Locator(".playlist-cover-icon").BoundingBoxAsync();
        Assert.NotNull(card);
        Assert.NotNull(icon);

        Assert.True(icon!.Height <= card!.Height * 0.5, $"Das Platzhaltersymbol ist {icon.Height}px hoch bei {card.Height}px Kachelhoehe");
        var opacity = await tile.Locator(".playlist-cover-icon").EvaluateAsync<double>("e => parseFloat(getComputedStyle(e).opacity)");
        Assert.True(opacity < 0.6, $"Das Platzhaltersymbol ist mit Deckkraft {opacity} zu praesent");
    }

    /// <summary>
    /// On a phone-sized screen everything stays on one line and inside the viewport, and the tiles use the full
    /// width instead of being squeezed into two narrow columns.
    /// </summary>
    [Fact]
    public async Task NarrowScreen_KeepsTheActionRowOnOneLine_AndTilesReadable()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        await CreatePlaylistViaUiAsync("Schmale Kachel");
        await OpenOverviewAsync(390, 844);

        var tops = new List<float>();
        foreach (var selector in new[] { "#playlist-filter-all", "#playlist-filter-public", "#create-playlist-button" })
        {
            var box = await Page.Locator(selector).BoundingBoxAsync();
            Assert.NotNull(box);
            Assert.True(box!.X >= 0 && box.X + box.Width <= 390, $"{selector} ragt aus dem Bildschirm");
            tops.Add(box.Y);
        }
        Assert.True(tops.Max() - tops.Min() <= 1, "Die Schaltflaechen liegen auf einem Telefon nicht auf einer Ebene");

        var scrollWidth = await Page.EvaluateAsync<double>("document.documentElement.scrollWidth");
        Assert.True(scrollWidth <= 391, $"Horizontaler Ueberlauf: scrollWidth {scrollWidth}px");

        var card = await Page.Locator(".playlist-row[data-playlist-name='Schmale Kachel'] .media-box").BoundingBoxAsync();
        Assert.NotNull(card);
        Assert.True(card!.Width >= 300, $"Die Kachel ist auf dem Telefon nur {card.Width}px breit");
    }
}
