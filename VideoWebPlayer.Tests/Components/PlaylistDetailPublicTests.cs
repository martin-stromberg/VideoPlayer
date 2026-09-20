using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Components.Playlists;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Components;

/// <summary>
/// Entwicklungsschritt 11: <see cref="PlaylistDetail"/> for public playlists. A viewer who is not the owner
/// (regular user, or administrator who is not the owner) gets a strictly read-only view in which NONE of the
/// editing affordances is rendered at all (each checked individually) while playing stays possible; the
/// "oeffentlich" toggle is rendered only for an administrator who owns the playlist. (The server refuses
/// the corresponding mutations independently - see the controller access-matrix tests.)
/// </summary>
public class PlaylistDetailPublicTests
{
    /// <summary>Every editing affordance of the detail view: name (for the failure message) and CSS selector.</summary>
    /// <returns>Theory data: name and selector.</returns>
    public static IEnumerable<object[]> EditingAffordances() => new[]
    {
        new object[] { "Aktionsleiste", ".metadata-action-bar" },
        new object[] { "Bild hochladen", "#playlist-detail-upload-cover-button" },
        new object[] { "Cover neu erzeugen", "#playlist-detail-regenerate-cover-button" },
        new object[] { "Bearbeiten", ".playlist-detail-edit-button" },
        new object[] { "Löschen", ".playlist-detail-delete-button" },
        new object[] { "Öffentlich-Umschalter", "#playlist-detail-toggle-public-button" },
        new object[] { "Sortiermodus-Wechsel", ".playlist-sortmode-toggle-button" },
        new object[] { "Genres bearbeiten", "#playlist-detail-edit-genres-button" },
        new object[] { "Genres zurücksetzen", "#playlist-detail-reset-genres-button" },
        new object[] { "Eintrag entfernen", ".playlist-entry-remove-button" },
        new object[] { "An Anfang", ".playlist-entry-move-start-button" },
        new object[] { "An Ende", ".playlist-entry-move-end-button" },
        new object[] { "Drag & Drop", ".playlist-entry-row[draggable=true]" },
        new object[] { "Drag&Drop-Hinweis", ".playlist-entries-draganddrop-hint" },
        new object[] { "Suchfeld zum Hinzufügen", ".admin-actions" },
    };

    [Theory]
    [MemberData(nameof(EditingAffordances))]
    public void Viewer_RegularUser_OfPublicPlaylist_RendersNoEditingAffordance(string name, string selector)
    {
        // Even if a (misbehaving) server reported "manually overridden genres" and manual mode, nothing
        // editable appears for a non-owner.
        using var ctx = CreateContext(CreatePlaylist(isOwner: false, isPublic: true), isAdmin: false);

        var cut = RenderDetail(ctx);

        Assert.True(cut.FindAll(selector).Count == 0, $"'{name}' ({selector}) must not be rendered for a non-owner.");
        Assert.Empty(cut.FindComponents<MediaSearchSelector>());
    }

    [Theory]
    [MemberData(nameof(EditingAffordances))]
    public void Viewer_AdminWhoIsNotOwner_RendersNoEditingAffordance(string name, string selector)
    {
        using var ctx = CreateContext(CreatePlaylist(isOwner: false, isPublic: true), isAdmin: true);

        var cut = RenderDetail(ctx);

        Assert.True(cut.FindAll(selector).Count == 0, $"'{name}' ({selector}) must not be rendered for an administrator who is not the owner.");
        Assert.Empty(cut.FindComponents<MediaSearchSelector>());
    }

    [Theory]
    [MemberData(nameof(EditingAffordances))]
    public void Owner_Admin_RendersEveryEditingAffordance_PositiveControlForTheSelectors(string name, string selector)
    {
        // Guards the two tests above against vacuously passing on a wrong selector: for the owner (an
        // administrator, manual mode, manually overridden genres) each selector must match.
        using var ctx = CreateContext(CreatePlaylist(isOwner: true, isPublic: true), isAdmin: true);

        var cut = RenderDetail(ctx);

        Assert.True(cut.FindAll(selector).Count > 0, $"'{name}' ({selector}) should be rendered for the owner.");
    }

