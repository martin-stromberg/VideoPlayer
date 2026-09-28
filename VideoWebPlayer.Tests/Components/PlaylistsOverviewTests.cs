using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Components.Playlists;
using VideoWebPlayer.Controllers.Models;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Components;

/// <summary>
/// Kundenfeedback zu Entwicklungsschritt 11: the combined playlist overview (<see cref="PlaylistsList"/>) lists
/// the user's own playlists first and the public playlists of other users at the end (never twice), offers a
/// three-button icon filter bar (Alle / Eigene / Öffentliche) and an icon-only "Neue Playlist" button.
/// </summary>
public class PlaylistsOverviewTests
{
    private const string ForeignBadgeText = "Von einem anderen Benutzer freigegeben";

    private static readonly DtoPlaylist Private = new() { Id = 1, Name = "Privat", IsOwner = true, IsPublic = false };
    private static readonly DtoPlaylist OwnPublic = new() { Id = 2, Name = "Eigene öffentliche", IsOwner = true, IsPublic = true };
    private static readonly DtoPlaylist ForeignA = new() { Id = 7, Name = "Für alle", IsOwner = false, IsPublic = true, SortMode = PlaylistSortModeValues.ByReleaseDate, CoverPictureId = 5 };
    private static readonly DtoPlaylist ForeignB = new() { Id = 8, Name = "Noch eine", IsOwner = false, IsPublic = true, SortMode = PlaylistSortModeValues.Manual };

    [Fact]
    public void All_ListsOwnPlaylistsFirst_ThenForeignOnes_AndOwnPublicPlaylistOnlyOnce()
    {
        using var ctx = CreateContext(own: [Private, OwnPublic], publicPlaylists: [OwnPublic, ForeignA, ForeignB]);

        var cut = ctx.Render<PlaylistsList>();

        var names = cut.FindAll("a.playlist-row").Select(t => t.GetAttribute("data-playlist-name")!).ToArray();
        Assert.Equal(["Privat", "Eigene öffentliche", "Für alle", "Noch eine"], names);
    }

    [Fact]
    public void Tiles_ForeignSymbolOnlyOnForeignPlaylists_PublicBadgeOnlyOnOwnPublicPlaylists()
    {
        using var ctx = CreateContext(own: [Private, OwnPublic], publicPlaylists: [OwnPublic, ForeignA, ForeignB]);

        var cut = ctx.Render<PlaylistsList>();

        var privateTile = cut.Find("a.playlist-row[data-playlist-name='Privat']");
        var ownPublicTile = cut.Find("a.playlist-row[data-playlist-name='Eigene öffentliche']");
        var foreignTile = cut.Find("a.playlist-row[data-playlist-name='Für alle']");

        Assert.Empty(privateTile.QuerySelectorAll(".playlist-foreign-badge"));
        Assert.Empty(privateTile.QuerySelectorAll(".playlist-public-badge"));

        Assert.Empty(ownPublicTile.QuerySelectorAll(".playlist-foreign-badge"));
        Assert.Single(ownPublicTile.QuerySelectorAll(".playlist-public-badge"));

        var badge = Assert.Single(foreignTile.QuerySelectorAll(".playlist-foreign-badge"));
        Assert.Equal(ForeignBadgeText, badge.GetAttribute("title"));
        Assert.Equal(ForeignBadgeText, badge.GetAttribute("aria-label"));
        Assert.Empty(foreignTile.QuerySelectorAll(".playlist-public-badge"));
        Assert.Equal(2, cut.FindAll(".playlist-foreign-badge").Count);
    }

