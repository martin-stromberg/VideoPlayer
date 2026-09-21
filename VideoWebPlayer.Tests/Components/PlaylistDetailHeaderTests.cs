using System.Globalization;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Components.Playlists;
using VideoWebPlayer.Components.Shared.Media;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Components;

/// <summary>
/// Tests for the header ("Kopfbereich") of <see cref="PlaylistDetail"/> after the Kundenrückmeldung zur
/// Detailansicht: it follows the structure of the series/movie detail pages (D1, D11), shows the playlist
/// information with readable dates (D2), shows the information of the selected title with "Abspielen" and -
/// for the owner - "Entfernen" (D8), keeps playback (<c>?entryId=</c>) independent of the selection.
/// </summary>
public class PlaylistDetailHeaderTests
{
    // ----- D1 / D2 / D11: playlist information ---------------------------------------------------------

    [Fact]
    public void Header_ShowsPlaylistInformation_FirstNameDescriptionSortModeGenresDates()
    {
        using var ctx = CreateContext(CreatePlaylist(isOwner: true), Entries());
        var cut = RenderDetail(ctx);

        Assert.Equal("Meine Playlist", cut.Find("#playlist-detail-name").TextContent);
        Assert.Equal("Eine Beschreibung", cut.Find("#playlist-detail-description").TextContent);
        Assert.Equal("Nach Erscheinungsdatum", cut.Find("#playlist-detail-sortmode").TextContent);
        Assert.Equal("Action, Drama", cut.Find("#playlist-detail-genres").TextContent);
        Assert.NotEmpty(cut.FindAll("#playlist-detail-created"));
        Assert.NotEmpty(cut.FindAll("#playlist-detail-updated"));
        // Without a selection the header shows the playlist, not a title.
        Assert.Empty(cut.FindAll("#playlist-detail-selected-entry"));
    }

    [Fact]
    public void Header_UsesTheSameStructureAndCssClassesAsTheSeriesDetailPage()
    {
        using var ctx = CreateContext(CreatePlaylist(isOwner: true), Entries());
        var cut = RenderDetail(ctx);

        var header = cut.Find(".tvshow-header");
        Assert.Single(header.QuerySelectorAll(".tvshow-header-overlay"));
        Assert.Single(header.QuerySelectorAll(".tvshow-header-overlay .tvshow-header-content"));
        Assert.Single(header.QuerySelectorAll(".tvshow-header-content h1"));
        Assert.Single(header.QuerySelectorAll(".tvshow-header-content .tvshow-meta"));
        Assert.Single(header.QuerySelectorAll(".tvshow-header-content .tvshow-plot"));
        Assert.Single(header.QuerySelectorAll(".back-arrow"));
        // The action buttons sit INSIDE the header (on the image), like on the series page - not above it.
        Assert.Single(header.QuerySelectorAll(".metadata-action-bar"));
        Assert.All(header.QuerySelectorAll(".metadata-action-bar > button"), b => Assert.Contains("metadata-icon-btn", b.ClassList));
        Assert.Empty(cut.FindAll(".metadata-action-bar").Where(bar => !header.Contains(bar)));
        // The list is below the header, outside of it.
        Assert.Empty(header.QuerySelectorAll(".playlist-entries-list"));
    }

    [Fact]
    public void Header_HeightIsNotDeterminedByTheImage_ImageIsAbsolutelyPositionedInsideTheHeader()
    {
        using var ctx = CreateContext(CreatePlaylist(isOwner: true, coverPictureId: 5), Entries());
        var cut = RenderDetail(ctx);

        var cover = cut.Find("#playlist-detail-cover-image");
        Assert.Contains("playlist-header-cover", cover.ClassList);
        Assert.True(cut.Find(".tvshow-header").Contains(cover));
        // The placeholder shares the class, so the rules that take the image out of the flow apply to both.
        Assert.All(cut.FindAll(".playlist-header-cover"), e => Assert.True(cut.Find(".tvshow-header").Contains(e)));
    }