    [Fact]
    public void Viewer_CanPlayAccessibleEntries_LockedEntriesAreGreyedOutAndNotPlayable()
    {
        using var ctx = CreateContext(CreatePlaylist(isOwner: false, isPublic: true), isAdmin: false);

        var cut = RenderDetail(ctx);

        var rows = cut.FindAll(".playlist-entry-row");
        Assert.Equal(2, rows.Count);
        var accessible = rows.Single(r => r.TextContent.Contains("Freigeschaltet"));
        var locked = rows.Single(r => r.TextContent.Contains("Gesperrt"));
        Assert.Single(accessible.QuerySelectorAll(".playlist-entry-play-button"));
        Assert.DoesNotContain("opacity-50", accessible.ClassName);
        Assert.Empty(locked.QuerySelectorAll(".playlist-entry-play-button"));
        Assert.Contains("opacity-50", locked.ClassName);
        Assert.NotNull(cut.Find("#playlist-detail-public-badge"));
    }

    [Fact]
    public void Viewer_BackButton_LeadsToPublicOverview()
    {
        using var ctx = CreateContext(CreatePlaylist(isOwner: false, isPublic: true), isAdmin: false);
        var cut = RenderDetail(ctx);

        cut.Find(".playlist-detail-back-button").Click();

        Assert.EndsWith("/playlists/public", ctx.Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void Owner_RegularUser_SeesEditingAffordances_ButNoPublicToggle()
    {
        using var ctx = CreateContext(CreatePlaylist(isOwner: true, isPublic: false), isAdmin: false);

        var cut = RenderDetail(ctx);

        Assert.Single(cut.FindAll(".playlist-detail-edit-button"));
        Assert.Single(cut.FindAll(".playlist-detail-delete-button"));
        Assert.Single(cut.FindAll("#playlist-detail-upload-cover-button"));
        Assert.Single(cut.FindAll(".playlist-sortmode-toggle-button"));
        Assert.Single(cut.FindComponents<MediaSearchSelector>());
        // The toggle is not greyed out - it is not there at all.
        Assert.Empty(cut.FindAll("#playlist-detail-toggle-public-button"));
    }

    [Fact]
    public void Owner_Admin_SeesPublicToggle_AndClickingItPublishes()
    {
        var playlistClientMock = CreatePlaylistClientMock(CreatePlaylist(isOwner: true, isPublic: false));
        playlistClientMock
            .Setup(c => c.SetPlaylistPublicAsync(1, It.Is<DtoSetPlaylistPublicRequest>(r => r.IsPublic)))
            .ReturnsAsync(CreatePlaylist(isOwner: true, isPublic: true));
        using var ctx = CreateContext(playlistClientMock, isAdmin: true);
        var cut = RenderDetail(ctx);
        Assert.Empty(cut.FindAll("#playlist-detail-public-badge"));

        cut.Find("#playlist-detail-toggle-public-button").Click();

        playlistClientMock.Verify(c => c.SetPlaylistPublicAsync(1, It.Is<DtoSetPlaylistPublicRequest>(r => r.IsPublic)), Times.Once);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#playlist-detail-public-badge")));
        Assert.Equal("true", cut.Find("#playlist-detail-toggle-public-button").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Owner_Admin_ClickingToggleOnPublicPlaylist_ClearsTheFlag()
    {
        var playlistClientMock = CreatePlaylistClientMock(CreatePlaylist(isOwner: true, isPublic: true));
        playlistClientMock
            .Setup(c => c.SetPlaylistPublicAsync(1, It.Is<DtoSetPlaylistPublicRequest>(r => !r.IsPublic)))
            .ReturnsAsync(CreatePlaylist(isOwner: true, isPublic: false));
        using var ctx = CreateContext(playlistClientMock, isAdmin: true);
        var cut = RenderDetail(ctx);

        cut.Find("#playlist-detail-toggle-public-button").Click();

        playlistClientMock.Verify(c => c.SetPlaylistPublicAsync(1, It.Is<DtoSetPlaylistPublicRequest>(r => !r.IsPublic)), Times.Once);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("#playlist-detail-public-badge")));
    }

    [Fact]
    public void Owner_Admin_ServerRefusesToggle_ShowsErrorAndKeepsState()
    {
        var playlistClientMock = CreatePlaylistClientMock(CreatePlaylist(isOwner: true, isPublic: false));
        playlistClientMock
            .Setup(c => c.SetPlaylistPublicAsync(1, It.IsAny<DtoSetPlaylistPublicRequest>()))
            .ThrowsAsync(new HttpRequestException("forbidden", null, System.Net.HttpStatusCode.Forbidden));
        using var ctx = CreateContext(playlistClientMock, isAdmin: true);
        var cut = RenderDetail(ctx);

        cut.Find("#playlist-detail-toggle-public-button").Click();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("#playlist-public-status")));
        Assert.Empty(cut.FindAll("#playlist-detail-public-badge"));
    }

