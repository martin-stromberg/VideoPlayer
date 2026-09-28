using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
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
            var before = await WaitForContinueWatchingEntryAsync(e => e.PlaylistId == playlistId && e.Entry.Id == doomedMovieId);
            Assert.Equal("Quellenloeschung-Playlist", before.PlaylistName);

            await DeleteMediaSourceAsync(MediaSourceId);

            var after = await WaitForContinueWatchingEntryAsync(e => e.PlaylistId == playlistId && e.Entry.Id == survivingMovieId);
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
        await WaitForContinueWatchingEntryAsync(e => e.PlaylistId == playlistId && e.Entry.Id == movieId);

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
    /// Creates a playlist holding the given movies in the given order (sorted by release date).
    /// </summary>
    /// <param name="name">The name of the playlist to create.</param>
    /// <param name="movieIds">The ids of the movies to add.</param>
    /// <returns>The id of the created playlist.</returns>
    private async Task<long> CreatePlaylistAsync(string name, params long[] movieIds)
    {
        var playlist = await Client.CreatePlaylistAsync(new DtoCreatePlaylistRequest
        {
            Name = name,
            SortMode = PlaylistSortModeValues.ByReleaseDate
        });
        foreach (var movieId in movieIds)
        {
            await Client.AddMediaToPlaylistAsync(playlist.Id,
                new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = movieId });
        }

        return playlist.Id;
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
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SessionToken);
        var response = await http.PostAsJsonAsync(
            "/api/continue-watching/progress",
            new { mediaType = "movie", mediaId = movieId, positionSeconds = 120, durationSeconds = 3600, playlistId },
            ct);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>
    /// Polls the continue-watching list until the expected entry appears (the server persists reported
    /// progress through <c>ContinueWatchingWorker</c>, a moment after answering the request).
    /// </summary>
    /// <param name="predicate">Identifies the awaited entry.</param>
    /// <returns>The matching entry.</returns>
    private async Task<ContinueWatchingDto> WaitForContinueWatchingEntryAsync(Func<ContinueWatchingDto, bool> predicate)
    {
        var ct = TestContext.Current.CancellationToken;
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            var match = (await Client.RequestContinueWatchingAsync()).FirstOrDefault(predicate);
            if (match is not null)
                return match;

            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Der erwartete Weiterschauen-Eintrag ist nicht erschienen.");

            await Task.Delay(100, ct);
        }
    }

    /// <summary>
    /// Resolves the id of a classified movie by the title its NFO carries.
    /// </summary>
    /// <param name="name">The movie title.</param>
    /// <returns>The movie's id.</returns>
    private Task<long> GetMovieIdAsync(string name)
        => ReadDatabaseAsync(db => db.Movies.AsNoTracking().Where(m => m.Name == name).Select(m => m.Id)
            .SingleAsync(TestContext.Current.CancellationToken));
}
