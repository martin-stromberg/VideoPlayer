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
/// Kundenfeedback zu Entwicklungsschritt 11: the two menu items "Playlists" and "Öffentliche Playlists" were merged
/// into a single "Playlists" entry (the public playlists are part of the combined overview).
/// </summary>
public class NavMenuPlaylistsTests
{
    [Fact]
    public void NavMenu_ContainsExactlyOnePlaylistEntry_AndNoSeparatePublicPlaylistsEntry()
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

        var playlistLinks = cut.FindAll("a.nav-link").Where(a => a.GetAttribute("href")?.StartsWith("/playlists", StringComparison.OrdinalIgnoreCase) == true).ToArray();
        var link = Assert.Single(playlistLinks);
        Assert.Equal("/playlists", link.GetAttribute("href"));
        Assert.Equal("Playlists", link.TextContent.Trim());
        Assert.DoesNotContain("Öffentliche Playlists", cut.Markup);
        Assert.DoesNotContain("/playlists/public", cut.Markup);
    }
}
