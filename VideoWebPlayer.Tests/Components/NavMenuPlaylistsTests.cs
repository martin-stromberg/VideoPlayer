using Bunit;
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
        using var ctx = NavMenuTestContext.Create();

        var cut = ctx.RenderNavMenu();

        var playlistLinks = cut.FindAll("a.nav-link").Where(a => a.GetAttribute("href")?.StartsWith("/playlists", StringComparison.OrdinalIgnoreCase) == true).ToArray();
        var link = Assert.Single(playlistLinks);
        Assert.Equal("/playlists", link.GetAttribute("href"));
        Assert.Equal("Playlists", link.TextContent.Trim());
        Assert.DoesNotContain("Öffentliche Playlists", cut.Markup);
        Assert.DoesNotContain("/playlists/public", cut.Markup);
    }
}
