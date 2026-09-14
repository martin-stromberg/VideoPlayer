using Microsoft.AspNetCore.Mvc;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Controllers;

/// <summary>
/// Controller-level tests for the Entwicklungsschritt-7 Sicherheitsabfrage on
/// <c>PlaylistsController.RemoveMediaFromPlaylist</c>: a continue-watching (Weiterschauen) entry bound to
/// the same playlist must turn into a 409 Conflict response (<see cref="DtoRemovePlaylistEntryConflictResponse"/>)
/// unless <c>confirmContinueWatchingRemoval</c> is set, mirroring the existing sort-mode-change confirmation
/// tests. Uses <see cref="PlaylistsControllerTestBase.UseControllerWithContinueWatchingResolution"/> to wire
/// a real <c>ContinueWatchingService</c>, since the base class's default controller does not perform this
/// resolution at all.
/// </summary>
public class PlaylistsControllerTests_RemoveMediaWithContinueWatching : PlaylistsControllerTestBase
{
    [Fact]
    public async Task RemoveMediaFromPlaylist_WithContinueWatchingReference_WithoutConfirmation_Returns409Conflict()
    {
        UseControllerWithContinueWatchingResolution();
        var movieId = await CreateMovieAsync();
        var playlistId = await CreatePlaylistAsync();
        await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = movieId });
        await AddContinueWatchingEntryAsync(movieId, playlistId);

        var result = await _controller.RemoveMediaFromPlaylist(playlistId, MediaTypeValues.Movie, movieId);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var response = Assert.IsType<DtoRemovePlaylistEntryConflictResponse>(conflict.Value);
        Assert.True(response.IsContinueWatchingConfirmationRequired);
    }

    [Fact]
    public async Task RemoveMediaFromPlaylist_WithContinueWatchingReference_Confirmed_Returns204NoContent()
    {
        UseControllerWithContinueWatchingResolution();
        var movieId = await CreateMovieAsync();
        var playlistId = await CreatePlaylistAsync();
        await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = movieId });
        await AddContinueWatchingEntryAsync(movieId, playlistId);

        var result = await _controller.RemoveMediaFromPlaylist(playlistId, MediaTypeValues.Movie, movieId, confirmContinueWatchingRemoval: true);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task RemoveMediaFromPlaylist_WithoutContinueWatchingReference_Returns204NoContentWithoutConfirmation()
    {
        UseControllerWithContinueWatchingResolution();
        var movieId = await CreateMovieAsync();
        var playlistId = await CreatePlaylistAsync();
        await _controller.AddMediaToPlaylist(playlistId, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = movieId });

        var result = await _controller.RemoveMediaFromPlaylist(playlistId, MediaTypeValues.Movie, movieId);

        Assert.IsType<NoContentResult>(result);
    }

    private async Task AddContinueWatchingEntryAsync(long movieId, long playlistId)
    {
        _db.ContinueWatchingEntries.Add(new ContinueWatchingEntry
        {
            UserId = _user.Id,
            MovieId = movieId,
            PlaylistId = playlistId,
            Position = TimeSpan.FromMinutes(1),
            UpdatedAt = DateTime.UtcNow,
            ListOrder = DateTime.UtcNow.Ticks
        });
        await _db.SaveChangesAsync();
    }
}
