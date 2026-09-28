using System.Net;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// A7, Teil 4: wird eine lokale Medienquelle gelöscht, verschwindet ein Weiterschauen-Eintrag mit
/// Playlist-Bezug nicht einfach — er zeigt danach auf den nächsten noch vorhandenen Titel derselben
/// Playlist. Enthält die Playlist keinen solchen Titel mehr, wird der Eintrag entfernt; auch dieser Fall
/// steht hier.
/// </summary>
[Trait("Category", "E2E")]
[Collection(MediaSourceClassifierCollection.Name)]
public sealed class LocalMediaPlaylistE2ETests_SourceDeletion : LocalMediaPlaylistE2ETestBase
{
    [Fact]
    public async Task DeletingTheLocalSource_ReplacesThePlaylistBoundContinueWatchingEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        WriteMovieFiles(Path.Combine(RootDirectory, "Filme"), "film1", "Zu loeschender Film", releaseYear: 2001);
        var (secondDirectory, _) = await CreateSecondLocalSourceAsync("Zweite lokale Quelle");
        try
        {
            WriteMovieFiles(Path.Combine(secondDirectory, "Filme"), "film2", "Bleibender Film", releaseYear: 2002);
            await ScanEverythingAsync();

            var doomedMovieId = await GetMovieIdAsync("Zu loeschender Film");
            var survivingMovieId = await GetMovieIdAsync("Bleibender Film");
            var playlistId = await CreatePlaylistAsync("Quellenloeschung-Playlist", doomedMovieId, survivingMovieId);

            await ReportProgressAsync(doomedMovieId, playlistId);
            var before = await ContinueWatchingTestHelper.WaitForEntryAsync(Client, e => e.PlaylistId == playlistId && e.Entry.Id == doomedMovieId, ct);
            Assert.Equal("Quellenloeschung-Playlist", before.PlaylistName);

            await DeleteMediaSourceAsync(MediaSourceId);

            var after = await ContinueWatchingTestHelper.WaitForEntryAsync(Client, e => e.PlaylistId == playlistId && e.Entry.Id == survivingMovieId, ct);
            Assert.Equal(playlistId, after.PlaylistId);
            Assert.Equal("Quellenloeschung-Playlist", after.PlaylistName);
            Assert.False(await ReadDatabaseAsync(db => db.Movies.AsNoTracking().AnyAsync(m => m.Id == doomedMovieId, ct)));
        }
        finally
        {
            try { Directory.Delete(secondDirectory, recursive: true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException) { /* best effort */ }
        }
    }

    [Fact]
    public async Task DeletingTheLocalSource_WithoutAnyRemainingTitle_RemovesTheContinueWatchingEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieDirectory = Path.Combine(RootDirectory, "Filme");
        WriteMovieFiles(movieDirectory, "film1", "Einziger lokaler Film", releaseYear: 2001);
        await ScanEverythingAsync();

        var movieId = await GetMovieIdAsync("Einziger lokaler Film");
        var playlistId = await CreatePlaylistAsync("Quellenloeschung-Playlist-ohne-Ersatz", movieId);

        await ReportProgressAsync(movieId, playlistId);
        await ContinueWatchingTestHelper.WaitForEntryAsync(Client, e => e.PlaylistId == playlistId && e.Entry.Id == movieId, ct);

        await DeleteMediaSourceAsync(MediaSourceId);

        Assert.Empty(await Client.RequestContinueWatchingAsync());
        Assert.False(await ReadDatabaseAsync(db => db.Movies.AsNoTracking().AnyAsync(m => m.Id == movieId, ct)));
    }

    /// <summary>
    /// Deletes the media source through the administrative endpoint the admin UI uses, so the
    /// playlist-bound continue-watching resolution really runs (<c>AdminSourcesController.DeleteSource</c>).
    /// </summary>
    /// <param name="mediaSourceId">The id of the media source to delete.</param>
    private async Task DeleteMediaSourceAsync(long mediaSourceId)
    {
        var ct = TestContext.Current.CancellationToken;
        using var http = CreateHttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SessionToken);
        var response = await http.DeleteAsync($"/api/admin/sources/{mediaSourceId}", ct);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>
    /// Reports playback progress for a title played from a playlist.
    /// </summary>
    /// <param name="movieId">The id of the movie the progress belongs to.</param>
    /// <param name="playlistId">The id of the playlist the title is played from.</param>
    private async Task ReportProgressAsync(long movieId, long playlistId)
    {
        var ct = TestContext.Current.CancellationToken;
        using var http = CreateHttpClient();
        var response = await ContinueWatchingTestHelper.ReportProgressAsync(http, SessionToken, movieId, playlistId, ct);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
