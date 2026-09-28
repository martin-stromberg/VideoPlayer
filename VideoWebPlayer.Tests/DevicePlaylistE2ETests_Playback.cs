using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// A6, Teil 2: ein gekoppeltes Gerät startet eine Playlist-Wiedergabe, holt den nächsten Titel und meldet
/// Fortschritt MIT Playlist-Bezug; der Eintrag taucht danach in der Weiterschauen-Liste des Anwenders mit
/// dem Namen der Playlist auf. Die Fortschrittsmeldung geht bewusst als echter HTTP-Aufruf an
/// <c>POST api/continue-watching/progress</c>: die Client-Bibliothek kennt zwar
/// <c>ReportPlaybackProgressAsync</c>, kann dort aber keine <c>playlistId</c> mitgeben (siehe Bericht).
/// </summary>
[Trait("Category", "E2E")]
public sealed class DevicePlaylistE2ETests_Playback : DeviceClientTestBase
{
    [Fact]
    public async Task PairedDevice_ReportsProgressFromPlaylist_ContinueWatchingCarriesPlaylistName()
    {
        var (user, payload) = await CreateUserAndPairDeviceAsync($"geraet-wiedergabe-{Guid.NewGuid():N}@test.com");
        var (firstMovieId, secondMovieId) = await SeedTwoAccessibleMoviesAsync(user.Id);
        // Wie bei eingelesenen Beständen: jeder Film gehört zu einer Filmsammlung (siehe
        // PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads).
        await AssignMovieCollectionAsync(firstMovieId, secondMovieId);
        var (playlistId, firstEntryId, secondEntryId) =
            await CreatePlaylistWithTwoMoviesAsync("Wiedergabe-Playlist", firstMovieId, secondMovieId);

        var start = await Client.StartPlaylistAsync(playlistId, entryId: null);
        Assert.Equal(firstEntryId, start.CurrentEntryId);
        Assert.Equal(firstMovieId, start.MediaId);
        Assert.Equal($"/api/items/movie/{firstMovieId}/stream", start.StreamUrl);

        var reported = await ReportProgressAsync(payload.Token, firstMovieId, playlistId);
        Assert.Equal(HttpStatusCode.NoContent, reported.StatusCode);

        var continueWatching = await WaitForContinueWatchingEntryAsync(e => e.PlaylistId == playlistId);
        Assert.Equal("Wiedergabe-Playlist", continueWatching.PlaylistName);
        Assert.Equal(firstMovieId, continueWatching.Entry.Id);
        Assert.Equal(firstEntryId, continueWatching.PlaylistEntryId);
        Assert.Equal(120, continueWatching.PositionSeconds);

        var next = await Client.GetNextPlaylistEntryAsync(playlistId, firstEntryId);
        Assert.NotNull(next);
        Assert.Equal(secondEntryId, next!.Entry.Id);
        Assert.Equal(secondMovieId, next.Entry.MediaId);
    }

    [Fact]
    public async Task PairedDevice_ReportsProgressForUnknownPlaylist_IsRefusedAsNotFound()
    {
        var (user, payload) = await CreateUserAndPairDeviceAsync($"geraet-wiedergabe-unbekannt-{Guid.NewGuid():N}@test.com");
        var (firstMovieId, _) = await SeedTwoAccessibleMoviesAsync(user.Id);

        var reported = await ReportProgressAsync(payload.Token, firstMovieId, playlistId: 999_999);

        Assert.Equal(HttpStatusCode.NotFound, reported.StatusCode);
    }

    /// <summary>
    /// BEFUND (nicht in diesem Lauf behoben, deshalb übersprungen): Gehört der Film zu KEINER Filmsammlung,
    /// beantwortet <c>GET api/continue-watching</c> die Anfrage mit HTTP 500 statt mit der Liste. Ursache ist
    /// <c>ContinueWatchingService.GetListAsync</c> (VideoWebPlayer/Services/ContinueWatchingService.cs:122):
    /// <c>Create&lt;DtoMovieCollection&gt;(_db.MovieCollections.Where(...).FirstOrDefault())</c> übergibt
    /// <c>null</c> an <c>Create&lt;T&gt;</c> (ebenda, Zeile 78), das sofort <c>ms.GetType()</c> aufruft —
    /// <see cref="NullReferenceException"/>. Erwartet: HTTP 200 mit dem Weiterschauen-Eintrag. Tatsächlich:
    /// HTTP 500 (Fehlerseite). Zum Nachstellen genügt es, das <c>Skip</c> zu entfernen.
    /// </summary>
    [Fact(Skip = "Befund: api/continue-watching antwortet mit 500, wenn ein Film zu keiner Filmsammlung gehoert (ContinueWatchingService.Create ohne Null-Pruefung). Wird laut Auftrag in diesem Lauf nicht behoben.")]
    public async Task PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads()
    {
        var (user, payload) = await CreateUserAndPairDeviceAsync($"geraet-ohne-sammlung-{Guid.NewGuid():N}@test.com");
        var (firstMovieId, secondMovieId) = await SeedTwoAccessibleMoviesAsync(user.Id);
        var (playlistId, firstEntryId, _) =
            await CreatePlaylistWithTwoMoviesAsync("Playlist-ohne-Filmsammlung", firstMovieId, secondMovieId);

        Assert.Equal(HttpStatusCode.NoContent, (await ReportProgressAsync(payload.Token, firstMovieId, playlistId)).StatusCode);

        var continueWatching = await WaitForContinueWatchingEntryAsync(e => e.PlaylistId == playlistId);

        Assert.Equal("Playlist-ohne-Filmsammlung", continueWatching.PlaylistName);
        Assert.Equal(firstEntryId, continueWatching.PlaylistEntryId);
    }

    /// <summary>
    /// Puts both movies into one movie collection, the way the scanner does for every directory it reads.
    /// </summary>
    /// <param name="movieIds">The ids of the movies to group into a collection.</param>
    private async Task AssignMovieCollectionAsync(params long[] movieIds)
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var movies = await db.Movies.Where(m => movieIds.Contains(m.Id)).ToListAsync(ct);
        var collection = new MovieCollection
        {
            Name = "Geräte-Filmsammlung",
            MediaSourceId = movies[0].MediaSourceId,
            CreatedAt = DateTime.UtcNow
        };
        db.MovieCollections.Add(collection);
        await db.SaveChangesAsync(ct);

        foreach (var movie in movies)
            movie.MovieCollectionId = collection.Id;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Reports playback progress the way a paired device does: bearer token of the device session, media
    /// type and id of the running title, and the id of the playlist it is being played from.
    /// </summary>
    /// <param name="bearerToken">The device session's JWT.</param>
    /// <param name="movieId">The id of the movie the progress belongs to.</param>
    /// <param name="playlistId">The id of the playlist the title is played from.</param>
    /// <returns>The server's answer.</returns>
    private async Task<HttpResponseMessage> ReportProgressAsync(string bearerToken, long movieId, long playlistId)
    {
        var ct = TestContext.Current.CancellationToken;
        using var http = CreateHttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        return await http.PostAsJsonAsync(
            "/api/continue-watching/progress",
            new
            {
                mediaType = "movie",
                mediaId = movieId,
                positionSeconds = 120,
                durationSeconds = 3600,
                playlistId
            },
            ct);
    }

    /// <summary>
    /// Polls the continue-watching list of the device's user until the expected entry appears: the server
    /// buffers reported progress and persists it in <c>ContinueWatchingWorker</c>, so the entry shows up a
    /// moment after the request was answered.
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
}