    /// <summary>
    /// Kundenrückmeldung zur Übersicht: the tile follows the film/series tiles - the image fills the whole tile
    /// and the title is the ONLY text, in the overlay at the bottom. No public-status wording, no created/updated
    /// dates, no sort-mode hint, no description and no genres.
    /// </summary>
    [Fact]
    public void Tile_ShowsOnlyTheTitle_NoDatesNoStatusTextNoDescriptionNoSortInfo()
    {
        var detailed = new DtoPlaylist
        {
            Id = 3,
            Name = "Mit allem",
            Description = "Eine Beschreibung, die nicht in die Kachel gehört",
            IsOwner = true,
            IsPublic = true,
            SortMode = PlaylistSortModeValues.Manual,
            CreatedAt = new DateTime(2026, 9, 7, 6, 23, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 9, 8, 21, 7, 0, DateTimeKind.Utc),
            Genres = [new DtoGenreOption { Id = 1, Name = "Action" }]
        };
        using var ctx = CreateContext(own: [detailed], publicPlaylists: []);

        var cut = ctx.Render<PlaylistsList>();

        var tile = cut.Find("a.playlist-row[data-playlist-name='Mit allem']");
        // Same structure as MediaBox: overlay at the bottom containing the title.
        var title = Assert.Single(tile.QuerySelectorAll(".media-card-overlay .media-titles .media-title-text"));
        Assert.Equal("Mit allem", title.TextContent.Trim());
        Assert.Equal("de", title.GetAttribute("lang"));

        Assert.Empty(tile.QuerySelectorAll(".playlist-card-dates"));
        Assert.Empty(tile.QuerySelectorAll(".playlist-card-meta"));
        Assert.Empty(tile.QuerySelectorAll(".media-subtitle-text"));
        Assert.Empty(tile.QuerySelectorAll(".playlist-card-genres"));
        Assert.Empty(tile.QuerySelectorAll(".playlist-sortmode-icon"));
        Assert.DoesNotContain("Erstellt", tile.TextContent);
        Assert.DoesNotContain("Aktualisiert", tile.TextContent);
        Assert.DoesNotContain("Action", tile.TextContent);
        Assert.DoesNotContain("Beschreibung", tile.TextContent);
        // "Öffentlich" only as the symbol's accessible name, never as visible tile text.
        Assert.Equal("Mit allem", tile.TextContent.Trim());
    }

    /// <summary>
    /// The public/foreign symbol sits in the top right corner of the tile (same shell for both), outside the
    /// title overlay at the bottom.
    /// </summary>
    [Fact]
    public void TileBadges_SitInTheTopRightCorner_AndAreSymbolsOnly()
    {
        using var ctx = CreateContext(own: [OwnPublic], publicPlaylists: [OwnPublic, ForeignA]);

        var cut = ctx.Render<PlaylistsList>();

        foreach (var name in new[] { "Eigene öffentliche", "Für alle" })
        {
            var tile = cut.Find($"a.playlist-row[data-playlist-name='{name}']");
            var badge = Assert.Single(tile.QuerySelectorAll(".playlist-tile-badge"));
            Assert.Equal("img", badge.GetAttribute("role"));
            Assert.False(string.IsNullOrWhiteSpace(badge.GetAttribute("title")));
            Assert.Equal(badge.GetAttribute("title"), badge.GetAttribute("aria-label"));
            Assert.Equal(string.Empty, badge.TextContent.Trim());
            Assert.Single(badge.QuerySelectorAll("svg"));
            // Not inside the bottom title overlay.
            Assert.Empty(tile.QuerySelectorAll(".media-card-overlay .playlist-tile-badge"));
        }
    }

    [Fact]
    public void ForeignTile_LinksToDetailView_AndShowsNoOwnerInformation()
    {
        using var ctx = CreateContext(own: [], publicPlaylists: [ForeignA]);

        var cut = ctx.Render<PlaylistsList>();

        var tile = cut.Find("a.playlist-row");
        Assert.Equal("/playlists/7", tile.GetAttribute("href"));
        Assert.Contains("/api/playlists/7/cover", cut.Find(".playlist-card-cover-image").GetAttribute("src"));
        Assert.DoesNotContain("Besitzer", cut.Markup, StringComparison.OrdinalIgnoreCase);
        // Nur lesend: keine Bearbeitungsmöglichkeit in der Übersicht.
        Assert.Empty(cut.FindAll(".playlist-detail-edit-button, .playlist-detail-delete-button"));
    }

