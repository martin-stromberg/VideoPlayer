using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Real-browser layout checks of the playlist detail header (Kundenrückmeldung zur Detailansicht): a fixed header
/// height that the cover image cannot change (very tall, very wide, tiny, no image), the same header structure and
/// dimming as the series page, action buttons on the image, readable dates, and a visible frame on every overlay
/// panel/dialog of the feature. (These properties are CSS effects that bUnit cannot see.)
/// </summary>
[Trait("Category", "E2E")]
public sealed class PlaylistDetailLayoutE2ETests : PlaylistsE2ETestBase
{
    private const string HeaderSelector = "#playlist-detail-header";

    private static readonly (string Name, int Width, int Height)[] ExtremeImages =
    [
        ("sehr hoch", 400, 3000),
        ("sehr breit", 4000, 300),
        ("klein", 32, 32),
    ];

    private async Task<double> HeightOfAsync(string selector)
        => await Page.Locator(selector).First.EvaluateAsync<double>("e => e.getBoundingClientRect().height");

    private async Task<long> PrepareOwnerWithPlaylistAsync(string name)
    {
        await LoginAsync(UserAEmail);
        await GrantMediaSourceAccessForUserAsync(UserAEmail);
        await CreatePlaylistViaUiAsync(name, "Eine kurze Beschreibung der Playlist.");
        await SeedMovieWithPosterIntoPlaylistAsync(name, $"{name} Film");
        return await SetPlaylistCoverAsync(name, null, isUserUploaded: false);
    }

    private async Task OpenPlaylistAsync(long playlistId)
    {
        await Page.GotoAsync($"{ServerUrl}/playlists/{playlistId}");
        await Page.WaitForSelectorAsync(HeaderSelector);
        // Let the (possibly huge) cover image load and render before measuring.
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(500);
    }

    /// <summary>
    /// The header height is fixed like on the series/movie pages: it does not depend on the cover image (tall, wide,
    /// tiny, none) and equals the height of a series page header at the same viewport.
    /// </summary>
    [Fact]
    public async Task Header_HasTheSameFixedHeight_ForExtremeCoverImages_AsOnTheSeriesPage()
    {
        if (SkipBrowser)
            return;

        await Page.SetViewportSizeAsync(1400, 900);
        var showId = await SeedTvShowWithSeasonsAsync("Vergleichsserie", ("Staffel 1", 2));
        var playlistId = await PrepareOwnerWithPlaylistAsync("Kopfhöhe");

        await Page.GotoAsync($"{ServerUrl}/tvshow/{showId}");
        await Page.WaitForSelectorAsync(".tvshow-header");
        await Page.WaitForTimeoutAsync(1500);
        var seriesHeight = await HeightOfAsync(".tvshow-header");

        // No image at all.
        await OpenPlaylistAsync(playlistId);
        var noImageHeight = await HeightOfAsync(HeaderSelector);
        Assert.True(Math.Abs(noImageHeight - seriesHeight) <= 1, $"Ohne Bild: {noImageHeight}px, Serienseite: {seriesHeight}px");

        foreach (var (name, width, height) in ExtremeImages)
        {
            await SetPlaylistCoverAsync("Kopfhöhe", TestImages.Png(width, height), isUserUploaded: true);
            await OpenPlaylistAsync(playlistId);
            await Expect(Page.Locator("#playlist-detail-cover-image")).ToBeVisibleAsync();

            var actual = await HeightOfAsync(HeaderSelector);
            Assert.True(Math.Abs(actual - seriesHeight) <= 1, $"Bild '{name}' ({width}x{height}): Kopfbereich {actual}px, Serienseite {seriesHeight}px");
            Assert.True(Math.Abs(actual - noImageHeight) <= 1, $"Bild '{name}' ({width}x{height}): Kopfbereich {actual}px, ohne Bild {noImageHeight}px");
        }
    }