    [Fact]
    public void Dates_AreLabelledAndReadable_WithSpaceBetweenLabelAndValue_AndSeparatedFromEachOther()
    {
        var created = new DateTime(2026, 9, 7, 4, 23, 16, DateTimeKind.Utc);
        var updated = new DateTime(2026, 9, 8, 19, 7, 51, DateTimeKind.Utc);
        var playlist = CreatePlaylist(isOwner: true);
        playlist.CreatedAt = created;
        playlist.UpdatedAt = updated;
        using var ctx = CreateContext(playlist, Entries());
        var cut = RenderDetail(ctx);

        var de = CultureInfo.GetCultureInfo("de-DE");
        var expectedCreated = "Erstellt: " + created.ToLocalTime().ToString("dd.MM.yyyy, HH:mm 'Uhr'", de);
        var expectedUpdated = "Aktualisiert: " + updated.ToLocalTime().ToString("dd.MM.yyyy, HH:mm 'Uhr'", de);
        Assert.Equal(expectedCreated, cut.Find("#playlist-detail-created").TextContent);
        Assert.Equal(expectedUpdated, cut.Find("#playlist-detail-updated").TextContent);

        // The regression of the feedback: label and value fused ("Erstellt07.09.2026 06:23:16 Aktualisiert...").
        var container = cut.Find(".playlist-detail-dates-muted");
        Assert.DoesNotContain("Erstellt0", container.TextContent);
        Assert.DoesNotContain("Aktualisiert0", container.TextContent);
        Assert.DoesNotMatch(@"Erstellt\d", container.TextContent);
        // Two separate elements (own spans) - the visual gap comes from the flex gap in CSS.
        Assert.Equal(2, container.Children.Length);
        Assert.Equal("Erstellt:", cut.Find("#playlist-detail-created .playlist-detail-date-label").TextContent);
        Assert.Equal("Aktualisiert:", cut.Find("#playlist-detail-updated .playlist-detail-date-label").TextContent);
        // Machine-readable values for the dates.
        Assert.StartsWith("2026-09-07", cut.Find("#playlist-detail-created time").GetAttribute("datetime"));
    }