    [Fact]
    public void FilterBar_HasThreeConnectedIconButtons_WithAccessibleNames_AndAllPreselected()
    {
        using var ctx = CreateContext(own: [Private], publicPlaylists: [ForeignA]);

        var cut = ctx.Render<PlaylistsList>();

        var group = cut.Find("#playlist-filter-group");
        Assert.Equal("group", group.GetAttribute("role"));
        Assert.False(string.IsNullOrWhiteSpace(group.GetAttribute("aria-label")));

        var buttons = group.QuerySelectorAll("button.playlist-filter-button").ToArray();
        Assert.Equal(3, buttons.Length);
        Assert.Equal(["all", "own", "public"], buttons.Select(b => b.GetAttribute("data-filter")!).ToArray());
        Assert.Equal(["Alle Playlists", "Eigene Playlists", "Öffentliche Playlists"], buttons.Select(b => b.GetAttribute("title")!).ToArray());
        Assert.Equal(["Alle Playlists", "Eigene Playlists", "Öffentliche Playlists"], buttons.Select(b => b.GetAttribute("aria-label")!).ToArray());
        // Reine Symbolbuttons: kein Textinhalt, dafür ein Symbol.
        Assert.All(buttons, b =>
        {
            Assert.Equal(string.Empty, b.TextContent.Trim());
            Assert.Single(b.QuerySelectorAll("svg"));
        });

        Assert.Equal(["true", "false", "false"], buttons.Select(b => b.GetAttribute("aria-pressed")!).ToArray());
        Assert.Single(cut.FindAll(".playlist-filter-button.active"));
        Assert.Contains("active", cut.Find("#playlist-filter-all").ClassList);
    }

    [Fact]
    public void FilterBar_SwitchingFilters_ChangesTheListAndTheActiveButton()
    {
        using var ctx = CreateContext(own: [Private, OwnPublic], publicPlaylists: [OwnPublic, ForeignA, ForeignB]);
        var cut = ctx.Render<PlaylistsList>();

        cut.Find("#playlist-filter-own").Click();
        Assert.Equal(["Privat", "Eigene öffentliche"], TileNames(cut));
        Assert.Equal("true", cut.Find("#playlist-filter-own").GetAttribute("aria-pressed"));
        Assert.Equal("false", cut.Find("#playlist-filter-all").GetAttribute("aria-pressed"));
        Assert.Contains("active", cut.Find("#playlist-filter-own").ClassList);
        Assert.DoesNotContain("active", cut.Find("#playlist-filter-all").ClassList);
        Assert.Empty(cut.FindAll(".playlist-foreign-badge"));

        // "Öffentliche" = alle öffentlichen Playlists: fremde UND eigene öffentliche, jede genau einmal.
        cut.Find("#playlist-filter-public").Click();
        Assert.Equal(["Eigene öffentliche", "Für alle", "Noch eine"], TileNames(cut));
        Assert.Equal("true", cut.Find("#playlist-filter-public").GetAttribute("aria-pressed"));
        Assert.Single(cut.FindAll(".playlist-filter-button.active"));

        cut.Find("#playlist-filter-all").Click();
        Assert.Equal(["Privat", "Eigene öffentliche", "Für alle", "Noch eine"], TileNames(cut));
        Assert.Equal("true", cut.Find("#playlist-filter-all").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void PublicAliasRoute_PreselectsThePublicFilter()
    {
        using var ctx = CreateContext(own: [Private, OwnPublic], publicPlaylists: [OwnPublic, ForeignA]);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/playlists/public");

        var cut = ctx.Render<PlaylistsList>();

        Assert.Equal("true", cut.Find("#playlist-filter-public").GetAttribute("aria-pressed"));
        Assert.Equal(["Eigene öffentliche", "Für alle"], TileNames(cut));
    }

    /// <summary>
    /// Regression: clicking the "Playlists" menu entry while the alias route "/playlists/public" is open
    /// navigates to "/playlists" but reuses the same component instance, which used to keep the "Öffentliche"
    /// filter active (the route was only evaluated once, on initialization).
    /// </summary>
    [Fact]
    public void NavigatingFromPublicAliasToPlaylists_ResetsTheFilterToAll_AndBack()
    {
        using var ctx = CreateContext(own: [Private, OwnPublic], publicPlaylists: [OwnPublic, ForeignA]);
        var navigation = ctx.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/playlists/public");
        var cut = ctx.Render<PlaylistsList>();
        Assert.Equal("true", cut.Find("#playlist-filter-public").GetAttribute("aria-pressed"));

        cut.InvokeAsync(() => navigation.NavigateTo("/playlists"));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("true", cut.Find("#playlist-filter-all").GetAttribute("aria-pressed"));
            Assert.Equal("false", cut.Find("#playlist-filter-public").GetAttribute("aria-pressed"));
        });
        Assert.Equal(["Privat", "Eigene öffentliche", "Für alle"], TileNames(cut));

        cut.InvokeAsync(() => navigation.NavigateTo("/playlists/public"));

        cut.WaitForAssertion(() =>
            Assert.Equal("true", cut.Find("#playlist-filter-public").GetAttribute("aria-pressed")));
    }