    private static DtoPlaylist CreatePlaylist(bool isOwner, bool isPublic) => new()
    {
        Id = 1,
        Name = "Test-Playlist",
        SortMode = PlaylistSortModeValues.Manual,
        IsOwner = isOwner,
        IsPublic = isPublic,
        GenresManuallyOverridden = true,
        Genres = new[] { new DtoGenreOption { Id = 1, Name = "Action" } }
    };

    private static Mock<IPlaylistApiClient> CreatePlaylistClientMock(DtoPlaylist playlist)
    {
        var mock = new Mock<IPlaylistApiClient>();
        mock.Setup(c => c.RequestPlaylistAsync(1)).ReturnsAsync(playlist);
        mock.Setup(c => c.RequestPlaylistEntriesPagedAsync(1, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DtoPlaylistEntriesPagedResult
            {
                Entries = new[]
                {
                    new DtoPlaylistEntry { Id = 10, PlaylistId = 1, MediaType = "Movie", MediaId = 100, MediaTitle = "Freigeschaltet", IsAccessible = true, SortOrder = 0 },
                    new DtoPlaylistEntry { Id = 11, PlaylistId = 1, MediaType = "Movie", MediaId = 101, MediaTitle = "Gesperrt", IsAccessible = false, SortOrder = 1 }
                },
                HasNextPage = false,
                TotalCount = 2
            });
        return mock;
    }

    private static global::Bunit.BunitContext CreateContext(DtoPlaylist playlist, bool isAdmin)
        => CreateContext(CreatePlaylistClientMock(playlist), isAdmin);

    private static global::Bunit.BunitContext CreateContext(Mock<IPlaylistApiClient> playlistClientMock, bool isAdmin)
    {
        var ctx = new global::Bunit.BunitContext();
        var auth = ctx.AddAuthorization().SetAuthorized("test-user");
        if (isAdmin)
            auth.SetClaims(new Claim("IsAdmin", "True"));

        ctx.Services.AddSingleton<VideoWebPlayerClient>(new NoOpVideoWebPlayerClient());
        ctx.Services.AddSingleton(playlistClientMock.Object);
        ctx.Services.AddSingleton<ILogger<PlaylistDetail>>(NullLogger<PlaylistDetail>.Instance);
        ctx.Services.AddSingleton<ILogger<PlaylistEntriesList>>(NullLogger<PlaylistEntriesList>.Instance);
        ctx.Services.AddSingleton<ILogger<MediaSearchSelector>>(NullLogger<MediaSearchSelector>.Instance);
        return ctx;
    }

    private static IRenderedComponent<PlaylistDetail> RenderDetail(global::Bunit.BunitContext ctx)
    {
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/playlists/1");
        return ctx.Render<PlaylistDetail>(parameters => parameters.Add(p => p.Id, 1));
    }
}
