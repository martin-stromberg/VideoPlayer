using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// End-to-end tests (real browser) for the combined playlist overview (Kundenfeedback zu Entwicklungsschritt 11):
/// own playlists first, foreign public playlists at the end with a symbol in the top right corner, own public
/// playlists listed only once, a three-button icon filter bar and an icon-only "Neue Playlist" button.
/// </summary>
[Trait("Category", "E2E")]
public sealed class PlaylistsOverviewE2ETests : PlaylistsE2ETestBase
{
    private const string ForeignBadgeText = "Von einem anderen Benutzer freigegeben";

    [Fact]
    public async Task Overview_ListsOwnFirstThenForeignPublic_WithoutDuplicates_AndFilterBar()
    {
        if (SkipBrowser)
            return;

        // Benutzer A: eine private und eine öffentliche Playlist.
        await LoginAsync(UserAEmail);
        await CreatePlaylistViaUiAsync("A-Privat");
        await CreatePlaylistViaUiAsync("A-Öffentlich");
        await MakePlaylistPublicAsync("A-Öffentlich");

        // Benutzer B: eine private und eine (eigene) öffentliche Playlist.
        await LoginAsync(UserBEmail);
        await CreatePlaylistViaUiAsync("B-Eigene");
        await CreatePlaylistViaUiAsync("B-Öffentlich");
        await MakePlaylistPublicAsync("B-Öffentlich");

        await OpenOverviewAsync("/playlists");

        // Reihenfolge: eigene zuerst, fremde öffentliche am Ende; die fremde private Playlist fehlt, die eigene
        // öffentliche Playlist steht genau einmal (als eigene) in der Liste.
        await AssertTilesAsync(["B-Eigene", "B-Öffentlich", "A-Öffentlich"]);
        await Expect(Page.Locator(".playlist-row[data-playlist-name='B-Öffentlich']")).ToHaveCountAsync(1);
        await Expect(Page.Locator(".playlist-row[data-playlist-name='A-Privat']")).ToHaveCountAsync(0);

        // Fremd-Symbol nur an der fremden Kachel, und zwar in deren rechter oberer Ecke.
        await Expect(Page.Locator(".playlist-foreign-badge")).ToHaveCountAsync(1);
        var foreignTile = Page.Locator(".playlist-row[data-playlist-name='A-Öffentlich']");
        var badge = foreignTile.Locator(".playlist-foreign-badge");
        await Expect(badge).ToHaveAttributeAsync("title", ForeignBadgeText);
        await Expect(badge).ToHaveAttributeAsync("aria-label", ForeignBadgeText);
        var tileBox = (await foreignTile.Locator(".playlist-card").BoundingBoxAsync())!;
        var badgeBox = (await badge.BoundingBoxAsync())!;
        Assert.True(badgeBox.X + badgeBox.Width > tileBox.X + tileBox.Width - 24, "Fremd-Symbol sitzt nicht rechts.");
        Assert.True(badgeBox.Y < tileBox.Y + 24, "Fremd-Symbol sitzt nicht oben.");
        await Expect(Page.Locator(".playlist-row[data-playlist-name='B-Öffentlich'] .playlist-public-badge")).ToHaveCountAsync(1);

        // Filterleiste: Alle (vorgewählt) / Eigene / Öffentliche.
        await Expect(Page.Locator("#playlist-filter-all")).ToHaveAttributeAsync("aria-pressed", "true");

        await Page.ClickAsync("#playlist-filter-own");
        await AssertTilesAsync(["B-Eigene", "B-Öffentlich"]);
        await Expect(Page.Locator("#playlist-filter-own")).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(Page.Locator("#playlist-filter-all")).ToHaveAttributeAsync("aria-pressed", "false");

        await Page.ClickAsync("#playlist-filter-public");
        await AssertTilesAsync(["B-Öffentlich", "A-Öffentlich"]);
        await Expect(Page.Locator("#playlist-filter-public")).ToHaveAttributeAsync("aria-pressed", "true");

        await Page.ClickAsync("#playlist-filter-all");
        await AssertTilesAsync(["B-Eigene", "B-Öffentlich", "A-Öffentlich"]);
    }

    [Fact]
    public async Task ForeignPublicPlaylist_OpensReadOnlyDetail_AndOldPublicRouteStillWorks()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        await CreatePlaylistViaUiAsync("A-Für-Alle");
        await MakePlaylistPublicAsync("A-Für-Alle");

        await LoginAsync(UserBEmail);
        await OpenOverviewAsync("/playlists/public");

        // Die frühere Route zeigt die zusammengefasste Übersicht mit vorgewähltem Filter "Öffentliche".
        await Expect(Page.Locator("#playlist-filter-public")).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(Page.Locator("h1")).ToHaveTextAsync("Playlists");

        await Page.Locator(".playlist-row[data-playlist-name='A-Für-Alle']").ClickAsync();
        await Page.WaitForSelectorAsync("#playlist-detail-name");
        await Expect(Page.Locator("#playlist-detail-name")).ToHaveTextAsync("A-Für-Alle");
        await Expect(Page.Locator(".playlist-detail-edit-button")).ToHaveCountAsync(0);
        await Expect(Page.Locator(".playlist-detail-delete-button")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Menu_HasOnePlaylistEntry_AndCreateButtonIsAnIconWithAccessibleName()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        await OpenOverviewAsync("/playlists");

        await Expect(Page.Locator("a.nav-link[href='/playlists']")).ToHaveCountAsync(1);
        await Expect(Page.Locator("a.nav-link[href='/playlists/public']")).ToHaveCountAsync(0);

        var create = Page.Locator("#create-playlist-button");
        await Expect(create).ToHaveAttributeAsync("title", "Neue Playlist");
        await Expect(create).ToHaveAttributeAsync("aria-label", "Neue Playlist");
        await Expect(create).ToHaveTextAsync(string.Empty);
        await Expect(create.Locator("svg")).ToHaveCountAsync(1);
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Neue Playlist" })).ToHaveCountAsync(1);

        // Die Filterbuttons sind über ihren zugänglichen Namen erreichbar.
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Öffentliche Playlists" })).ToHaveCountAsync(1);
    }

    private async Task OpenOverviewAsync(string path)
    {
        await Page.GotoAsync($"{ServerUrl}{path}");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForSelectorAsync("#playlist-filter-group");
        await Page.WaitForTimeoutAsync(1500);
    }

    /// <summary>
    /// Waits until the overview shows the expected tiles in the expected order (a filter switch is a Blazor Server
    /// round trip, so the list changes asynchronously after the click) and fails with the last seen names otherwise.
    /// </summary>
    /// <param name="expectedNames">The playlist names expected in the overview, in display order.</param>
    private async Task AssertTilesAsync(params string[] expectedNames)
    {
        string[] names = [];
        for (var attempt = 0; attempt < 50; attempt++)
        {
            names = await Page.Locator("a.playlist-row").EvaluateAllAsync<string[]>("els => els.map(e => e.getAttribute('data-playlist-name'))");
            if (names.SequenceEqual(expectedNames))
                return;

            await Page.WaitForTimeoutAsync(100);
        }

        Assert.Equal(expectedNames, names);
    }
}