    [Fact]
    public async Task Header_HeightDoesNotDependOnTheImage_AlsoOnASmallScreen()
    {
        if (SkipBrowser)
            return;

        await Page.SetViewportSizeAsync(400, 800);
        var playlistId = await PrepareOwnerWithPlaylistAsync("Kopfhöhe schmal");
        await OpenPlaylistAsync(playlistId);
        var noImageHeight = await HeightOfAsync(HeaderSelector);

        foreach (var (name, width, height) in ExtremeImages)
        {
            await SetPlaylistCoverAsync("Kopfhöhe schmal", TestImages.Png(width, height), isUserUploaded: true);
            await OpenPlaylistAsync(playlistId);

            var actual = await HeightOfAsync(HeaderSelector);
            Assert.True(Math.Abs(actual - noImageHeight) <= 1, $"Bild '{name}' ({width}x{height}): Kopfbereich {actual}px, ohne Bild {noImageHeight}px");
        }

        // Nothing sticks out horizontally on a phone-sized screen.
        var scrollWidth = await Page.EvaluateAsync<double>("document.documentElement.scrollWidth");
        Assert.True(scrollWidth <= 400 + 1, $"Horizontaler Überlauf: scrollWidth {scrollWidth}px bei 400px Breite");
    }

    /// <summary>
    /// The cover is dimmed exactly like on the series page (same overlay gradient), the action buttons sit ON the image
    /// inside the header (not above it) and the image covers the whole header.
    /// </summary>
    [Fact]
    public async Task Header_ImageIsDimmedLikeOnTheSeriesPage_AndActionButtonsSitOnTheImage()
    {
        if (SkipBrowser)
            return;

        await Page.SetViewportSizeAsync(1400, 900);
        var showId = await SeedTvShowWithSeasonsAsync("Vergleichsserie", ("Staffel 1", 1));
        // As administrator (and owner) all four action buttons plus the back arrow are present.
        await MakeUserAdminAsync(UserAEmail);
        var playlistId = await PrepareOwnerWithPlaylistAsync("Kopfbild");
        await SetPlaylistCoverAsync("Kopfbild", TestImages.Png(1600, 520), isUserUploaded: true);

        await Page.GotoAsync($"{ServerUrl}/tvshow/{showId}");
        await Page.WaitForSelectorAsync(".tvshow-header-overlay");
        await Page.WaitForTimeoutAsync(1000);
        var seriesOverlay = await Page.Locator(".tvshow-header-overlay").EvaluateAsync<string>("e => getComputedStyle(e).backgroundImage");
        var seriesMinHeight = await Page.Locator(".tvshow-header").EvaluateAsync<string>("e => getComputedStyle(e).minHeight");

        await OpenPlaylistAsync(playlistId);
        var playlistOverlay = await Page.Locator($"{HeaderSelector} .tvshow-header-overlay").EvaluateAsync<string>("e => getComputedStyle(e).backgroundImage");
        var playlistMinHeight = await Page.Locator(HeaderSelector).EvaluateAsync<string>("e => getComputedStyle(e).minHeight");

        // Same values as on the series page: the dimming gradient over the image and the height rule.
        Assert.Contains("linear-gradient", playlistOverlay);
        Assert.Equal(seriesOverlay, playlistOverlay);
        Assert.Equal(seriesMinHeight, playlistMinHeight);

        // The cover fills the whole header and is stacked below the overlay (the gradient dims it).
        var header = await Page.Locator(HeaderSelector).EvaluateAsync<System.Text.Json.JsonElement>("e => { const r = e.getBoundingClientRect(); return { x: r.x, y: r.y, w: r.width, h: r.height, r: r.right, b: r.bottom }; }");
        var cover = await Page.Locator("#playlist-detail-cover-image").EvaluateAsync<System.Text.Json.JsonElement>("e => { const r = e.getBoundingClientRect(); return { x: r.x, y: r.y, w: r.width, h: r.height }; }");
        Assert.True(Math.Abs(cover.GetProperty("w").GetDouble() - header.GetProperty("w").GetDouble()) <= 1);
        Assert.True(Math.Abs(cover.GetProperty("h").GetDouble() - header.GetProperty("h").GetDouble()) <= 1);
        var coverFit = await Page.Locator("#playlist-detail-cover-image").EvaluateAsync<string>("e => getComputedStyle(e).objectFit");
        Assert.Equal("cover", coverFit);

        // Every action button (and the back arrow) lies within the header rectangle = on the image.
        var buttons = Page.Locator($"{HeaderSelector} .metadata-action-bar button, {HeaderSelector} .back-arrow");
        Assert.True(await buttons.CountAsync() >= 5, "Aktionsbuttons und Zurück-Pfeil erwartet");
        var top = header.GetProperty("y").GetDouble();
        var bottom = header.GetProperty("b").GetDouble();
        var right = header.GetProperty("r").GetDouble();
        for (var i = 0; i < await buttons.CountAsync(); i++)
        {
            var box = await buttons.Nth(i).BoundingBoxAsync();
            Assert.NotNull(box);
            Assert.True(box!.Y >= top - 1 && box.Y + box.Height <= bottom + 1, $"Button {i} liegt nicht auf dem Bild (y={box.Y}, Kopfbereich {top}-{bottom})");
            Assert.True(box.X + box.Width <= right + 1, $"Button {i} ragt rechts aus dem Kopfbereich");
        }

        // Nothing but the header is above the image: the header is the first element of the page content.
        var actionBarParentIsHeader = await Page.EvaluateAsync<bool>("document.querySelector('.metadata-action-bar').closest('#playlist-detail-header') !== null");
        Assert.True(actionBarParentIsHeader);
    }

