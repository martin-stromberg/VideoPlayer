using Bunit;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Components.Layout;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Components;

/// <summary>
/// Merkmal <c>HiddenInMenu</c> an <see cref="MediaSource"/>: das <c>NavMenu</c> filtert versteckte
/// Quellen beim Befüllen seiner Quellenliste — für Admins und Nicht-Admins gleichermaßen. Bleiben
/// keine sichtbaren Quellen übrig, verschwindet auch die Überschrift „Quellen".
/// </summary>
public class NavMenuHiddenSourceTests
{
    [Fact]
    public void NavMenu_HidesSourceEntry_WhenHiddenInMenu()
    {
        using var ctx = NavMenuTestContext.Create(sources:
        [
            new DtoMediaSource { Id = 1, Name = "Sichtbare Quelle" },
            new DtoMediaSource { Id = 2, Name = "Clip-Quelle", HiddenInMenu = true }
        ]);

        var cut = ctx.RenderNavMenu();

        var hrefs = SourceHrefs(cut);
        Assert.Contains("/mediasource/1", hrefs);
        Assert.DoesNotContain("/mediasource/2", hrefs);
        Assert.DoesNotContain("Clip-Quelle", cut.Markup);
    }

    [Fact]
    public void NavMenu_ShowsSourceEntry_WhenNotHidden()
    {
        using var ctx = NavMenuTestContext.Create(sources:
        [
            new DtoMediaSource { Id = 1, Name = "Sichtbare Quelle" }
        ]);

        var cut = ctx.RenderNavMenu();

        var hrefs = SourceHrefs(cut);
        Assert.Contains("/mediasource/1", hrefs);
        Assert.Contains("Quellen", cut.Markup);
    }

    [Fact]
    public void NavMenu_HidesSourcesSection_WhenAllSourcesHidden()
    {
        using var ctx = NavMenuTestContext.Create(sources:
        [
            new DtoMediaSource { Id = 1, Name = "Clip-Quelle Eins", HiddenInMenu = true },
            new DtoMediaSource { Id = 2, Name = "Clip-Quelle Zwei", HiddenInMenu = true }
        ]);

        var cut = ctx.RenderNavMenu();

        Assert.Empty(SourceHrefs(cut));
        // Ohne sichtbare Quellen darf auch die Sektions-Überschrift nicht gerendert werden.
        Assert.DoesNotContain(cut.FindAll("span.nav-link"), s => s.TextContent.Trim() == "Quellen");
    }

    [Fact]
    public void NavMenu_HidesHiddenSource_ForAdmin()
    {
        using var ctx = NavMenuTestContext.Create(isAdmin: true, sources:
        [
            new DtoMediaSource { Id = 1, Name = "Sichtbare Quelle" },
            new DtoMediaSource { Id = 2, Name = "Clip-Quelle", HiddenInMenu = true }
        ]);

        var cut = ctx.RenderNavMenu();

        var hrefs = SourceHrefs(cut);
        Assert.Contains("/mediasource/1", hrefs);
        Assert.DoesNotContain("/mediasource/2", hrefs);
    }

    private static IReadOnlyList<string> SourceHrefs(IRenderedComponent<NavMenu> cut)
        => cut.FindAll("a.nav-link")
            .Select(a => a.GetAttribute("href"))
            .Where(h => h?.StartsWith("/mediasource/", StringComparison.OrdinalIgnoreCase) == true)
            .Select(h => h!)
            .ToList();
}