    [Fact]
    public void Dates_UseGermanFormat_NotTheServerCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var playlist = CreatePlaylist(isOwner: true);
            playlist.CreatedAt = new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc);
            using var ctx = CreateContext(playlist, Entries());
            var cut = RenderDetail(ctx);

            Assert.Matches(@"^Erstellt: \d{2}\.\d{2}\.2026, \d{2}:\d{2} Uhr$", cut.Find("#playlist-detail-created").TextContent);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    // ----- D8: selected title in the header ------------------------------------------------------------

    [Fact]
    public void SelectingAMovie_ShowsItsInformationInTheHeader()
    {
        using var ctx = CreateContext(CreatePlaylist(isOwner: true), Entries());
        var cut = RenderDetail(ctx);

        cut.FindAll(".playlist-entry-row").Single(r => r.TextContent.Contains("Der Film")).Click();

        Assert.Equal("Der Film", cut.Find("#playlist-detail-entry-title").TextContent);
        Assert.Equal("2018", cut.Find("#playlist-detail-entry-year").TextContent);
        Assert.Equal("Film", cut.Find("#playlist-detail-entry-type").TextContent);
        Assert.Equal("Eine Filmhandlung.", cut.Find("#playlist-detail-entry-plot").TextContent);
        Assert.Contains("/api/pictures/77", cut.Find("#playlist-detail-entry-poster").GetAttribute("src"));
        Assert.Empty(cut.FindAll("#playlist-detail-entry-parent"));
        // The playlist information is replaced by the title information.
        Assert.Empty(cut.FindAll("#playlist-detail-name"));
        Assert.Empty(cut.FindAll("#playlist-detail-created"));
        // The selected tile is marked in the list.
        Assert.Equal("true", cut.FindAll(".playlist-entry-row").Single(r => r.TextContent.Contains("Der Film")).GetAttribute("aria-selected"));
    }

    [Fact]
    public void SelectingAnEpisode_ShowsTypeEpisodeNumberAndTheSeriesItBelongsTo()
    {
        using var ctx = CreateContext(CreatePlaylist(isOwner: true), Entries());
        var cut = RenderDetail(ctx);

        cut.FindAll(".playlist-entry-row").Single(r => r.TextContent.Contains("Die Folge")).Click();

        Assert.Equal("Die Folge", cut.Find("#playlist-detail-entry-title").TextContent);
        Assert.Equal("Episode 3", cut.Find("#playlist-detail-entry-type").TextContent);
        Assert.Equal("Serie: Die Serie", cut.Find("#playlist-detail-entry-parent").TextContent);
    }

    /// <summary>
    /// As on the series page, the image of a selected episode becomes the background of the header (customer
    /// feedback: the title's image and information are shown in the header); the small poster is then not needed.
    /// </summary>
    [Fact]
    public void SelectingAnEpisode_ShowsItsBackgroundImageInTheHeader_InsteadOfThePoster()
    {
        using var ctx = CreateContext(CreatePlaylist(isOwner: true), Entries());
        var cut = RenderDetail(ctx);

        cut.FindAll(".playlist-entry-row").Single(r => r.TextContent.Contains("Die Folge")).Click();

        Assert.Contains("/api/episodes/200/background-image", cut.Find("#playlist-detail-entry-background").GetAttribute("src"));
        Assert.Empty(cut.FindAll("#playlist-detail-entry-poster"));

        // Back to the playlist: the header shows the playlist again, without an entry background.
        cut.Find("#playlist-detail-back-button").Click();
        Assert.Empty(cut.FindAll("#playlist-detail-entry-background"));
    }

    [Fact]
    public void SelectingAMovie_KeepsThePosterAndShowsNoEpisodeBackground()
    {
        using var ctx = CreateContext(CreatePlaylist(isOwner: true), Entries());
        var cut = RenderDetail(ctx);

        cut.FindAll(".playlist-entry-row").Single(r => r.TextContent.Contains("Der Film")).Click();

        Assert.Empty(cut.FindAll("#playlist-detail-entry-background"));
        Assert.NotEmpty(cut.FindAll("#playlist-detail-entry-poster"));
    }

    /// <summary>
    /// A locked episode (public playlist of another user, not unlocked for the viewer) must not load its
    /// background image into the header.
    /// </summary>
    [Fact]
    public void SelectingALockedEpisode_LoadsNoBackgroundImage()
    {
        var entries = new[]
        {
            new DtoPlaylistEntry
            {
                Id = 12, PlaylistId = 1, MediaType = "TVShowEpisode", MediaId = 201, MediaTitle = "Gesperrte Folge", IsAccessible = false,
                ParentMediaType = "TVShow", ParentMediaId = 5, ParentMediaTitle = "Die Serie", AddedAt = DateTime.UtcNow
            }
        };
        using var ctx = CreateContext(CreatePlaylist(isOwner: false), entries);
        var cut = RenderDetail(ctx);

        cut.Find(".playlist-entry-row").Click();

        Assert.Empty(cut.FindAll("#playlist-detail-entry-background"));
    }

    [Fact]
    public void SelectedEntry_WithoutOptionalData_ShowsTitleTypeAndImageWithoutEmptyFields()
    {
        var entries = new[]
        {
            new DtoPlaylistEntry { Id = 30, PlaylistId = 1, MediaType = "Movie", MediaId = 300, MediaTitle = "Nackter Film", IsAccessible = true, AddedAt = DateTime.UtcNow }
        };
        using var ctx = CreateContext(CreatePlaylist(isOwner: true), entries);
        var cut = RenderDetail(ctx);

        cut.Find(".playlist-entry-row").Click();

        Assert.Equal("Nackter Film", cut.Find("#playlist-detail-entry-title").TextContent);
        Assert.Equal("Film", cut.Find("#playlist-detail-entry-type").TextContent);
        Assert.Empty(cut.FindAll("#playlist-detail-entry-year"));
        Assert.Empty(cut.FindAll("#playlist-detail-entry-plot"));
        Assert.Empty(cut.FindAll("#playlist-detail-entry-parent"));
        Assert.Contains("/images/placeholder.png", cut.Find("#playlist-detail-entry-poster").GetAttribute("src"));
    }

    [Fact]
    public void SelectedEntry_HeaderHasPlayButtonInsideTheOverlay_LikeTheEpisodeOnTheSeriesPage()
    {
        using var ctx = CreateContext(CreatePlaylist(isOwner: true), Entries());
        var cut = RenderDetail(ctx);
        Assert.Empty(cut.FindAll("#playlist-detail-play-entry-button"));

        cut.FindAll(".playlist-entry-row").Single(r => r.TextContent.Contains("Der Film")).Click();

        var play = Assert.Single(cut.FindAll(".tvshow-header-overlay #playlist-detail-play-entry-button"));
        Assert.Contains("play-button", play.ClassList);
        Assert.Equal("Abspielen", play.GetAttribute("title"));
        Assert.Equal("Abspielen", play.GetAttribute("aria-label"));
    }

    [Fact]
    public void BackButton_WithSelection_ClearsItAndShowsThePlaylistAgain_WithoutLeavingThePage()
    {
        using var ctx = CreateContext(CreatePlaylist(isOwner: true), Entries());
        var cut = RenderDetail(ctx);
        var uriBefore = ctx.Services.GetRequiredService<NavigationManager>().Uri;
        cut.FindAll(".playlist-entry-row").First().Click();
        Assert.Contains("aufheben", cut.Find("#playlist-detail-back-button").GetAttribute("title"));

        cut.Find("#playlist-detail-back-button").Click();

        Assert.Empty(cut.FindAll("#playlist-detail-selected-entry"));
        Assert.Equal("Meine Playlist", cut.Find("#playlist-detail-name").TextContent);
        Assert.Equal(uriBefore, ctx.Services.GetRequiredService<NavigationManager>().Uri);
        Assert.All(cut.FindAll(".playlist-entry-row"), r => Assert.Equal("false", r.GetAttribute("aria-selected")));
        Assert.Equal("Zurück zur Übersicht", cut.Find("#playlist-detail-back-button").GetAttribute("title"));
    }

    [Fact]
    public void Selection_ChangesOnlyTheHeader_NotTheUrl_EntryIdQueryParameterIsPlaybackOnly()
    {
        using var ctx = CreateContext(CreatePlaylist(isOwner: true), Entries());
        var cut = RenderDetail(ctx);
        var nav = ctx.Services.GetRequiredService<NavigationManager>();

        cut.FindAll(".playlist-entry-row").First().Click();

        Assert.DoesNotContain("entryId", nav.Uri);
        // No playback was started by merely selecting.
        Assert.Empty(cut.FindComponents<VideoPlayer>());
    }

    [Fact]
    public void PlayFromHeader_StartsPlaylistPlaybackAtTheSelectedEntry_WithPlaylistContext()
    {
        var mock = CreateMock(CreatePlaylist(isOwner: false, isPublic: true), Entries());
        mock.Setup(c => c.StartPlaylistAsync(1, 10)).ReturnsAsync(new DtoPlaylistPlaybackStart
        {
            PlaylistId = 1,
            PlaylistName = "Meine Playlist",
            TotalCount = 2,
            CurrentPosition = 1,
            CurrentEntryId = 10,
            MediaType = "movie",
            MediaId = 100,
            StreamUrl = "/api/items/movie/100/stream"
        });
        using var ctx = CreateContext(mock);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = RenderDetail(ctx);
        cut.FindAll(".playlist-entry-row").Single(r => r.TextContent.Contains("Der Film")).Click();

        cut.Find("#playlist-detail-play-entry-button").Click();

        mock.Verify(c => c.StartPlaylistAsync(1, 10), Times.Once);
        Assert.Contains("entryId=10", ctx.Services.GetRequiredService<NavigationManager>().Uri);
        var player = Assert.Single(cut.FindComponents<VideoPlayer>());
        Assert.NotNull(player.Instance.PlaylistContext);
        Assert.Equal(1, player.Instance.PlaylistContext!.PlaylistId);
    }

    [Fact]
    public void DoubleClickOnTile_StillStartsPlayback_ExistingBehaviour()
    {
        var mock = CreateMock(CreatePlaylist(isOwner: true), Entries());
        mock.Setup(c => c.StartPlaylistAsync(1, 10)).ReturnsAsync(new DtoPlaylistPlaybackStart
        {
            PlaylistId = 1,
            PlaylistName = "Meine Playlist",
            TotalCount = 2,
            CurrentPosition = 1,
            CurrentEntryId = 10,
            MediaType = "movie",
            MediaId = 100,
            StreamUrl = "/api/items/movie/100/stream"
        });
        using var ctx = CreateContext(mock);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = RenderDetail(ctx);

        cut.FindAll(".playlist-entry-row").Single(r => r.TextContent.Contains("Der Film")).DoubleClick();

        mock.Verify(c => c.StartPlaylistAsync(1, 10), Times.Once);
    }

    [Fact]
    public void EntryIdQueryParameter_StillStartsPlayback_WithoutSelectingAnything()
    {
        var mock = CreateMock(CreatePlaylist(isOwner: true), Entries());
        mock.Setup(c => c.StartPlaylistAsync(1, 11)).ReturnsAsync(new DtoPlaylistPlaybackStart
        {
            PlaylistId = 1,
            PlaylistName = "Meine Playlist",
            TotalCount = 2,
            CurrentPosition = 2,
            CurrentEntryId = 11,
            MediaType = "episode",
            MediaId = 200,
            StreamUrl = "/api/items/tvshowepisode/200/stream"
        });
        using var ctx = CreateContext(mock);
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("/playlists/1?entryId=11");

        var cut = ctx.Render<PlaylistDetail>(parameters => parameters.Add(p => p.Id, 1));

        mock.Verify(c => c.StartPlaylistAsync(1, 11), Times.Once);
        Assert.Single(cut.FindComponents<VideoPlayer>());
        // Playback resumes via the query parameter; the header still shows the playlist (no selection is derived).
        Assert.Empty(cut.FindAll("#playlist-detail-selected-entry"));
    }

    // ----- D8: removing the selected title (owner only) ------------------------------------------------

    [Fact]
    public async Task Owner_RemoveFromHeader_RemovesTheEntry_ClearsTheSelection_AndReloadsTheList()
    {
        var entries = Entries();
        var mock = CreateMock(CreatePlaylist(isOwner: true), entries);
        mock.Setup(c => c.RemoveMediaFromPlaylistAsync(1, "Movie", 100, false)).Returns(Task.CompletedTask);
        using var ctx = CreateContext(mock);
        var cut = RenderDetail(ctx);
        cut.FindAll(".playlist-entry-row").Single(r => r.TextContent.Contains("Der Film")).Click();
        // After the removal the server returns the remaining entry only.
        mock.Setup(c => c.RequestPlaylistEntriesPagedAsync(1, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DtoPlaylistEntriesPagedResult { Entries = new[] { entries[1] }, HasNextPage = false, TotalCount = 1 });

        await cut.InvokeAsync(() => cut.Find("#playlist-detail-remove-entry-button").ClickAsync(new MouseEventArgs()));

        mock.Verify(c => c.RemoveMediaFromPlaylistAsync(1, "Movie", 100, false), Times.Once);
        Assert.Empty(cut.FindAll("#playlist-detail-selected-entry"));
        Assert.Equal("Meine Playlist", cut.Find("#playlist-detail-name").TextContent);
        Assert.Single(cut.FindAll(".playlist-entry-row"));
    }

    [Fact]
    public async Task Owner_RemoveFromHeader_ContinueWatchingConflict_AsksFirst_ThenConfirmedRemovalSucceeds()
    {
        var mock = CreateMock(CreatePlaylist(isOwner: true), Entries());
        mock.Setup(c => c.RemoveMediaFromPlaylistAsync(1, "Movie", 100, false))
            .ThrowsAsync(new HttpRequestException("Weiterschauen", null, System.Net.HttpStatusCode.Conflict));
        mock.Setup(c => c.RemoveMediaFromPlaylistAsync(1, "Movie", 100, true)).Returns(Task.CompletedTask);
        using var ctx = CreateContext(mock);
        var cut = RenderDetail(ctx);
        cut.FindAll(".playlist-entry-row").Single(r => r.TextContent.Contains("Der Film")).Click();

        await cut.InvokeAsync(() => cut.Find("#playlist-detail-remove-entry-button").ClickAsync(new MouseEventArgs()));

        // The Schritt-7 safety question appears; nothing was removed yet and the selection is still there.
        Assert.Single(cut.FindComponents<PlaylistEntryContinueWatchingConfirmationDialog>());
        mock.Verify(c => c.RemoveMediaFromPlaylistAsync(1, "Movie", 100, true), Times.Never);
        Assert.Single(cut.FindAll("#playlist-detail-selected-entry"));

        await cut.InvokeAsync(() => cut.Find("#confirm-remove-continuewatching-button").ClickAsync(new MouseEventArgs()));

        mock.Verify(c => c.RemoveMediaFromPlaylistAsync(1, "Movie", 100, true), Times.Once);
        Assert.Empty(cut.FindAll("#playlist-detail-selected-entry"));
    }

    [Fact]
    public async Task Owner_RemoveFromHeader_ContinueWatchingConflict_CancelKeepsEverything()
    {
        var mock = CreateMock(CreatePlaylist(isOwner: true), Entries());
        mock.Setup(c => c.RemoveMediaFromPlaylistAsync(1, "Movie", 100, false))
            .ThrowsAsync(new HttpRequestException("Weiterschauen", null, System.Net.HttpStatusCode.Conflict));
        using var ctx = CreateContext(mock);
        var cut = RenderDetail(ctx);
        cut.FindAll(".playlist-entry-row").Single(r => r.TextContent.Contains("Der Film")).Click();
        await cut.InvokeAsync(() => cut.Find("#playlist-detail-remove-entry-button").ClickAsync(new MouseEventArgs()));

        await cut.InvokeAsync(() => cut.Find("#cancel-remove-continuewatching-button").ClickAsync(new MouseEventArgs()));

        mock.Verify(c => c.RemoveMediaFromPlaylistAsync(1, "Movie", 100, true), Times.Never);
        Assert.Empty(cut.FindComponents<PlaylistEntryContinueWatchingConfirmationDialog>());
        Assert.Single(cut.FindAll("#playlist-detail-selected-entry"));
        Assert.Equal(2, cut.FindAll(".playlist-entry-row").Count);
    }

    [Fact]
    public async Task Owner_RemovingTheLastTitleFromTheHeader_SwitchesToAddMode()
    {
        var only = new[] { Entries()[0] };
        var mock = CreateMock(CreatePlaylist(isOwner: true), only);
        mock.Setup(c => c.RemoveMediaFromPlaylistAsync(1, "Movie", 100, false)).Returns(Task.CompletedTask);
        using var ctx = CreateContext(mock);
        var cut = RenderDetail(ctx);
        cut.Find(".playlist-entry-row").Click();
        mock.Setup(c => c.RequestPlaylistEntriesPagedAsync(1, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DtoPlaylistEntriesPagedResult { Entries = Array.Empty<DtoPlaylistEntry>(), HasNextPage = false, TotalCount = 0 });

        await cut.InvokeAsync(() => cut.Find("#playlist-detail-remove-entry-button").ClickAsync(new MouseEventArgs()));

        Assert.Single(cut.FindComponents<MediaSearchSelector>());
        Assert.Empty(cut.FindAll("#playlist-detail-selected-entry"));
    }

    [Fact]
    public void Owner_SelectingAnEntry_HidesThePlaylistActions_OnlyRemoveEntryRemains()
    {
        using var ctx = CreateContext(CreatePlaylist(isOwner: true), Entries());
        var cut = RenderDetail(ctx);
        Assert.NotEmpty(cut.FindAll(".playlist-detail-edit-button"));

        cut.FindAll(".playlist-entry-row").First().Click();

        // The playlist-level actions (edit/delete playlist, image, ...) belong to the playlist view; with a
        // selected title the bar only offers removing that title - no second, ambiguous "Löschen".
        Assert.Empty(cut.FindAll(".playlist-detail-delete-button"));
        Assert.Empty(cut.FindAll(".playlist-detail-edit-button"));
        Assert.Empty(cut.FindAll("#playlist-detail-cover-button"));
        Assert.Single(cut.FindAll("#playlist-detail-remove-entry-button"));
    }

    // ----- D10: the content-area toggle lives in the header --------------------------------------------

    /// <summary>
    /// Kundenrückmeldung: the two mode buttons moved out of the content area into the action bar on the image,
    /// and only ONE of them is shown - the one switching to the respective other area (like the publish button,
    /// which also only ever carries the current symbol).
    /// </summary>
    [Fact]
    public void ModeToggle_SitsInTheHeaderActionBar_AndShowsOnlyTheButtonForTheOtherArea()
    {
        using var ctx = CreateContext(CreatePlaylist(isOwner: true), Entries());
        var cut = RenderDetail(ctx);

        // A non-empty playlist starts in the list mode, so only the "Titel hinzufügen" button exists.
        var toggle = Assert.Single(cut.FindAll(".tvshow-header .metadata-action-bar #playlist-mode-add-button"));
        Assert.Empty(cut.FindAll("#playlist-mode-entries-button"));
        Assert.Contains("metadata-icon-btn", toggle.ClassList);
        Assert.Equal("Titel hinzufügen", toggle.GetAttribute("title"));
        Assert.Equal(toggle.GetAttribute("title"), toggle.GetAttribute("aria-label"));
        Assert.NotNull(toggle.QuerySelector("svg"));

        cut.Find("#playlist-mode-add-button").Click();

        // Now the opposite: only the button back to the list, and the add area is shown.
        Assert.Single(cut.FindAll(".tvshow-header .metadata-action-bar #playlist-mode-entries-button"));
        Assert.Empty(cut.FindAll("#playlist-mode-add-button"));
        Assert.Equal("Titel der Playlist auflisten", cut.Find("#playlist-mode-entries-button").GetAttribute("title"));
        Assert.Single(cut.FindComponents<MediaSearchSelector>());
    }

    [Fact]
    public void ModeToggle_EmptyPlaylist_StartsInAddMode_AndOffersOnlyTheWayBackToTheList()
    {
        using var ctx = CreateContext(CreatePlaylist(isOwner: true), Array.Empty<DtoPlaylistEntry>());
        var cut = RenderDetail(ctx);

        Assert.Single(cut.FindComponents<MediaSearchSelector>());
        Assert.Single(cut.FindAll("#playlist-mode-entries-button"));
        Assert.Empty(cut.FindAll("#playlist-mode-add-button"));
    }

    /// <summary>
    /// The toggle stays reachable while a title is selected - switching to "Titel hinzufügen" then clears the
    /// selection, so the header shows the playlist again instead of a title that is no longer listed.
    /// </summary>
    [Fact]
    public void ModeToggle_WithASelectedTitle_SwitchesAndClearsTheSelection()
    {
        using var ctx = CreateContext(CreatePlaylist(isOwner: true), Entries());
        var cut = RenderDetail(ctx);
        cut.FindAll(".playlist-entry-row").First().Click();
        Assert.Single(cut.FindAll("#playlist-detail-selected-entry"));

        cut.Find("#playlist-mode-add-button").Click();

        Assert.Empty(cut.FindAll("#playlist-detail-selected-entry"));
        Assert.Equal("Meine Playlist", cut.Find("#playlist-detail-name").TextContent);
        Assert.Single(cut.FindComponents<MediaSearchSelector>());
    }

    // ----- helpers -----------------------------------------------------------------------------------------

    private static DtoPlaylist CreatePlaylist(bool isOwner, bool isPublic = false, long? coverPictureId = null) => new()
    {
        Id = 1,
        Name = "Meine Playlist",
        Description = "Eine Beschreibung",
        SortMode = PlaylistSortModeValues.ByReleaseDate,
        IsOwner = isOwner,
        IsPublic = isPublic,
        CoverPictureId = coverPictureId,
        CreatedAt = new DateTime(2026, 9, 7, 4, 23, 16, DateTimeKind.Utc),
        UpdatedAt = new DateTime(2026, 9, 8, 19, 7, 51, DateTimeKind.Utc),
        Genres = new[] { new DtoGenreOption { Id = 1, Name = "Action" }, new DtoGenreOption { Id = 2, Name = "Drama" } }
    };

    private static DtoPlaylistEntry[] Entries() => new[]
    {
        new DtoPlaylistEntry
        {
            Id = 10, PlaylistId = 1, MediaType = "Movie", MediaId = 100, MediaTitle = "Der Film", IsAccessible = true,
            ResolvedPictureId = 77, ReleaseDate = new DateTime(2018, 4, 12), Plot = "Eine Filmhandlung.", AddedAt = DateTime.UtcNow
        },
        new DtoPlaylistEntry
        {
            Id = 11, PlaylistId = 1, MediaType = "TVShowEpisode", MediaId = 200, MediaTitle = "Die Folge", IsAccessible = true,
            ParentMediaType = "TVShow", ParentMediaId = 5, ParentMediaTitle = "Die Serie", EpisodeNumber = 3,
            ReleaseDate = new DateTime(2021, 9, 1), Plot = "Eine Folgenhandlung.", AddedAt = DateTime.UtcNow
        }
    };

    private static Mock<IPlaylistApiClient> CreateMock(DtoPlaylist playlist, DtoPlaylistEntry[] entries)
    {
        var mock = new Mock<IPlaylistApiClient>();
        mock.Setup(c => c.RequestPlaylistAsync(1)).ReturnsAsync(playlist);
        mock.Setup(c => c.RequestPlaylistEntriesPagedAsync(1, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DtoPlaylistEntriesPagedResult { Entries = entries, HasNextPage = false, TotalCount = entries.Length });
        return mock;
    }

    private static global::Bunit.BunitContext CreateContext(DtoPlaylist playlist, DtoPlaylistEntry[] entries)
        => CreateContext(CreateMock(playlist, entries));

    private static global::Bunit.BunitContext CreateContext(Mock<IPlaylistApiClient> mock)
    {
        var ctx = new global::Bunit.BunitContext();
        ctx.AddAuthorization().SetAuthorized("test-user");
        ctx.Services.AddSingleton<VideoWebPlayerClient>(new NoOpVideoWebPlayerClient());
        ctx.Services.AddSingleton(mock.Object);
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