    [Fact]
    public async Task Dates_AreReadableAndClearlySeparated()
    {
        if (SkipBrowser)
            return;

        await Page.SetViewportSizeAsync(1400, 900);
        var playlistId = await PrepareOwnerWithPlaylistAsync("Datumsanzeige");
        await OpenPlaylistAsync(playlistId);

        var created = await Page.Locator("#playlist-detail-created").InnerTextAsync();
        var updated = await Page.Locator("#playlist-detail-updated").InnerTextAsync();
        Assert.Matches(new Regex(@"^Erstellt:\s+\d{2}\.\d{2}\.\d{4}, \d{2}:\d{2} Uhr$"), created);
        Assert.Matches(new Regex(@"^Aktualisiert:\s+\d{2}\.\d{2}\.\d{4}, \d{2}:\d{2} Uhr$"), updated);

        var createdBox = await Page.Locator("#playlist-detail-created").BoundingBoxAsync();
        var updatedBox = await Page.Locator("#playlist-detail-updated").BoundingBoxAsync();
        Assert.NotNull(createdBox);
        Assert.NotNull(updatedBox);
        var gap = updatedBox!.X - (createdBox!.X + createdBox.Width);
        Assert.True(gap >= 16, $"Zwischen den beiden Datumsangaben sind nur {gap}px Abstand");

        // Label and value are separated by a visible gap as well.
        var labelBox = await Page.Locator("#playlist-detail-created .playlist-detail-date-label").BoundingBoxAsync();
        var valueBox = await Page.Locator("#playlist-detail-created time").BoundingBoxAsync();
        Assert.NotNull(labelBox);
        Assert.NotNull(valueBox);
        Assert.True(valueBox!.X - (labelBox!.X + labelBox.Width) >= 2, "Beschriftung und Datum berühren sich");
    }

    /// <summary>
    /// The root cause of "no frame": the panels used colour variables that only exist inside the admin area, so the
    /// border and background were undefined elsewhere. Every overlay panel/dialog of the feature must now have a
    /// visible border and an opaque background.
    /// </summary>
    [Fact]
    public async Task EveryOverlayPanelOfThePlaylistFeature_HasAVisibleFrameAndOpaqueBackground()
    {
        if (SkipBrowser)
            return;

        await Page.SetViewportSizeAsync(1400, 900);
        var playlistId = await PrepareOwnerWithPlaylistAsync("Rahmen");
        await SetPlaylistCoverAsync("Rahmen", TestImages.Png(800, 300), isUserUploaded: true);
        await OpenPlaylistAsync(playlistId);

        // Bearbeiten
        await Page.ClickAsync(".playlist-detail-edit-button");
        await Page.WaitForSelectorAsync("#playlist-name-input");
        await AssertDialogHasFrameAsync("Bearbeiten");
        await Page.ClickAsync(".admin-dialog .btn-outline-light");

        // Löschen (Bestätigung)
        await Page.ClickAsync(".playlist-detail-delete-button");
        await Page.WaitForSelectorAsync("#confirm-delete-playlist-button");
        await AssertDialogHasFrameAsync("Löschen-Bestätigung");
        await Page.ClickAsync("#cancel-delete-playlist-button");

        // Genre-Editor
        await Page.ClickAsync("#playlist-detail-edit-genres-button");
        await Page.WaitForSelectorAsync("#playlist-genre-editor-save-button");
        await AssertDialogHasFrameAsync("Genre-Editor");
        await Page.ClickAsync("#playlist-genre-editor-cancel-button");

        // Sortiermodus-Bestätigung: to manual (no question), back again (question about losing the manual order)
        await Page.ClickAsync(".playlist-sortmode-toggle-button");
        await Expect(Page.Locator("#playlist-detail-sortmode")).ToHaveTextAsync("Manuell");
        await Page.ClickAsync(".playlist-sortmode-toggle-button");
        await Page.WaitForSelectorAsync("#confirm-sortmode-change-button");
        await AssertDialogHasFrameAsync("Sortiermodus-Bestätigung");
        await Page.ClickAsync("#cancel-sortmode-change-button");

        // Bild-Panel und darüber die Rückfrage zum Entfernen eines hochgeladenen Bildes
        await Page.ClickAsync("#playlist-detail-cover-button");
        await Page.WaitForSelectorAsync("#playlist-cover-panel");
        await AssertDialogHasFrameAsync("Bild-Panel", "#playlist-cover-panel");
        await Page.ClickAsync("#playlist-cover-remove-button");
        await Page.WaitForSelectorAsync("#confirm-remove-cover-button");
        await AssertDialogHasFrameAsync("Bild-Entfernen-Bestätigung", ".admin-dialog:has(#confirm-remove-cover-button)");
    }

