using Bunit;
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
/// Integration test covering the wiring between <see cref="MediaSearchSelector"/> and
/// <see cref="PlaylistEntriesList"/>: a search-result selection must reach
/// <c>IPlaylistApiClient.AddMediaToPlaylistAsync</c> via <c>PlaylistEntriesList.OnMediaSelectedAsync</c>
/// and <c>AddEntryAsync</c>, exactly as the removed dropdown + id-input previously did.
/// </summary>
public class PlaylistEntriesListTests
{
    [Fact]
    public async Task PlaylistEntriesList_OnMediaSelectedAsync_CallsAddEntryAsync()
    {
        using var ctx = new global::Bunit.TestContext();

        var playlistClientMock = new Mock<IPlaylistApiClient>();
        playlistClientMock
            .Setup(c => c.RequestPlaylistEntriesPagedAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DtoPlaylistEntriesPagedResult { Entries = Array.Empty<DtoPlaylistEntry>(), HasNextPage = false, TotalCount = 0 });
        playlistClientMock
            .Setup(c => c.AddMediaToPlaylistAsync(It.IsAny<long>(), It.IsAny<DtoAddMediaToPlaylistRequest>()))
            .ReturnsAsync(new DtoPlaylistAddResult { Message = "1 Titel hinzugefuegt." });

        ctx.Services.AddSingleton<VideoWebPlayerClient>(new NoOpVideoWebPlayerClient());
        ctx.Services.AddSingleton(playlistClientMock.Object);
        ctx.Services.AddSingleton<ILogger<PlaylistEntriesList>>(NullLogger<PlaylistEntriesList>.Instance);
        ctx.Services.AddSingleton<ILogger<MediaSearchSelector>>(NullLogger<MediaSearchSelector>.Instance);

        var cut = ctx.Render<PlaylistEntriesList>(parameters => parameters
            .Add(p => p.PlaylistId, 1)
            .Add(p => p.IsManualMode, false));

        var searchSelector = cut.FindComponent<MediaSearchSelector>();
        await cut.InvokeAsync(() => searchSelector.Instance.OnMediaSelected.InvokeAsync(("Movie", 42L)));

        playlistClientMock.Verify(
            c => c.AddMediaToPlaylistAsync(1, It.Is<DtoAddMediaToPlaylistRequest>(r => r.MediaType == "Movie" && r.MediaId == 42)),
            Times.Once);
    }

    /// <summary>
    /// Regression tests for the "IsPlayableEntry ignores IsAccessible" and "@ondblclick unconditionally
    /// bound" bugs (Playlist-Wiedergabe Schritt 5, Runde 1 Nachbesserung, Punkte 2/3): a locked
    /// (<c>IsAccessible == false</c>) or non-playable collection entry (e.g. TVShow) must render no
    /// "Abspielen" button, and double-clicking its row must not raise <see cref="PlaylistEntriesList.OnPlayEntry"/>
    /// - previously a locked entry's "Abspielen" button led into a 403 that replaced the whole
    /// PlaylistDetail page, and a collection entry's row double-click resolved its collection MediaId as if
    /// it were a playable movie/episode id.
    /// </summary>
    [Fact]
    public async Task PlaylistEntriesList_LockedEntry_HasNoPlayButtonAndDoubleClickHasNoEffect()
    {
        var entry = new DtoPlaylistEntry { Id = 1, PlaylistId = 1, MediaType = "Movie", MediaId = 10, MediaTitle = "Gesperrter Film", IsAccessible = false };
        var (cut, onPlayEntryCalls) = RenderWithEntry(entry);

        var row = cut.Find(".playlist-entry-row");
        Assert.Empty(cut.FindAll("button.playlist-entry-play-button"));

        await cut.InvokeAsync(() => row.DoubleClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));

