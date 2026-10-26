using Bunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Components.Layout;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
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
        using var ctx = CreateContext(isAdmin: false,
            new DtoMediaSource { Id = 1, Name = "Sichtbare Quelle" },
            new DtoMediaSource { Id = 2, Name = "Clip-Quelle", HiddenInMenu = true });

        var cut = ctx.Render<NavMenu>();

        var hrefs = SourceHrefs(cut);
        Assert.Contains("/mediasource/1", hrefs);
        Assert.DoesNotContain("/mediasource/2", hrefs);
        Assert.DoesNotContain("Clip-Quelle", cut.Markup);
    }

    [Fact]
    public void NavMenu_ShowsSourceEntry_WhenNotHidden()
    {
        using var ctx = CreateContext(isAdmin: false,
            new DtoMediaSource { Id = 1, Name = "Sichtbare Quelle" });

        var cut = ctx.Render<NavMenu>();

        var hrefs = SourceHrefs(cut);
        Assert.Contains("/mediasource/1", hrefs);
        Assert.Contains("Quellen", cut.Markup);
    }

    [Fact]
    public void NavMenu_HidesSourcesSection_WhenAllSourcesHidden()
    {
        using var ctx = CreateContext(isAdmin: false,
            new DtoMediaSource { Id = 1, Name = "Clip-Quelle Eins", HiddenInMenu = true },
            new DtoMediaSource { Id = 2, Name = "Clip-Quelle Zwei", HiddenInMenu = true });

        var cut = ctx.Render<NavMenu>();

        Assert.Empty(SourceHrefs(cut));
        // Ohne sichtbare Quellen darf auch die Sektions-Überschrift nicht gerendert werden.
        Assert.DoesNotContain(cut.FindAll("span.nav-link"), s => s.TextContent.Trim() == "Quellen");
    }

    [Fact]
    public void NavMenu_HidesHiddenSource_ForAdmin()
    {
        using var ctx = CreateContext(isAdmin: true,
            new DtoMediaSource { Id = 1, Name = "Sichtbare Quelle" },
            new DtoMediaSource { Id = 2, Name = "Clip-Quelle", HiddenInMenu = true });

        var cut = ctx.Render<NavMenu>();

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

    private static BunitContext CreateContext(bool isAdmin, params DtoMediaSource[] sources)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var db = new ApplicationDbContext(options, new EventManager());

        var ctx = new global::Bunit.BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var auth = ctx.AddAuthorization().SetAuthorized(isAdmin ? "admin-user" : "test-user");
        if (isAdmin)
            auth.SetClaims(new System.Security.Claims.Claim("IsAdmin", "True"));
        ctx.Services.AddSingleton(db);
        ctx.Services.AddSingleton(new ProgramSettingsService(db, Microsoft.Extensions.Logging.Abstractions.NullLogger<ProgramSettingsService>.Instance));
        ctx.Services.AddSingleton<VideoWebPlayerClient>(new NoOpVideoWebPlayerClient(sources));
        ctx.Services.AddSingleton(new Mock<ILoginIpBlockService>().Object);
        return ctx;
    }
}
