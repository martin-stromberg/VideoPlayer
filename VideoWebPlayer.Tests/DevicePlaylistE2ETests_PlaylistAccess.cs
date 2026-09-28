using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// A6, Teil 1: was ein per QR-Bootstrap gekoppeltes Gerät von den Playlists seines Anwenders sieht —
/// die Liste samt Einträgen und das Titelbild, das ein Gerät (wie der Browser bei Bildern) mit dem
/// Anmeldenachweis als Abfrageparameter statt als Kopfzeile abruft. Der Ablauf läuft durchgehend über die
/// Client-Bibliothek (<see cref="VideoWebPlayer.Client.VideoWebPlayerClient"/>) gegen die echte Anwendung;
/// nur der Bildabruf geht bewusst über einen nackten <see cref="HttpClient"/> ohne
/// <c>Authorization</c>-Kopfzeile, weil genau das geprüft werden soll.
/// </summary>
[Trait("Category", "E2E")]
public sealed class DevicePlaylistE2ETests_PlaylistAccess : DeviceClientTestBase
{
    [Fact]
    public async Task PairedDevice_RequestPlaylists_ReturnsOwnPlaylistWithItsEntries()
    {
        var ct = TestContext.Current.CancellationToken;
        var (user, _) = await CreateUserAndPairDeviceAsync($"geraet-playlists-{Guid.NewGuid():N}@test.com");
        var (firstMovieId, secondMovieId) = await SeedTwoAccessibleMoviesAsync(user.Id);
        var (playlistId, firstEntryId, secondEntryId) =
            await CreatePlaylistWithTwoMoviesAsync("Geräte-Playlist", firstMovieId, secondMovieId);

        var playlists = (await Client.RequestPlaylistsAsync()).ToList();

        var playlist = Assert.Single(playlists, p => p.Id == playlistId);
        Assert.Equal("Geräte-Playlist", playlist.Name);
        Assert.True(playlist.IsOwner);

        var page = await Client.RequestPlaylistEntriesPagedAsync(playlistId, 1, 20, ct);
        Assert.Equal(new[] { firstEntryId, secondEntryId }, page.Entries.Select(e => e.Id).ToArray());
        Assert.All(page.Entries, entry => Assert.True(entry.IsAccessible));
    }

    [Fact]
    public async Task PairedDevice_RequestPlaylistCoverWithAccessTokenQuery_ReturnsGeneratedImage()
    {
        var ct = TestContext.Current.CancellationToken;
        var (user, payload) = await CreateUserAndPairDeviceAsync($"geraet-cover-{Guid.NewGuid():N}@test.com");
        var (firstMovieId, secondMovieId) = await SeedTwoAccessibleMoviesAsync(user.Id);
        await SeedPosterAsync(firstMovieId);
        var (playlistId, _, _) = await CreatePlaylistWithTwoMoviesAsync("Titelbild-Playlist", firstMovieId, secondMovieId);

        var generated = await Client.RegeneratePlaylistCoverAsync(playlistId);
        Assert.True(generated.Success, generated.Message);

        using var http = CreateHttpClient();
        var response = await http.GetAsync($"/api/playlists/{playlistId}/cover?access_token={payload.Token}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        // JPEG-Kennung: das Bild ist wirklich ein Bild und keine Fehlerseite.
        Assert.True(bytes.Length > 2 && bytes[0] == 0xFF && bytes[1] == 0xD8, $"Kein JPEG, {bytes.Length} Bytes empfangen.");
    }

    [Fact]
    public async Task PairedDevice_RequestPlaylistCoverWithoutCredential_IsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var (user, _) = await CreateUserAndPairDeviceAsync($"geraet-cover-anonym-{Guid.NewGuid():N}@test.com");
        var (firstMovieId, secondMovieId) = await SeedTwoAccessibleMoviesAsync(user.Id);
        await SeedPosterAsync(firstMovieId);
        var (playlistId, _, _) = await CreatePlaylistWithTwoMoviesAsync("Titelbild-Playlist-ohne-Nachweis", firstMovieId, secondMovieId);
        Assert.True((await Client.RegeneratePlaylistCoverAsync(playlistId)).Success);

        using var http = CreateHttpClient();
        var response = await http.GetAsync($"/api/playlists/{playlistId}/cover", ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Gives the movie a real poster picture, the only source the automatic cover collage draws from
    /// (<c>PlaylistCoverImageGenerator</c> collects the entries' poster pictures).
    /// </summary>
    /// <param name="movieId">The id of the movie to give a poster to.</param>
    private async Task SeedPosterAsync(long movieId)
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var picture = new Picture { Type = "poster", Data = TestImages.Png(200, 300), ContentType = "image/png" };
        db.Pictures.Add(picture);
        await db.SaveChangesAsync(ct);

        var movie = await db.Movies.FirstAsync(m => m.Id == movieId, ct);
        movie.PosterPictureId = picture.Id;
        await db.SaveChangesAsync(ct);
    }
}
