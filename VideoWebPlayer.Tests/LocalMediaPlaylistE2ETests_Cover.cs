using System.Net;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// A7, Teil 3: das Titelbild einer Playlist entsteht aus den Bildern lokaler Inhalte. Belegt wird beides —
/// dass die Bilddaten wirklich aus den Dateien des lokalen Verzeichnisses stammen (Byte-Vergleich mit der
/// Datei auf der Platte) und dass daraus eine abrufbare Collage wird. Ohne Bilder im Verzeichnis meldet die
/// Erzeugung ehrlich "keine Bilder verfügbar", statt ein leeres Titelbild zu speichern.
/// </summary>
[Trait("Category", "E2E")]
[Collection(MediaSourceClassifierCollection.Name)]
public sealed class LocalMediaPlaylistE2ETests_Cover : LocalMediaPlaylistE2ETestBase
{
    [Fact]
    public async Task PlaylistCover_IsGeneratedFromThePostersOfTheLocalDirectory()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieDirectory = Path.Combine(RootDirectory, "Filme");
        WriteMovieFiles(movieDirectory, "film1", "Bild-Film 1", withPoster: true, releaseYear: 2001);
        WriteMovieFiles(movieDirectory, "film2", "Bild-Film 2", withPoster: true, releaseYear: 2002);
        await ScanEverythingAsync();

        // Die Bilddaten stammen wirklich aus der lokalen Datei und nicht aus einer anderen Quelle.
        var posterData = await ReadDatabaseAsync(db => db.Movies.AsNoTracking()
            .Where(m => m.Name == "Bild-Film 1")
            .Join(db.Pictures.AsNoTracking(), m => m.PosterPictureId, p => p.Id, (_, p) => p.Data)
            .SingleAsync(ct));
        Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(movieDirectory, "film1-poster.png"), ct), posterData);

        var playlistId = await CreatePlaylistWithLocalMoviesAsync("Titelbild-Playlist");

        var result = await Client.RegeneratePlaylistCoverAsync(playlistId);

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.PictureId);

        using var http = CreateHttpClient();
        var response = await http.GetAsync($"/api/playlists/{playlistId}/cover?access_token={SessionToken}", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        Assert.True(bytes.Length > 2 && bytes[0] == 0xFF && bytes[1] == 0xD8, $"Kein JPEG, {bytes.Length} Bytes empfangen.");
    }

    [Fact]
    public async Task PlaylistCover_WithoutImagesInTheLocalDirectory_ReportsNoImagesAvailable()
    {
        var movieDirectory = Path.Combine(RootDirectory, "Filme");
        WriteMovieFiles(movieDirectory, "film1", "Bild-Film 1", releaseYear: 2001);
        WriteMovieFiles(movieDirectory, "film2", "Bild-Film 2", releaseYear: 2002);
        await ScanEverythingAsync();

        var playlistId = await CreatePlaylistWithLocalMoviesAsync("Titelbild-Playlist-ohne-Bilder");

        var result = await Client.RegeneratePlaylistCoverAsync(playlistId);

        Assert.False(result.Success);
        Assert.Contains("Keine Bilder", result.Message);
        Assert.Null((await Client.RequestPlaylistAsync(playlistId))!.CoverPictureId);
    }

    /// <summary>
    /// Creates a playlist holding both scanned movies of the local directory.
    /// </summary>
    /// <param name="name">The name of the playlist to create.</param>
    /// <returns>The id of the created playlist.</returns>
    private async Task<long> CreatePlaylistWithLocalMoviesAsync(string name)
    {
        var ct = TestContext.Current.CancellationToken;
        var movieIds = await ReadDatabaseAsync(db => db.Movies.AsNoTracking()
            .Where(m => m.MediaSourceId == MediaSourceId)
            .OrderBy(m => m.Name)
            .Select(m => m.Id)
            .ToArrayAsync(ct));
        Assert.Equal(2, movieIds.Length);

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
}
