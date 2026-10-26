using Bunit;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Components;

/// <summary>
/// Der Live-TV-Eintrag der Hauptnavigation (Schritt 7 des TV-Senders): genau ein Link auf
/// <c>/live-tv</c> mit verständlicher Beschriftung für angemeldete Benutzer.
/// </summary>
public class NavMenuLiveTvTests
{
    [Fact]
    public void NavMenu_ContainsExactlyOneLiveTvEntry_ForAuthorizedUser()
    {
        using var ctx = NavMenuTestContext.Create(userId: "test-user-id");
        ctx.Db.Setups.Add(new Setup { TvSenderEnabled = true });
        ctx.Db.TvSenderUserAccess.Add(new TvSenderUserAccess { UserId = "test-user-id" });
        ctx.Db.SaveChanges();

        var cut = ctx.RenderNavMenu();

        var links = cut.FindAll("a.nav-link").Where(a => a.GetAttribute("href")?.Equals("/live-tv", StringComparison.OrdinalIgnoreCase) == true).ToArray();
        var link = Assert.Single(links);
        Assert.Equal("Live-TV", link.TextContent.Trim());
    }

    [Fact]
    public void NavMenu_HidesLiveTvEntry_WhenFeatureNotReleased()
    {
        using var ctx = NavMenuTestContext.Create(userId: "test-user-id");
        ctx.Db.Setups.Add(new Setup { TvSenderEnabled = false });
        ctx.Db.SaveChanges();

        var cut = ctx.RenderNavMenu();

        Assert.DoesNotContain(cut.FindAll("a.nav-link"), a => a.GetAttribute("href")?.Equals("/live-tv", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void NavMenu_HidesLiveTvEntry_WhenUserNotReleased()
    {
        using var ctx = NavMenuTestContext.Create(userId: "test-user-id");
        ctx.Db.Setups.Add(new Setup { TvSenderEnabled = true });
        ctx.Db.SaveChanges();

        var cut = ctx.RenderNavMenu();

        Assert.DoesNotContain(cut.FindAll("a.nav-link"), a => a.GetAttribute("href")?.Equals("/live-tv", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void NavMenu_ShowsLiveTvEntry_ForAdminWithoutAccessRow()
    {
        using var ctx = NavMenuTestContext.Create(isAdmin: true);
        ctx.Db.Setups.Add(new Setup { TvSenderEnabled = true });
        ctx.Db.SaveChanges();

        var cut = ctx.RenderNavMenu();

        Assert.Contains(cut.FindAll("a.nav-link"), a => a.GetAttribute("href")?.Equals("/live-tv", StringComparison.OrdinalIgnoreCase) == true);
    }
}
