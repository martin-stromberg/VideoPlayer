using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Integration tests for <see cref="PlaylistService"/>'s cover management methods (Entwicklungsschritt 10,
/// Playlist-Abbildungen): upload validation and persistence (<see cref="PlaylistService.SetPlaylistCoverAsync"/>),
/// automatic regeneration (<see cref="PlaylistService.GeneratePlaylistCoverAsync"/>), retrieval
/// (<see cref="PlaylistService.GetPlaylistCoverAsync"/>), deletion (<see cref="PlaylistService.DeletePlaylistCoverAsync"/>)
/// and cover cleanup on playlist deletion.
/// </summary>
public class PlaylistServiceTests_Cover : PlaylistServiceTestBase
{
    [Fact]
    public async Task SetPlaylistCover_ValidUpload_SavedAsUserUploaded()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlist = await _service.CreatePlaylistAsync(_testUserId, "Playlist", null, null, ct);
        var jpegBytes = CreateJpegBytes();

        var pictureId = await _service.SetPlaylistCoverAsync(playlist.Id, _testUserId, jpegBytes, "image/jpeg", ct);

        var stored = await _db.Playlists.AsNoTracking().SingleAsync(p => p.Id == playlist.Id, ct);
        Assert.Equal(pictureId, stored.CoverPictureId);
        Assert.True(stored.CoverPictureIsUserUploaded);
        var picture = await _db.Pictures.AsNoTracking().SingleAsync(p => p.Id == pictureId, ct);
        Assert.False(picture.IsGeneratedBackground);
        Assert.Equal(playlist.Id, picture.PlaylistId);
    }

    [Fact]
    public async Task SetPlaylistCover_InvalidFormat_ThrowsInvalidOperationException()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlist = await _service.CreatePlaylistAsync(_testUserId, "Playlist", null, null, ct);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SetPlaylistCoverAsync(playlist.Id, _testUserId, new byte[] { 1, 2, 3 }, "image/bmp", ct));

        Assert.Contains("BMP wird nicht unterstützt", exception.Message);
    }

    [Fact]
    public async Task SetPlaylistCover_FileTooLarge_ThrowsInvalidOperationException()
    {
        var ct = TestContext.Current.CancellationToken;
        var service = CreateService(); // default MaxCoverImageSizeBytes (5 MB) via default PlaylistSettings
        var playlist = await service.CreatePlaylistAsync(_testUserId, "Playlist", null, null, ct);
        var tooLarge = new byte[6 * 1024 * 1024];

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SetPlaylistCoverAsync(playlist.Id, _testUserId, tooLarge, "image/jpeg", ct));
    }

    [Fact]
    public async Task SetPlaylistCover_UploadedReplacesGenerated_DeletesOldPicture()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlist = await _service.CreatePlaylistAsync(_testUserId, "Playlist", null, null, ct);
        var generatedPictureId = await CreateGeneratedPictureAsync(playlist.Id);
        await SetCoverDirectlyAsync(playlist.Id, generatedPictureId, isUserUploaded: false);

        var newPictureId = await _service.SetPlaylistCoverAsync(playlist.Id, _testUserId, CreateJpegBytes(), "image/jpeg", ct);

        var stored = await _db.Playlists.AsNoTracking().SingleAsync(p => p.Id == playlist.Id, ct);
        Assert.Equal(newPictureId, stored.CoverPictureId);
        Assert.True(stored.CoverPictureIsUserUploaded);
        Assert.NotEqual(generatedPictureId, newPictureId);
        Assert.False(await _db.Pictures.AsNoTracking().AnyAsync(p => p.Id == generatedPictureId, ct));
    }

    /// <summary>
    /// Regression test for the transaction-safety fix to <c>PlaylistService.ReplaceCoverPictureAsync</c>
    /// (Code-Review, Entwicklungsschritt 10): replacing an existing cover used to persist the newly saved
    /// picture and the playlist's updated <see cref="Playlist.CoverPictureId"/>/old-picture deletion via two
    /// separate <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> calls - if the second one failed
    /// (e.g. a dropped connection), the already-committed new picture would be permanently orphaned. Counting
    /// <see cref="DbContext.SavedChanges"/> invocations during a single replace verifies both steps are now
    /// persisted atomically in exactly one call.
    /// </summary>
    [Fact]
    public async Task SetPlaylistCover_ReplacingExistingCover_UsesSingleSaveChangesCall()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlist = await _service.CreatePlaylistAsync(_testUserId, "Playlist", null, null, ct);
        await _service.SetPlaylistCoverAsync(playlist.Id, _testUserId, CreateJpegBytes(), "image/jpeg", ct);

        var saveChangesCount = 0;
        void Handler(object? sender, SavedChangesEventArgs e) => saveChangesCount++;
        _db.SavedChanges += Handler;
        try
        {
            await _service.SetPlaylistCoverAsync(playlist.Id, _testUserId, CreateJpegBytes(), "image/jpeg", ct);
        }
        finally
        {
            _db.SavedChanges -= Handler;
        }

        Assert.Equal(1, saveChangesCount);
    }

    [Fact]
    public async Task SetPlaylistCover_UnauthorizedUser_ThrowsAccessDenied()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlist = await _service.CreatePlaylistAsync(_testUserId, "Playlist", null, null, ct);

        await Assert.ThrowsAsync<PlaylistAccessDeniedException>(
            () => _service.SetPlaylistCoverAsync(playlist.Id, _otherUserId, CreateJpegBytes(), "image/jpeg", ct));
    }

    [Fact]
    public async Task GeneratePlaylistCover_NoSourceImages_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlist = await _service.CreatePlaylistAsync(_testUserId, "Leer", null, null, ct);

        var pictureId = await _service.GeneratePlaylistCoverAsync(playlist.Id, _testUserId, cancellationToken: ct);

        Assert.Null(pictureId);
        var stored = await _db.Playlists.AsNoTracking().SingleAsync(p => p.Id == playlist.Id, ct);
        Assert.Null(stored.CoverPictureId);
    }

    [Fact]
    public async Task GeneratePlaylistCover_WithSourceImages_SavesGeneratedCover()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieId = await CreateMovieWithPosterAsync("Film");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movieId));

        var pictureId = await _service.GeneratePlaylistCoverAsync(playlistId, _testUserId, cancellationToken: ct);

        Assert.NotNull(pictureId);
        var stored = await _db.Playlists.AsNoTracking().SingleAsync(p => p.Id == playlistId, ct);
        Assert.Equal(pictureId, stored.CoverPictureId);
        Assert.False(stored.CoverPictureIsUserUploaded);
        var picture = await _db.Pictures.AsNoTracking().SingleAsync(p => p.Id == pictureId!.Value, ct);
        Assert.True(picture.IsGeneratedBackground);
        Assert.Equal("image/jpeg", picture.ContentType);
    }

    [Fact]
    public async Task GeneratePlaylistCover_AfterContentChange_ProducesNewPicture()
    {
        var ct = TestContext.Current.CancellationToken;
        var movie1 = await CreateMovieWithPosterAsync("Film 1");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movie1));

        var firstPictureId = await _service.GeneratePlaylistCoverAsync(playlistId, _testUserId, cancellationToken: ct);
        Assert.NotNull(firstPictureId);

        var movie2 = await CreateMovieWithPosterAsync("Film 2");
        await _service.AddMediaToPlaylistAsync(playlistId, _testUserId, MediaTypeValues.Movie, movie2, ct);

        var secondPictureId = await _service.GeneratePlaylistCoverAsync(playlistId, _testUserId, cancellationToken: ct);

        Assert.NotNull(secondPictureId);
        Assert.NotEqual(firstPictureId, secondPictureId);
        Assert.False(await _db.Pictures.AsNoTracking().AnyAsync(p => p.Id == firstPictureId!.Value, ct));
    }

    [Fact]
    public async Task GetPlaylistCover_CoverSet_ReturnsPicture()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlist = await _service.CreatePlaylistAsync(_testUserId, "Playlist", null, null, ct);
        var pictureId = await _service.SetPlaylistCoverAsync(playlist.Id, _testUserId, CreateJpegBytes(), "image/jpeg", ct);

        var picture = await _service.GetPlaylistCoverAsync(playlist.Id, ct);

        Assert.NotNull(picture);
        Assert.Equal(pictureId, picture!.Id);
    }

    [Fact]
    public async Task GetPlaylistCover_NoCoverSet_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlist = await _service.CreatePlaylistAsync(_testUserId, "Playlist", null, null, ct);

        var picture = await _service.GetPlaylistCoverAsync(playlist.Id, ct);

        Assert.Null(picture);
    }

    [Fact]
    public async Task DeletePlaylistCover_CoverSet_ClearsReferenceAndDeletesPicture()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlist = await _service.CreatePlaylistAsync(_testUserId, "Playlist", null, null, ct);
        var pictureId = await _service.SetPlaylistCoverAsync(playlist.Id, _testUserId, CreateJpegBytes(), "image/jpeg", ct);

        await _service.DeletePlaylistCoverAsync(playlist.Id, _testUserId, ct);

        var stored = await _db.Playlists.AsNoTracking().SingleAsync(p => p.Id == playlist.Id, ct);
        Assert.Null(stored.CoverPictureId);
        Assert.False(stored.CoverPictureIsUserUploaded);
        Assert.False(await _db.Pictures.AsNoTracking().AnyAsync(p => p.Id == pictureId, ct));
    }

    [Fact]
    public async Task DeletePlaylistAsync_WithCover_DeletesCoverPicture()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlist = await _service.CreatePlaylistAsync(_testUserId, "Playlist", null, null, ct);
        var pictureId = await _service.SetPlaylistCoverAsync(playlist.Id, _testUserId, CreateJpegBytes(), "image/jpeg", ct);

        await _service.DeletePlaylistAsync(playlist.Id, _testUserId, ct);

        Assert.False(await _db.Pictures.AsNoTracking().AnyAsync(p => p.Id == pictureId, ct));
    }

    /// <summary>
    /// Regression test for Abnahme-Abweichung 1 (Nachbesserungsrunde 1, Schritt 10): "Neu erzeugen"
    /// replaced an uploaded cover without any confirmation, contradicting "Ein hochgeladenes Bild hat
    /// immer Vorrang".
    /// </summary>
    [Fact]
    public async Task GeneratePlaylistCover_UploadedCoverWithoutConfirmation_ThrowsAndKeepsUploadedCover()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieId = await CreateMovieWithPosterAsync("Film");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movieId));
        var uploadedPictureId = await _service.SetPlaylistCoverAsync(playlistId, _testUserId, CreateJpegBytes(), "image/jpeg", ct);

        await Assert.ThrowsAsync<UploadedCoverReplacementConfirmationRequiredException>(
            () => _service.GeneratePlaylistCoverAsync(playlistId, _testUserId, cancellationToken: ct));

        var stored = await _db.Playlists.AsNoTracking().SingleAsync(p => p.Id == playlistId, ct);
        Assert.Equal(uploadedPictureId, stored.CoverPictureId);
        Assert.True(stored.CoverPictureIsUserUploaded);
        Assert.True(await _db.Pictures.AsNoTracking().AnyAsync(p => p.Id == uploadedPictureId, ct));
    }

    [Fact]
    public async Task GeneratePlaylistCover_UploadedCoverWithConfirmation_ReplacesWithGeneratedCover()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieId = await CreateMovieWithPosterAsync("Film");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movieId));
        var uploadedPictureId = await _service.SetPlaylistCoverAsync(playlistId, _testUserId, CreateJpegBytes(), "image/jpeg", ct);

        var newPictureId = await _service.GeneratePlaylistCoverAsync(playlistId, _testUserId, confirmReplaceUploadedCover: true, cancellationToken: ct);

        Assert.NotNull(newPictureId);
        var stored = await _db.Playlists.AsNoTracking().SingleAsync(p => p.Id == playlistId, ct);
        Assert.Equal(newPictureId, stored.CoverPictureId);
        Assert.False(stored.CoverPictureIsUserUploaded);
        Assert.False(await _db.Pictures.AsNoTracking().AnyAsync(p => p.Id == uploadedPictureId, ct));
    }

    [Fact]
    public async Task GeneratePlaylistCover_GeneratedCoverWithoutConfirmation_ReplacesWithoutPrompt()
    {
        var ct = TestContext.Current.CancellationToken;
        var movieId = await CreateMovieWithPosterAsync("Film");
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movieId));
        var firstPictureId = await _service.GeneratePlaylistCoverAsync(playlistId, _testUserId, cancellationToken: ct);

        var secondPictureId = await _service.GeneratePlaylistCoverAsync(playlistId, _testUserId, cancellationToken: ct);

        Assert.NotNull(secondPictureId);
        Assert.NotEqual(firstPictureId, secondPictureId);
    }

    [Fact]
    public async Task GeneratePlaylistCover_UploadedCoverButNoSourceImages_ReturnsNullWithoutPromptAndKeepsCover()
    {
        var ct = TestContext.Current.CancellationToken;
        var playlist = await _service.CreatePlaylistAsync(_testUserId, "Leer", null, null, ct);
        var uploadedPictureId = await _service.SetPlaylistCoverAsync(playlist.Id, _testUserId, CreateJpegBytes(), "image/jpeg", ct);

        var result = await _service.GeneratePlaylistCoverAsync(playlist.Id, _testUserId, cancellationToken: ct);

        Assert.Null(result);
        var stored = await _db.Playlists.AsNoTracking().SingleAsync(p => p.Id == playlist.Id, ct);
        Assert.Equal(uploadedPictureId, stored.CoverPictureId);
        Assert.True(stored.CoverPictureIsUserUploaded);
    }

    private async Task<long> CreateMovieWithPosterAsync(string name)
    {
        var picture = new Picture { Type = "poster", Data = CreateJpegBytes(), ContentType = "image/jpeg" };
        _db.Pictures.Add(picture);
        await _db.SaveChangesAsync();

        var movie = new Movie { Name = name, MediaSourceId = 1, CreatedAt = DateTime.UtcNow, PosterPictureId = picture.Id };
        _db.Movies.Add(movie);
        await _db.SaveChangesAsync();
        return movie.Id;
    }

    private async Task<long> CreateGeneratedPictureAsync(long playlistId)
    {
        var picture = new Picture { Type = "cover", Data = CreateJpegBytes(), ContentType = "image/jpeg", IsGeneratedBackground = true, PlaylistId = playlistId };
        _db.Pictures.Add(picture);
        await _db.SaveChangesAsync();
        return picture.Id;
    }

    private async Task SetCoverDirectlyAsync(long playlistId, long pictureId, bool isUserUploaded)
    {
        var playlist = await _db.Playlists.SingleAsync(p => p.Id == playlistId);
        playlist.CoverPictureId = pictureId;
        playlist.CoverPictureIsUserUploaded = isUserUploaded;
        await _db.SaveChangesAsync();
    }

    private static byte[] CreateJpegBytes()
    {
        using var image = new Image<Rgba32>(8, 8, Color.Teal.ToPixel<Rgba32>());
        using var stream = new MemoryStream();
        image.SaveAsJpeg(stream);
        return stream.ToArray();
    }
}
