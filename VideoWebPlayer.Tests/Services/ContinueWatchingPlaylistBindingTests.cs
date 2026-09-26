using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Abnahme-Abweichung A2: Eine Fortschrittsmeldung mit <c>playlistId</c> für einen Titel, der kein Eintrag
/// dieser Playlist (mehr) ist, darf den einen Weiterschauen-Eintrag der Playlist nicht anfassen. Der Titel
/// wird dann wie ein Titel ohne Playlist geführt (<c>PlaylistId = null</c>) und fällt unter die normalen
/// Regeln ohne Playlist.
/// </summary>
public class ContinueWatchingPlaylistBindingTests : ContinueWatchingPlaylistTestBase
{
    /// <summary>
    /// Ablauf des Prüfers (Beleg <c>mangel2-*</c> / <c>rand-L-entfernt.txt</c>): E1 läuft aus der Playlist,
    /// wird über die Sicherheitsabfrage aus der Playlist entfernt (Ersatz E2 wird gesetzt), der Player meldet
    /// danach weiter Fortschritt für E1 mit Playlist-Bezug. Der Ersatz muss bestehen bleiben.
    /// </summary>
    [Fact]
    public async Task ProgressForTitleRemovedFromPlaylist_KeepsReplacementAndBindsReportWithoutPlaylist()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, episodes) = await CreateShowAsync("Serie", 3);
        await GrantMediaSourceAccessForUserAsync(_testUserId);
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.TVShowEpisode, episodes[0], 0),
            (MediaTypeValues.TVShowEpisode, episodes[1], 1),
            (MediaTypeValues.TVShowEpisode, episodes[2], 2));

        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, episodes[0], MidPosition, Duration, playlistId, ct);

        await _playlists.RemoveMediaFromPlaylistAsync(playlistId, _testUserId, MediaTypeValues.TVShowEpisode, episodes[0],
            confirmContinueWatchingRemoval: true, cancellationToken: ct);

        // Der Ersatz steht nach dem Entfernen korrekt auf E2 (BR-17).
        Assert.Equal(episodes[1], Assert.Single(await GetContinueWatchingEntriesAsync(playlistId, cancellationToken: ct)).TVShowEpisodeId);

        // Der Player laeuft weiter und meldet erneut Fortschritt fuer den entfernten Titel MIT playlistId.
        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, episodes[0], TimeSpan.FromMinutes(11), Duration, playlistId, ct);

        // Der eine Eintrag der Playlist bleibt der Ersatz ...
        var playlistEntry = Assert.Single(await GetContinueWatchingEntriesAsync(playlistId, cancellationToken: ct));
        Assert.Equal(episodes[1], playlistEntry.TVShowEpisodeId);

        // ... und der entfernte Titel wird ohne Playlist-Bezug gefuehrt.
        var freeEntry = Assert.Single(await GetPlaylistlessEntriesAsync(ct));
        Assert.Equal(episodes[0], freeEntry.TVShowEpisodeId);
        Assert.Null(freeEntry.PlaylistId);
        Assert.Equal(TimeSpan.FromMinutes(11), freeEntry.Position);
    }

    /// <summary>
    /// Derselbe Ablauf über den produktiven Weg <c>ReportProgressAsync</c> + Puffer-Flush: Auch der
    /// Pufferschlüssel muss bereits ohne Playlist-Bezug gebildet werden.
    /// </summary>
    [Fact]
    public async Task ProgressForTitleRemovedFromPlaylist_ViaReportProgressAndBufferFlush_KeepsReplacement()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, episodes) = await CreateShowAsync("Serie", 3);
        await GrantMediaSourceAccessForUserAsync(_testUserId);
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.TVShowEpisode, episodes[0], 0),
            (MediaTypeValues.TVShowEpisode, episodes[1], 1),
            (MediaTypeValues.TVShowEpisode, episodes[2], 2));
        var user = await _db.Users.SingleAsync(u => u.Id == _testUserId, ct);

        await _continueWatching.ReportProgressAsync(user, null, episodes[0], MidPosition, Duration, playlistId, ct);
        await FlushBufferAsync(1, ct);

        await _playlists.RemoveMediaFromPlaylistAsync(playlistId, _testUserId, MediaTypeValues.TVShowEpisode, episodes[0],
            confirmContinueWatchingRemoval: true, cancellationToken: ct);

        await _continueWatching.ReportProgressAsync(user, null, episodes[0], TimeSpan.FromMinutes(11), Duration, playlistId, ct);
        await FlushBufferAsync(1, ct);

        Assert.Equal(episodes[1], Assert.Single(await GetContinueWatchingEntriesAsync(playlistId, cancellationToken: ct)).TVShowEpisodeId);
        Assert.Equal(episodes[0], Assert.Single(await GetPlaylistlessEntriesAsync(ct)).TVShowEpisodeId);
    }

    /// <summary>
    /// Erreicht der aus der Playlist entfernte Titel danach seine Endsequenz, gilt für ihn die Regel ohne
    /// Playlist: Nachfolger ist die nächste Episode der Serie, als Eintrag ohne Playlist-Bezug. Der Eintrag
    /// der Playlist bleibt unverändert.
    /// </summary>
    [Fact]
    public async Task EndSequenceForTitleRemovedFromPlaylist_UsesSeriesOrderWithoutTouchingPlaylistEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, episodes) = await CreateShowAsync("Serie", 3);
        await GrantMediaSourceAccessForUserAsync(_testUserId);
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.TVShowEpisode, episodes[0], 0),
            (MediaTypeValues.TVShowEpisode, episodes[2], 1));

        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, episodes[2], MidPosition, Duration, playlistId, ct);
        var playlistEntryBefore = Assert.Single(await GetContinueWatchingEntriesAsync(playlistId, cancellationToken: ct));

        // E2 gehoert nicht zur Playlist, wird aber mit playlistId gemeldet und erreicht die Endsequenz.
        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, episodes[1], EndSequencePosition, Duration, playlistId, ct);

        var playlistEntryAfter = Assert.Single(await GetContinueWatchingEntriesAsync(playlistId, cancellationToken: ct));
        Assert.Equal(playlistEntryBefore.Id, playlistEntryAfter.Id);
        Assert.Equal(episodes[2], playlistEntryAfter.TVShowEpisodeId);

        // Serienregel ohne Playlist: Nachfolger von E2 ist E3, ohne Playlist-Bezug.
        var freeEntry = Assert.Single(await GetPlaylistlessEntriesAsync(ct));
        Assert.Equal(episodes[2], freeEntry.TVShowEpisodeId);
        Assert.Null(freeEntry.PlaylistId);
    }

    /// <summary>
    /// Ein Kaskaden-Kindeintrag (Titel, der über eine ganze Serie in die Playlist kam) ist ein eigener
    /// <c>PlaylistEntry</c> und behält deshalb seinen Playlist-Bezug.
    /// </summary>
    [Fact]
    public async Task ProgressForCascadeChildEntry_KeepsPlaylistBinding()
    {
        var ct = TestContext.Current.CancellationToken;
        var (showId, episodes) = await CreateShowAsync("Serie", 2);
        await GrantMediaSourceAccessForUserAsync(_testUserId);

        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShow, showId, 0));
        foreach (var (episodeId, index) in episodes.Select((id, i) => (id, i)))
        {
            _db.PlaylistEntries.Add(new PlaylistEntry
            {
                PlaylistId = playlistId,
                MediaType = MediaTypeValues.TVShowEpisode,
                MediaId = episodeId,
                ParentMediaType = MediaTypeValues.TVShow,
                ParentMediaId = showId,
                SortOrder = index + 1,
                AddedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync(ct);

        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, episodes[0], MidPosition, Duration, playlistId, ct);

        var entry = Assert.Single(await GetContinueWatchingEntriesAsync(playlistId, cancellationToken: ct));
        Assert.Equal(episodes[0], entry.TVShowEpisodeId);
        Assert.Equal(playlistId, entry.PlaylistId);
        Assert.Empty(await GetPlaylistlessEntriesAsync(ct));
    }

    /// <summary>
    /// Ein Betrachter einer öffentlichen Playlist wird genauso behandelt: Meldet er Fortschritt für einen
    /// Titel, der nicht zur Playlist gehört, bekommt er einen eigenen Eintrag ohne Playlist-Bezug; der
    /// Eintrag des Besitzers bleibt unverändert.
    /// </summary>
    [Fact]
    public async Task ViewerOfPublicPlaylist_ProgressForForeignTitle_DoesNotTouchOwnersPlaylistEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, episodes) = await CreateShowAsync("Serie", 3);
        await GrantMediaSourceAccessForUserAsync(_testUserId);
        await GrantMediaSourceAccessForUserAsync(_otherUserId);

        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_otherUserId,
            (MediaTypeValues.TVShowEpisode, episodes[0], 0));
        await MakePlaylistPublicAsync(playlistId);

        await _continueWatching.ProcessBufferedEntryAsync(_otherUserId, null, episodes[0], MidPosition, Duration, playlistId, ct);
        var ownerEntry = Assert.Single(await GetContinueWatchingEntriesAsync(playlistId, _otherUserId, ct));

        // Der Betrachter meldet einen Titel, der nicht zur Playlist gehoert.
        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, episodes[1], MidPosition, Duration, playlistId, ct);

        var ownerEntryAfter = Assert.Single(await GetContinueWatchingEntriesAsync(playlistId, _otherUserId, ct));
        Assert.Equal(ownerEntry.Id, ownerEntryAfter.Id);
        Assert.Equal(episodes[0], ownerEntryAfter.TVShowEpisodeId);

        Assert.Empty(await GetContinueWatchingEntriesAsync(playlistId, _testUserId, ct));
        var viewerEntry = Assert.Single(await GetPlaylistlessEntriesAsync(ct, _testUserId));
        Assert.Equal(episodes[1], viewerEntry.TVShowEpisodeId);
    }
}
