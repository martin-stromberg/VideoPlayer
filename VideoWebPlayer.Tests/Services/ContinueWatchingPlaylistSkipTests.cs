using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// „Überspringen" in der Weiterschauen-Liste muss bei einem Eintrag mit Playlist-Bezug denselben
/// Nachfolger ermitteln wie das Erreichen der Endsequenz: den nächsten abspielbaren und zugänglichen
/// Titel der Playlist. Ohne Playlist-Bezug bleibt die Serien-/Sammlungsreihenfolge maßgeblich.
/// </summary>
public class ContinueWatchingPlaylistSkipTests : ContinueWatchingPlaylistTestBase
{
    /// <summary>
    /// Überspringen der letzten Episode der ersten Serie führt auf die erste Episode der zweiten Serie.
    /// </summary>
    [Fact]
    public async Task Skip_PlaylistEntry_ReplacesWithNextPlaylistTitle()
    {
        var ct = TestContext.Current.CancellationToken;
        var (playlistId, showA, showB) = await CreateTwoShowPlaylistAsync();

        var seeded = await SeedContinueWatchingEntryAsync(null, showA[1], playlistId, position: MidPosition);

        var result = await _continueWatching.SkipAsync(_testUserId, null, showA[1], playlistId, ct);

        Assert.Equal(ContinueWatchingService.SkipResult.Replaced, result);
        var entry = Assert.Single(await GetContinueWatchingEntriesAsync(playlistId, cancellationToken: ct));
        Assert.Equal(showB[0], entry.TVShowEpisodeId);
        Assert.Equal(seeded.ListOrder, entry.ListOrder);
    }

    /// <summary>
    /// Ein aus der Playlist entfernter Titel wird auch beim Überspringen übersprungen.
    /// </summary>
    [Fact]
    public async Task Skip_PlaylistEntry_SkipsTitleExcludedFromPlaylist()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, episodes) = await CreateShowAsync("Serie", 3);
        await GrantMediaSourceAccessForUserAsync(_testUserId);
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.TVShowEpisode, episodes[0], 0),
            (MediaTypeValues.TVShowEpisode, episodes[2], 1));

        await SeedContinueWatchingEntryAsync(null, episodes[0], playlistId, position: MidPosition);

        var result = await _continueWatching.SkipAsync(_testUserId, null, episodes[0], playlistId, ct);

        Assert.Equal(ContinueWatchingService.SkipResult.Replaced, result);
        Assert.Equal(episodes[2], Assert.Single(await GetContinueWatchingEntriesAsync(playlistId, cancellationToken: ct)).TVShowEpisodeId);
    }

    /// <summary>
    /// Am Ende der Playlist wird der Eintrag ersatzlos entfernt, auch wenn die Serie weitergeht.
    /// </summary>
    [Fact]
    public async Task Skip_LastPlaylistTitle_RemovesWithoutNext()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, episodes) = await CreateShowAsync("Serie", 3);
        await GrantMediaSourceAccessForUserAsync(_testUserId);
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.TVShowEpisode, episodes[0], 0),
            (MediaTypeValues.TVShowEpisode, episodes[1], 1));

        await SeedContinueWatchingEntryAsync(null, episodes[1], playlistId, position: MidPosition);

        var result = await _continueWatching.SkipAsync(_testUserId, null, episodes[1], playlistId, ct);

        Assert.Equal(ContinueWatchingService.SkipResult.RemovedWithoutNext, result);
        Assert.Empty(await GetContinueWatchingEntriesAsync(playlistId, cancellationToken: ct));
    }

    /// <summary>
    /// Ohne Playlist-Bezug bleibt das Überspringen an der Serienreihenfolge orientiert.
    /// </summary>
    [Fact]
    public async Task Skip_WithoutPlaylist_StillUsesSeriesOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, episodes) = await CreateShowAsync("Serie", 2);

        await SeedContinueWatchingEntryAsync(null, episodes[0], null, position: MidPosition);

        var result = await _continueWatching.SkipAsync(_testUserId, null, episodes[0], null, ct);

        Assert.Equal(ContinueWatchingService.SkipResult.Replaced, result);
        Assert.Equal(episodes[1], Assert.Single(await GetPlaylistlessEntriesAsync(ct)).TVShowEpisodeId);
    }
}
