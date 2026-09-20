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
        using var ctx = new global::Bunit.BunitContext();

        var playlistClientMock = new Mock<IPlaylistApiClient>();
        playlistClientMock
            .Setup(c => c.RequestPlaylistEntriesPagedAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DtoPlaylistEntriesPagedResult { Entries = Array.Empty<DtoPlaylistEntry>(), HasNextPage = false, TotalCount = 0 });
        playlistClientMock
            .Setup(c => c.AddMediaToPlaylistAsync(It.IsAny<long>(), It.IsAny<DtoAddMediaToPlaylistRequest>()))
            .ReturnsAsync(new DtoPlaylistAddResult { Message = "1 Titel hinzugefügt." });

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
        var entry = new DtoPlaylistEntry { Id = 3, PlaylistId = 1, MediaType = "Movie", MediaId = 30, MediaTitle = "Zugänglicher Film", IsAccessible = true };
        var (cut, onPlayEntryCalls) = RenderWithEntry(entry);

        var row = cut.Find(".playlist-entry-row");
        Assert.Single(cut.FindAll("button.playlist-entry-play-button"));

        await cut.InvokeAsync(() => row.DoubleClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));

        var raisedEntry = Assert.Single(onPlayEntryCalls);
        Assert.Equal(entry.Id, raisedEntry.Id);
    }

    /// <summary>
    /// Regression/behavior test for the Entwicklungsschritt-7 Sicherheitsabfrage: removing an entry that
    /// the server reports as referenced by a continue-watching (Weiterschauen) entry bound to the same
    /// playlist (409 Conflict, mirroring <c>PlaylistDetailTests</c>'s pattern for the sort-mode-change
    /// confirmation) must show <see cref="PlaylistEntryContinueWatchingConfirmationDialog"/> instead of
    /// removing anything or showing a plain error, and must not yet call
    /// <c>RemoveMediaFromPlaylistAsync</c> a second time.
    /// </summary>
    [Fact]
    public async Task PlaylistEntriesList_RemoveEntry_ContinueWatchingConflict_ShowsConfirmationDialog()
    {
        var entry = new DtoPlaylistEntry { Id = 4, PlaylistId = 1, MediaType = "Movie", MediaId = 40, MediaTitle = "Weiterschauen-Titel", IsAccessible = true };
        var (cut, playlistClientMock) = RenderWithEntryAndMock(entry);

        playlistClientMock
            .Setup(c => c.RemoveMediaFromPlaylistAsync(1, "Movie", 40, false))
            .ThrowsAsync(new HttpRequestException("Weiterschauen-Bezug", null, System.Net.HttpStatusCode.Conflict));

        var removeButton = cut.Find("button.playlist-entry-remove-button");
        await cut.InvokeAsync(() => removeButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));

        var dialog = cut.FindComponent<PlaylistEntryContinueWatchingConfirmationDialog>();
        Assert.NotNull(dialog);
        Assert.Contains("Weiterschauen-Liste", cut.Markup);

        playlistClientMock.Verify(c => c.RemoveMediaFromPlaylistAsync(1, "Movie", 40, true), Times.Never);
    }

    /// <summary>
    /// Confirming <see cref="PlaylistEntryContinueWatchingConfirmationDialog"/> must retry the removal with
    /// <c>confirmContinueWatchingRemoval: true</c> and close the dialog again.
    /// </summary>
    [Fact]
    public async Task PlaylistEntriesList_ConfirmContinueWatchingRemoval_RetriesWithConfirmationFlag()
    {
        var entry = new DtoPlaylistEntry { Id = 5, PlaylistId = 1, MediaType = "Movie", MediaId = 50, MediaTitle = "Weiterschauen-Titel", IsAccessible = true };
        var (cut, playlistClientMock) = RenderWithEntryAndMock(entry);

        playlistClientMock
            .Setup(c => c.RemoveMediaFromPlaylistAsync(1, "Movie", 50, false))
            .ThrowsAsync(new HttpRequestException("Weiterschauen-Bezug", null, System.Net.HttpStatusCode.Conflict));
        playlistClientMock
            .Setup(c => c.RemoveMediaFromPlaylistAsync(1, "Movie", 50, true))
            .Returns(Task.CompletedTask);

        var removeButton = cut.Find("button.playlist-entry-remove-button");
        await cut.InvokeAsync(() => removeButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));

        var confirmButton = cut.Find("#confirm-remove-continuewatching-button");
        await cut.InvokeAsync(() => confirmButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));

        playlistClientMock.Verify(c => c.RemoveMediaFromPlaylistAsync(1, "Movie", 50, true), Times.Once);
        Assert.Empty(cut.FindComponents<PlaylistEntryContinueWatchingConfirmationDialog>());
    }

    /// <summary>
    /// Canceling <see cref="PlaylistEntryContinueWatchingConfirmationDialog"/> must close the dialog and
    /// must not retry the removal at all.
    /// </summary>
    [Fact]
    public async Task PlaylistEntriesList_CancelContinueWatchingRemoval_ClosesDialogWithoutRetrying()
    {
        var entry = new DtoPlaylistEntry { Id = 6, PlaylistId = 1, MediaType = "Movie", MediaId = 60, MediaTitle = "Weiterschauen-Titel", IsAccessible = true };
        var (cut, playlistClientMock) = RenderWithEntryAndMock(entry);

        playlistClientMock
            .Setup(c => c.RemoveMediaFromPlaylistAsync(1, "Movie", 60, false))
            .ThrowsAsync(new HttpRequestException("Weiterschauen-Bezug", null, System.Net.HttpStatusCode.Conflict));

        var removeButton = cut.Find("button.playlist-entry-remove-button");
        await cut.InvokeAsync(() => removeButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));

        var cancelButton = cut.Find("#cancel-remove-continuewatching-button");
        await cut.InvokeAsync(() => cancelButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));

        playlistClientMock.Verify(c => c.RemoveMediaFromPlaylistAsync(1, "Movie", 60, true), Times.Never);
        Assert.Empty(cut.FindComponents<PlaylistEntryContinueWatchingConfirmationDialog>());
    }

    /// <summary>
    /// Renders <see cref="PlaylistEntriesList"/> with a single given entry (like <see cref="RenderWithEntry"/>),
    /// additionally returning the mocked <see cref="IPlaylistApiClient"/> so callers can set up/verify
    /// <c>RemoveMediaFromPlaylistAsync</c> expectations.
    /// </summary>
    /// <param name="entry">The single entry the mocked <c>RequestPlaylistEntriesPagedAsync</c> call returns.</param>
    /// <param name="Cut">(Return tuple field.) The rendered component.</param>
    /// <param name="PlaylistClientMock">(Return tuple field.) The mocked <see cref="IPlaylistApiClient"/> backing the component.</param>
    /// <returns>The rendered component and the mocked <see cref="IPlaylistApiClient"/> backing it.</returns>
    private static (global::Bunit.IRenderedComponent<PlaylistEntriesList> Cut, Mock<IPlaylistApiClient> PlaylistClientMock) RenderWithEntryAndMock(DtoPlaylistEntry entry)
    {
        var ctx = new global::Bunit.BunitContext();

        var playlistClientMock = new Mock<IPlaylistApiClient>();
        playlistClientMock
            .Setup(c => c.RequestPlaylistEntriesPagedAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DtoPlaylistEntriesPagedResult { Entries = new[] { entry }, HasNextPage = false, TotalCount = 1 });

        ctx.Services.AddSingleton<VideoWebPlayerClient>(new NoOpVideoWebPlayerClient());
        ctx.Services.AddSingleton(playlistClientMock.Object);
        ctx.Services.AddSingleton<ILogger<PlaylistEntriesList>>(NullLogger<PlaylistEntriesList>.Instance);
        ctx.Services.AddSingleton<ILogger<MediaSearchSelector>>(NullLogger<MediaSearchSelector>.Instance);

        var cut = ctx.Render<PlaylistEntriesList>(parameters => parameters
            .Add(p => p.PlaylistId, 1)
            .Add(p => p.IsManualMode, false));

        return (cut, playlistClientMock);
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
        var ctx = new global::Bunit.BunitContext();

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
