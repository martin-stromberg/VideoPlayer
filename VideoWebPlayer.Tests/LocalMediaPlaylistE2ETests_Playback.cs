using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Authentication;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// A7, Teil 1: ein Titel aus einer Medienquelle vom Typ "lokales Verzeichnis" wird einer Playlist
/// hinzugefügt und aus der Playlist heraus abgespielt. Dass wirklich aus der lokalen Datei gelesen wird
/// (und nicht über SFTP), belegt der Vergleich der ausgelieferten Bytes mit dem Inhalt der Datei auf der
/// Platte. Der Fehlerfall gehört dazu: ohne Zugriff auf die Quelle wird derselbe Playlist-Titel abgelehnt.
/// </summary>
[Trait("Category", "E2E")]
[Collection(MediaSourceClassifierCollection.Name)]
public sealed class LocalMediaPlaylistE2ETests_Playback : LocalMediaPlaylistE2ETestBase
{
    [Fact]
    public async Task LocalDirectoryTitleInPlaylist_IsPlayedFromTheLocalFile()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieDirectory = Path.Combine(RootDirectory, "Filme");
        var firstBytes = WriteMovieFiles(movieDirectory, "film1", "Lokaler Film 1", releaseYear: 2001);
        WriteMovieFiles(movieDirectory, "film2", "Lokaler Film 2", releaseYear: 2002);
        await ScanEverythingAsync();

        var firstMovieId = await GetMovieIdAsync("Lokaler Film 1");
        var secondMovieId = await GetMovieIdAsync("Lokaler Film 2");

        var playlist = await Client.CreatePlaylistAsync(new DtoCreatePlaylistRequest
        {
            Name = "Lokale-Playlist",
            SortMode = PlaylistSortModeValues.ByReleaseDate
        });
        var firstEntry = await Client.AddMediaToPlaylistAsync(playlist.Id,
            new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = firstMovieId });
        var secondEntry = await Client.AddMediaToPlaylistAsync(playlist.Id,
            new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = secondMovieId });

        var start = await Client.StartPlaylistAsync(playlist.Id, firstEntry.TopLevelEntry!.Id);
        Assert.Equal(firstMovieId, start.MediaId);
        Assert.Equal($"/api/items/movie/{firstMovieId}/stream", start.StreamUrl);

        using var http = CreateHttpClient();
        var stream = await http.GetAsync($"{start.StreamUrl}?access_token={SessionToken}", ct);

        Assert.Equal(HttpStatusCode.OK, stream.StatusCode);
        Assert.Equal(firstBytes, await stream.Content.ReadAsByteArrayAsync(ct));

        var next = await Client.GetNextPlaylistEntryAsync(playlist.Id, firstEntry.TopLevelEntry.Id);
        Assert.NotNull(next);
        Assert.Equal(secondEntry.TopLevelEntry!.Id, next!.Entry.Id);
        Assert.Equal(secondMovieId, next.Entry.MediaId);
    }

    [Fact]
    public async Task LocalDirectoryTitleInPlaylist_WithoutSourceAccess_IsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        WriteMovieFiles(Path.Combine(RootDirectory, "Filme"), "film1", "Gesperrter lokaler Film", releaseYear: 2001);
        await ScanEverythingAsync();

        var movieId = await GetMovieIdAsync("Gesperrter lokaler Film");
        var playlist = await Client.CreatePlaylistAsync(new DtoCreatePlaylistRequest
        {
            Name = "Lokale-Playlist-ohne-Zugriff",
            SortMode = PlaylistSortModeValues.ByReleaseDate
        });
        await Client.AddMediaToPlaylistAsync(playlist.Id,
            new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = movieId });

        var strangerToken = await CreateUserWithoutSourceAccessAsync();
        using var http = CreateHttpClient();
        var stream = await http.GetAsync($"/api/items/movie/{movieId}/stream?access_token={strangerToken}", ct);

        Assert.Equal(HttpStatusCode.Forbidden, stream.StatusCode);
    }

    /// <summary>
    /// Resolves the id of a classified movie by the title its NFO carries.
    /// </summary>
    /// <param name="name">The movie title.</param>
    /// <returns>The movie's id.</returns>
    private Task<long> GetMovieIdAsync(string name)
        => ReadDatabaseAsync(db => db.Movies.AsNoTracking().Where(m => m.Name == name).Select(m => m.Id)
            .SingleAsync(TestContext.Current.CancellationToken));

    /// <summary>
    /// Creates a second user who has no access to the local media source, for the refusal case.
    /// </summary>
    /// <returns>That user's bearer token.</returns>
    private async Task<string> CreateUserWithoutSourceAccessAsync()
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var tokenService = scope.ServiceProvider.GetRequiredService<AuthorizationTokenService>();

        var email = $"ohne-zugriff-{Guid.NewGuid():N}@test.com";
        var stranger = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        var created = await userManager.CreateAsync(stranger);
        Assert.True(created.Succeeded, string.Join(Environment.NewLine, created.Errors.Select(e => e.Description)));

        return tokenService.CreateToken(stranger).token;
    }
}
