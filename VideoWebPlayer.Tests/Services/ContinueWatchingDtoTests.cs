using System.Security.Claims;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests für die Befüllung von <c>ContinueWatchingDto.PlaylistId</c> und <c>ContinueWatchingDto.PlaylistName</c>
/// durch <see cref="VideoWebPlayer.Services.ContinueWatchingService.GetListAsync"/>.
/// </summary>
public sealed class ContinueWatchingDtoTests : ContinueWatchingServiceTestBase
{
    [Fact]
    public async Task DTO_PlaylistIdAndName_PopulatedFromDb()
    {
        var ct = TestContext.Current.CancellationToken;
        var movie = await CreateMovieAsync("Movie");
        var playlist = await CreateTestPlaylistAsync(_testUserId, "Meine Playlist");

        _db.ContinueWatchingEntries.Add(new ContinueWatchingEntry
        {
            UserId = _testUserId,
            MovieId = movie.Id,
            PlaylistId = playlist.Id,
            Position = TimeSpan.FromMinutes(5),
            UpdatedAt = DateTime.UtcNow,
            ListOrder = 1
        });
        await _db.SaveChangesAsync(ct);

        var list = await _service.GetListAsync(new ClaimsPrincipal(), ct);

        var dto = Assert.Single(list);
        Assert.Equal(playlist.Id, dto.PlaylistId);
        Assert.Equal("Meine Playlist", dto.PlaylistName);
    }

    [Fact]
    public async Task DTO_PlaylistIdNull_NameNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var movie = await CreateMovieAsync("Movie");

        _db.ContinueWatchingEntries.Add(new ContinueWatchingEntry
        {
            UserId = _testUserId,
            MovieId = movie.Id,
            Position = TimeSpan.FromMinutes(5),
            UpdatedAt = DateTime.UtcNow,
            ListOrder = 1
        });
        await _db.SaveChangesAsync(ct);

        var list = await _service.GetListAsync(new ClaimsPrincipal(), ct);

        var dto = Assert.Single(list);
        Assert.Null(dto.PlaylistId);
        Assert.Null(dto.PlaylistName);
    }

    [Fact]
    public async Task DTO_PlaylistEntryId_PopulatedWhenMediaStillInPlaylist()
    {
        var ct = TestContext.Current.CancellationToken;
        var movie = await CreateMovieAsync("Movie");
        var playlist = await CreateTestPlaylistAsync(_testUserId, "Meine Playlist");
        var playlistEntry = await CreateTestPlaylistEntryAsync(playlist.Id, movie.Id, MediaTypeValues.Movie);

        _db.ContinueWatchingEntries.Add(new ContinueWatchingEntry
        {
            UserId = _testUserId,
            MovieId = movie.Id,
            PlaylistId = playlist.Id,
            Position = TimeSpan.FromMinutes(5),
            UpdatedAt = DateTime.UtcNow,
            ListOrder = 1
        });
        await _db.SaveChangesAsync(ct);

        var list = await _service.GetListAsync(new ClaimsPrincipal(), ct);

        var dto = Assert.Single(list);
        Assert.Equal(playlistEntry.Id, dto.PlaylistEntryId);
    }

    [Fact]
    public async Task DTO_PlaylistEntryId_NullWhenMediaNoLongerInPlaylist()
    {
        var ct = TestContext.Current.CancellationToken;
        var movie = await CreateMovieAsync("Movie");
        // Playlist existiert, enthaelt aber KEINEN PlaylistEntry fuer dieses Medium (z.B. zwischenzeitlich
        // aus der Playlist entfernt) -> PlaylistEntryId muss null bleiben, kein Fehler.
        var playlist = await CreateTestPlaylistAsync(_testUserId, "Meine Playlist");

        _db.ContinueWatchingEntries.Add(new ContinueWatchingEntry
        {
            UserId = _testUserId,
            MovieId = movie.Id,
            PlaylistId = playlist.Id,
            Position = TimeSpan.FromMinutes(5),
            UpdatedAt = DateTime.UtcNow,
            ListOrder = 1
        });
        await _db.SaveChangesAsync(ct);

        var list = await _service.GetListAsync(new ClaimsPrincipal(), ct);

        var dto = Assert.Single(list);
        Assert.Equal(playlist.Id, dto.PlaylistId);
        Assert.Null(dto.PlaylistEntryId);
    }
}