        Assert.Empty(onPlayEntryCalls);
    }

    /// <summary>
    /// Regression test for the Playlist-UI-Redesign requirement ("Wurde eine ganze Serie hinzugefuegt, so
    /// darf es keine Kachel fuer die Serie selbst oder die Staffeln geben"): a non-playable collection
    /// entry (e.g. TVShow, added alongside its show's cascade for organizational bookkeeping - see
    /// <c>PlaylistEntriesList.IsRenderableEntry</c>) must not render its own tile at all, and consequently
    /// cannot raise <see cref="PlaylistEntriesList.OnPlayEntry"/> either.
    /// </summary>
    [Fact]
    public void PlaylistEntriesList_CollectionEntry_RendersNoTile()
    {
        var entry = new DtoPlaylistEntry { Id = 2, PlaylistId = 1, MediaType = "TVShow", MediaId = 20, MediaTitle = "Serie", IsAccessible = true };
        var (cut, onPlayEntryCalls) = RenderWithEntry(entry);

        Assert.Empty(cut.FindAll(".playlist-entry-row"));
        Assert.Empty(cut.FindAll("button.playlist-entry-play-button"));
        Assert.Empty(onPlayEntryCalls);
    }

    [Fact]
    public async Task PlaylistEntriesList_PlayableAccessibleEntry_HasPlayButtonAndDoubleClickInvokesOnPlayEntry()
    {
        var entry = new DtoPlaylistEntry { Id = 3, PlaylistId = 1, MediaType = "Movie", MediaId = 30, MediaTitle = "Zugaenglicher Film", IsAccessible = true };
        var (cut, onPlayEntryCalls) = RenderWithEntry(entry);

        var row = cut.Find(".playlist-entry-row");
        Assert.Single(cut.FindAll("button.playlist-entry-play-button"));

        await cut.InvokeAsync(() => row.DoubleClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));

        var raisedEntry = Assert.Single(onPlayEntryCalls);
        Assert.Equal(entry.Id, raisedEntry.Id);
    }

    /// <summary>
    /// Renders <see cref="PlaylistEntriesList"/> with a single given entry, wiring
    /// <see cref="PlaylistEntriesList.OnPlayEntry"/> to append to the returned list. Shared by the
    /// "Abspielen"-button-visibility and double-click regression tests above.
    /// </summary>
    /// <param name="entry">The single entry the mocked <c>RequestPlaylistEntriesPagedAsync</c> call returns.</param>
    /// <param name="Cut">(Return tuple field.) The rendered component.</param>
    /// <param name="OnPlayEntryCalls">(Return tuple field.) The list every <see cref="PlaylistEntriesList.OnPlayEntry"/> call appends to.</param>
    /// <returns>The rendered component and the list every <see cref="PlaylistEntriesList.OnPlayEntry"/> call appends to.</returns>
    private static (global::Bunit.IRenderedComponent<PlaylistEntriesList> Cut, List<DtoPlaylistEntry> OnPlayEntryCalls) RenderWithEntry(DtoPlaylistEntry entry)
    {
        var ctx = new global::Bunit.TestContext();

        var playlistClientMock = new Mock<IPlaylistApiClient>();
        playlistClientMock
            .Setup(c => c.RequestPlaylistEntriesPagedAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DtoPlaylistEntriesPagedResult { Entries = new[] { entry }, HasNextPage = false, TotalCount = 1 });

        ctx.Services.AddSingleton<VideoWebPlayerClient>(new NoOpVideoWebPlayerClient());
        ctx.Services.AddSingleton(playlistClientMock.Object);
        ctx.Services.AddSingleton<ILogger<PlaylistEntriesList>>(NullLogger<PlaylistEntriesList>.Instance);
        ctx.Services.AddSingleton<ILogger<MediaSearchSelector>>(NullLogger<MediaSearchSelector>.Instance);

        var calls = new List<DtoPlaylistEntry>();
        var cut = ctx.Render<PlaylistEntriesList>(parameters => parameters
            .Add(p => p.PlaylistId, 1)
            .Add(p => p.IsManualMode, false)
            .Add(p => p.OnPlayEntry, EventCallback.Factory.Create<DtoPlaylistEntry>(new object(), e => calls.Add(e))));

        return (cut, calls);
    }
}
