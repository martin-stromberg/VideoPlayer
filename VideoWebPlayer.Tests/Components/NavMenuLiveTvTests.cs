using Bunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VideoWebPlayer.Client;
using VideoWebPlayer.Components.Layout;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
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
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        using var db = new ApplicationDbContext(options, new EventManager());

        using var ctx = new global::Bunit.BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.AddAuthorization().SetAuthorized("test-user");
        ctx.Services.AddSingleton(db);
        ctx.Services.AddSingleton<VideoWebPlayerClient>(new NoOpVideoWebPlayerClient());
        ctx.Services.AddSingleton(new Mock<ILoginIpBlockService>().Object);

        var cut = ctx.Render<NavMenu>();

        var links = cut.FindAll("a.nav-link").Where(a => a.GetAttribute("href")?.Equals("/live-tv", StringComparison.OrdinalIgnoreCase) == true).ToArray();
        var link = Assert.Single(links);
        Assert.Equal("Live-TV", link.TextContent.Trim());
    }
}
