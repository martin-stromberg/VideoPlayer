using System.Net;
using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.ClientLibrary;

/// <summary>
/// Automatic session renewal of the client library (A5): an expired session is renewed once and the
/// call repeated, parallel 401 answers share a single renewal, and the final answers 403 and 404 (A4)
/// trigger no renewal at all.
/// </summary>
public sealed class VideoWebPlayerClientTests_Reauthorization : DeviceClientTestBase
{
    private const string RefreshRoute = "/api/auth/refresh";

    [Fact]
    public async Task ExpiredSession_IsRenewedOnce_AndRequestRepeated()
    {
        var (user, _) = await CreateUserAndPairDeviceAsync($"reauth-{Guid.NewGuid():N}@test.com");
        var (firstMovieId, secondMovieId) = await SeedTwoAccessibleMoviesAsync(user.Id);
        await CreatePlaylistWithTwoMoviesAsync("Erneuerungs-Playlist", firstMovieId, secondMovieId);

        ExpireSession();
        var playlists = await Client.RequestPlaylistsAsync();

        Assert.Single(playlists);
        Assert.Equal(1, Requests.CountTo(RefreshRoute));
    }

    [Fact]
    public async Task ForbiddenAnswer_IsFinal_AndTriggersNoRenewal()
    {
        var (user, _) = await CreateUserAndPairDeviceAsync($"reauth-403-{Guid.NewGuid():N}@test.com");
        var lockedMovieId = await SeedLockedMovieAsync();
        var (accessibleMovieId, _) = await SeedTwoAccessibleMoviesAsync(user.Id);
        var playlist = await Client.CreatePlaylistAsync(new DtoCreatePlaylistRequest { Name = "Gesperrter-Titel-Playlist", SortMode = "ByReleaseDate" });
        await Client.AddMediaToPlaylistAsync(playlist.Id, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = accessibleMovieId });
        var locked = await Client.AddMediaToPlaylistAsync(playlist.Id, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = lockedMovieId });

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => Client.StartPlaylistAsync(playlist.Id, locked.TopLevelEntry!.Id));

        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
        Assert.Equal(0, Requests.CountTo(RefreshRoute));
    }

    [Fact]
    public async Task NotFoundAnswer_IsFinal_AndTriggersNoRenewal()
    {
        var (user, _) = await CreateUserAndPairDeviceAsync($"reauth-404-{Guid.NewGuid():N}@test.com");
        var (firstMovieId, secondMovieId) = await SeedTwoAccessibleMoviesAsync(user.Id);
        var (playlistId, firstEntryId, _) = await CreatePlaylistWithTwoMoviesAsync("Unbekannter-Eintrag-Playlist", firstMovieId, secondMovieId);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => Client.StartPlaylistAsync(playlistId, firstEntryId + 100000));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal(0, Requests.CountTo(RefreshRoute));
    }

    [Fact]
    public async Task ParallelRequestsAfterExpiry_RenewTheSessionOnlyOnce()
    {
        var (user, _) = await CreateUserAndPairDeviceAsync($"reauth-parallel-{Guid.NewGuid():N}@test.com");
        var (firstMovieId, secondMovieId) = await SeedTwoAccessibleMoviesAsync(user.Id);
        var (playlistId, _, _) = await CreatePlaylistWithTwoMoviesAsync("Parallel-Playlist", firstMovieId, secondMovieId);

        ExpireSession();
        var calls = Enumerable.Range(0, 5).Select(_ => Client.RequestPlaylistAsync(playlistId)).ToArray();
        var results = await Task.WhenAll(calls);

        Assert.All(results, playlist => Assert.NotNull(playlist));
        Assert.Equal(1, Requests.CountTo(RefreshRoute));
    }

    [Fact]
    public async Task AfterDeviceRevocation_ExpiredSessionFailsWithoutEndlessRetry()
    {
        var (user, payload) = await CreateUserAndPairDeviceAsync($"reauth-revoked-{Guid.NewGuid():N}@test.com");
        var (firstMovieId, secondMovieId) = await SeedTwoAccessibleMoviesAsync(user.Id);
        await CreatePlaylistWithTwoMoviesAsync("Widerruf-Playlist", firstMovieId, secondMovieId);

        await RevokeDeviceAsync(payload.DeviceToken);
        ExpireSession();
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => Client.RequestPlaylistsAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Equal(1, Requests.CountTo(RefreshRoute));
    }

    /// <summary>
    /// Adds a movie in a media source nobody has access to, so every attempt to play it is refused with 403.
    /// </summary>
    /// <returns>The id of the created movie.</returns>
    private async Task<long> SeedLockedMovieAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var source = new MediaSource { Name = $"Gesperrte Quelle {Guid.NewGuid()}", Path = "/locked", Host = "localhost", Port = 22, CreatedAt = DateTime.UtcNow };
        db.MediaSources.Add(source);
        await db.SaveChangesAsync();

        var movie = new Movie { Name = "Gesperrter Gerätefilm", MediaSourceId = source.Id, CreatedAt = DateTime.UtcNow };
        db.Movies.Add(movie);
        await db.SaveChangesAsync();
        return movie.Id;
    }
}