    private async Task AssertDialogHasFrameAsync(string name, string selector = ".admin-dialog")
    {
        var style = await Page.Locator(selector).Last.EvaluateAsync<System.Text.Json.JsonElement>(
            "e => { const s = getComputedStyle(e); return { width: s.borderTopWidth, style: s.borderTopStyle, color: s.borderTopColor, bg: s.backgroundColor }; }");

        var width = double.Parse(style.GetProperty("width").GetString()!.Replace("px", string.Empty), System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(width >= 1, $"{name}: keine Rahmenbreite ({width}px)");
        Assert.NotEqual("none", style.GetProperty("style").GetString());
        Assert.NotEqual("rgba(0, 0, 0, 0)", style.GetProperty("color").GetString());
        // Opaque background: the page must not shine through.
        Assert.NotEqual("rgba(0, 0, 0, 0)", style.GetProperty("bg").GetString());
        Assert.DoesNotContain("transparent", style.GetProperty("bg").GetString());
    }

    /// <summary>
    /// The frame fix must not break the other page that uses the same dialog markup (administration of genres):
    /// its dialog keeps its frame and background.
    /// </summary>
    [Fact]
    public async Task GenreAdminDialog_StillHasItsFrame()
    {
        if (SkipBrowser)
            return;

        await MakeUserAdminAsync(UserAEmail);
        await LoginAsync(UserAEmail);
        await SeedGenreAsync("Testgenre");
        await Page.GotoAsync($"{ServerUrl}/admin/genres");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(1500);

        await Page.Locator(".admin-console button:has-text('Bearbeiten')").First.ClickAsync();
        await Page.WaitForSelectorAsync(".admin-dialog");

        await AssertDialogHasFrameAsync("Genre-Verwaltung");
    }

    /// <summary>
    /// A phone-sized screen: the buttons stay on the image and inside the viewport, the mode toggle fits the row.
    /// </summary>
    [Fact]
    public async Task NarrowScreen_KeepsHeaderButtonsAndModeToggleInsideTheViewport()
    {
        if (SkipBrowser)
            return;

        await Page.SetViewportSizeAsync(400, 800);
        await MakeUserAdminAsync(UserAEmail);
        var playlistId = await PrepareOwnerWithPlaylistAsync("Schmal");
        await OpenPlaylistAsync(playlistId);

        var buttons = Page.Locator($"{HeaderSelector} .metadata-action-bar button");
        Assert.True(await buttons.CountAsync() >= 4);
        for (var i = 0; i < await buttons.CountAsync(); i++)
        {
            var box = await buttons.Nth(i).BoundingBoxAsync();
            Assert.NotNull(box);
            Assert.True(box!.X >= 0 && box.X + box.Width <= 400, $"Button {i} liegt außerhalb (x={box.X}, Breite {box.Width})");
        }

        var group = await Page.Locator("#playlist-content-mode-group").BoundingBoxAsync();
        Assert.NotNull(group);
        Assert.True(group!.X + group.Width <= 400, $"Moduswechsel ragt aus dem Bildschirm (rechter Rand {group.X + group.Width}px)");
    }

    private async Task SeedGenreAsync(string name)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var source = await db.MediaSources.FirstOrDefaultAsync();
        if (source is null)
        {
            source = new MediaSource { Name = "Genre-Quelle", Host = "127.0.0.1", Port = 22, Path = "/g", Username = "u", Password = "p" };
            db.MediaSources.Add(source);
            await db.SaveChangesAsync();
        }

        db.Genres.Add(new Genre { Name = name, MediaSourceId = source.Id });
        await db.SaveChangesAsync();
    }
}