    [Fact]
    public void NavigatingToADetailPage_KeepsTheSelectedFilter()
    {
        using var ctx = CreateContext(own: [Private], publicPlaylists: [ForeignA]);
        var navigation = ctx.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/playlists");
        var cut = ctx.Render<PlaylistsList>();
        cut.Find("#playlist-filter-own").Click();

        cut.InvokeAsync(() => navigation.NavigateTo("/playlists/5"));

        Assert.Equal("true", cut.Find("#playlist-filter-own").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void OwnRoute_PreselectsTheAllFilter()
    {
        using var ctx = CreateContext(own: [Private], publicPlaylists: [ForeignA]);
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/playlists");

        var cut = ctx.Render<PlaylistsList>();

        Assert.Equal("true", cut.Find("#playlist-filter-all").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void CreateButton_IsAnIconWithAccessibleName_AndOpensTheForm()
    {
        using var ctx = CreateContext(own: [Private], publicPlaylists: []);
        var cut = ctx.Render<PlaylistsList>();

        var button = cut.Find("#create-playlist-button");

        Assert.Equal("Neue Playlist", button.GetAttribute("title"));
        Assert.Equal("Neue Playlist", button.GetAttribute("aria-label"));
        Assert.Equal(string.Empty, button.TextContent.Trim());
        Assert.Single(button.QuerySelectorAll("svg"));
        Assert.DoesNotContain("Neue Playlist erstellen", cut.Markup);

        button.Click();
        Assert.Single(cut.FindComponents<PlaylistForm>());
    }

    [Fact]
    public void OverviewOffersNoSeparatePublicOverviewLinkOrButton()
    {
        using var ctx = CreateContext(own: [Private], publicPlaylists: [ForeignA]);

        var cut = ctx.Render<PlaylistsList>();

        Assert.Empty(cut.FindAll("#public-playlists-link"));
        Assert.Empty(cut.FindAll("#own-playlists-link"));
        Assert.Empty(cut.FindAll("a[href='/playlists/public']"));
    }

    [Theory]
    [InlineData("all", "Keine Playlists vorhanden")]
    [InlineData("own", "Keine eigenen Playlists vorhanden")]
    [InlineData("public", "Keine öffentlichen Playlists vorhanden")]
    public void EmptyStates_AreShownPerFilter(string filter, string expectedTitle)
    {
        using var ctx = CreateContext(own: [], publicPlaylists: []);
        var cut = ctx.Render<PlaylistsList>();

        cut.Find($"#playlist-filter-{filter}").Click();

        Assert.Equal(expectedTitle, cut.Find("#playlists-empty strong").TextContent.Trim());
        Assert.Empty(cut.FindAll("a.playlist-row"));
    }

    [Fact]
    public void EmptyState_InOwnFilter_WhenOnlyForeignPlaylistsExist_IsShown()
    {
        using var ctx = CreateContext(own: [], publicPlaylists: [ForeignA]);
        var cut = ctx.Render<PlaylistsList>();
        Assert.Single(cut.FindAll("a.playlist-row"));

        cut.Find("#playlist-filter-own").Click();

        Assert.Empty(cut.FindAll("a.playlist-row"));
        Assert.Single(cut.FindAll("#playlists-empty"));
    }

    [Fact]
    public void LoadFailure_ShowsError()
    {
        var mock = new Mock<IPlaylistApiClient>();
        mock.Setup(c => c.RequestPlaylistsAsync(null)).ReturnsAsync(Array.Empty<DtoPlaylist>());
        mock.Setup(c => c.RequestPublicPlaylistsAsync(null)).ThrowsAsync(new HttpRequestException("boom"));
        using var ctx = CreateContext(mock);

        var cut = ctx.Render<PlaylistsList>();

        Assert.Single(cut.FindAll("#playlists-load-error"));
    }

    [Fact]
    public void GenreFilter_IsAppliedToBothQueries_AndCombinesWithTheFilterBar()
    {
        var mock = new Mock<IPlaylistApiClient>();
        mock.Setup(c => c.RequestPlaylistsAsync(null)).ReturnsAsync(new[] { Private, OwnPublic });
        mock.Setup(c => c.RequestPublicPlaylistsAsync(null)).ReturnsAsync(new[] { OwnPublic, ForeignA, ForeignB });
        mock.Setup(c => c.RequestPlaylistsAsync(5)).ReturnsAsync(new[] { OwnPublic });
        mock.Setup(c => c.RequestPublicPlaylistsAsync(5)).ReturnsAsync(new[] { OwnPublic, ForeignB });
        using var ctx = CreateContext(mock, withGenres: true);
        var cut = ctx.Render<PlaylistsList>();
        Assert.Equal(4, cut.FindAll("a.playlist-row").Count);

        cut.Find("#playlists-genre-filter").Change("5");

        Assert.Equal(["Eigene öffentliche", "Noch eine"], TileNames(cut));
        mock.Verify(c => c.RequestPlaylistsAsync(5), Times.Once);
        mock.Verify(c => c.RequestPublicPlaylistsAsync(5), Times.Once);

        cut.Find("#playlist-filter-own").Click();
        Assert.Equal(["Eigene öffentliche"], TileNames(cut));
    }

    private static string[] TileNames(IRenderedComponent<PlaylistsList> cut)
        => cut.FindAll("a.playlist-row").Select(t => t.GetAttribute("data-playlist-name")!).ToArray();

    private static global::Bunit.BunitContext CreateContext(DtoPlaylist[] own, DtoPlaylist[] publicPlaylists)
    {
        var mock = new Mock<IPlaylistApiClient>();
        mock.Setup(c => c.RequestPlaylistsAsync(null)).ReturnsAsync(own);
        mock.Setup(c => c.RequestPublicPlaylistsAsync(null)).ReturnsAsync(publicPlaylists);
        return CreateContext(mock);
    }

    private static global::Bunit.BunitContext CreateContext(Mock<IPlaylistApiClient> playlistClientMock, bool withGenres = false)
    {
        var ctx = new global::Bunit.BunitContext();
        ctx.AddAuthorization().SetAuthorized("test-user");
        ctx.Services.AddSingleton<VideoWebPlayerClient>(withGenres ? new GenreVideoWebPlayerClient() : new NoOpVideoWebPlayerClient());
        ctx.Services.AddSingleton(playlistClientMock.Object);
        return ctx;
    }

    /// <summary>
    /// <see cref="VideoWebPlayerClient"/> stand-in returning a single genre option (id 5) for the genre catalog.
    /// </summary>
    private sealed class GenreVideoWebPlayerClient : VideoWebPlayerClient
    {
        public GenreVideoWebPlayerClient() : base(new HttpClient(), NullLogger<VideoWebPlayerClient>.Instance)
        {
        }

        protected override Task<T> HttpGetAsync<T>(string endPoint)
            => HttpGetAsync<T>(endPoint, CancellationToken.None);

        protected override Task<T> HttpGetAsync<T>(string endPoint, CancellationToken cancellationToken)
        {
            object result = typeof(T) == typeof(List<DtoGenreOption>)
                ? new List<DtoGenreOption> { new() { Id = 5, Name = "Action" } }
                : new List<MediaEntryDto>();
            return Task.FromResult((T)result);
        }
    }
}
