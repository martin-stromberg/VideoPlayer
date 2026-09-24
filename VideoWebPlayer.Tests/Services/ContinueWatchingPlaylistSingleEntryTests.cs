using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Kundenrückmeldung „Verhalten der Weiterschauen-Liste", Szenario 2: Für eine Playlist darf es je
/// Anwender höchstens EINEN Weiterschauen-Eintrag geben. Wird aus derselben Playlist ein anderer Titel
/// aufgerufen, ersetzt dessen Eintrag den bisherigen - unabhängig davon, ob es sich um dieselbe Serie,
/// eine andere Serie oder einen Film handelt. Einträge ohne Playlist-Bezug und Einträge anderer
/// Playlists bleiben unberührt.
/// </summary>
public class ContinueWatchingPlaylistSingleEntryTests : ContinueWatchingPlaylistTestBase
{
    /// <summary>
    /// Szenario 2: Episode der ersten Serie, danach - aus derselben Playlist - eine Episode der zweiten
    /// Serie. Es bleibt genau ein Eintrag übrig, und zwar der der zweiten Serie.
    /// </summary>
    [Fact]
    public async Task SecondShowFromSamePlaylist_ReplacesEntryOfFirstShow()
    {
        var ct = TestContext.Current.CancellationToken;
        var (playlistId, showA, showB) = await CreateTwoShowPlaylistAsync();

        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, showA[0], MidPosition, Duration, playlistId, ct);
        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, showB[0], MidPosition, Duration, playlistId, ct);

        var entry = Assert.Single(await GetContinueWatchingEntriesAsync(playlistId, cancellationToken: ct));
        Assert.Equal(showB[0], entry.TVShowEpisodeId);
    }

    /// <summary>
    /// Derselbe Ablauf über den produktiven Weg <c>ReportProgressAsync</c> + Puffer-Flush
    /// (<see cref="VideoWebPlayer.Services.ContinueWatchingWorker"/>).
    /// </summary>
    [Fact]
    public async Task SecondShowFromSamePlaylist_ViaReportProgressAndBufferFlush_ReplacesEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var (playlistId, showA, showB) = await CreateTwoShowPlaylistAsync();
        var user = await _db.Users.SingleAsync(u => u.Id == _testUserId, ct);

        await _continueWatching.ReportProgressAsync(user, null, showA[0], MidPosition, Duration, playlistId, ct);
        await FlushBufferAsync(1, ct);
        await _continueWatching.ReportProgressAsync(user, null, showB[0], MidPosition, Duration, playlistId, ct);
        await FlushBufferAsync(1, ct);

        var entry = Assert.Single(await GetContinueWatchingEntriesAsync(playlistId, cancellationToken: ct));
        Assert.Equal(showB[0], entry.TVShowEpisodeId);
    }

    /// <summary>
    /// Auch ein Film ersetzt den Episoden-Eintrag derselben Playlist (gemischte Playlist).
    /// </summary>
    [Fact]
    public async Task MovieFromSamePlaylist_ReplacesEpisodeEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, episodes) = await CreateShowAsync("Serie", 1);
        var movieId = await CreateStandaloneMovieAsync("Film");
        await GrantMediaSourceAccessForUserAsync(_testUserId);
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.TVShowEpisode, episodes[0], 0),
            (MediaTypeValues.Movie, movieId, 1));

        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, episodes[0], MidPosition, Duration, playlistId, ct);
        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, movieId, null, MidPosition, Duration, playlistId, ct);

        var entry = Assert.Single(await GetContinueWatchingEntriesAsync(playlistId, cancellationToken: ct));
        Assert.Equal(movieId, entry.MovieId);
        Assert.Null(entry.TVShowEpisodeId);
    }

    /// <summary>
    /// Mehrere Playlists desselben Anwenders beeinflussen sich nicht: jede behält ihren eigenen Eintrag.
    /// </summary>
    [Fact]
    public async Task DifferentPlaylists_KeepTheirOwnEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, showA) = await CreateShowAsync("Serie A", 1);
        var (_, showB) = await CreateShowAsync("Serie B", 1);
        await GrantMediaSourceAccessForUserAsync(_testUserId);
        var playlist1 = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShowEpisode, showA[0]));
        var playlist2 = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShowEpisode, showB[0]));

        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, showA[0], MidPosition, Duration, playlist1, ct);
        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, showB[0], MidPosition, Duration, playlist2, ct);

        Assert.Equal(showA[0], Assert.Single(await GetContinueWatchingEntriesAsync(playlist1, cancellationToken: ct)).TVShowEpisodeId);
        Assert.Equal(showB[0], Assert.Single(await GetContinueWatchingEntriesAsync(playlist2, cancellationToken: ct)).TVShowEpisodeId);
    }

    /// <summary>
    /// Ein Eintrag ohne Playlist-Bezug wird durch einen Playlist-Eintrag nicht verdrängt.
    /// </summary>
    [Fact]
    public async Task PlaylistlessEntry_IsNotRemovedByPlaylistEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, showA) = await CreateShowAsync("Serie A", 1);
        var (_, showB) = await CreateShowAsync("Serie B", 1);
        await GrantMediaSourceAccessForUserAsync(_testUserId);
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShowEpisode, showB[0]));

        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, showA[0], MidPosition, Duration, null, ct);
        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, showB[0], MidPosition, Duration, playlistId, ct);

        Assert.Equal(showA[0], Assert.Single(await GetPlaylistlessEntriesAsync(ct)).TVShowEpisodeId);
        Assert.Equal(showB[0], Assert.Single(await GetContinueWatchingEntriesAsync(playlistId, cancellationToken: ct)).TVShowEpisodeId);
    }

    /// <summary>
    /// Derselbe Fortschrittsbericht zweimal erzeugt keinen zweiten Eintrag und lässt den vorhandenen
    /// Eintrag bestehen.
    /// </summary>
    [Fact]
    public async Task SameReportTwice_DoesNotDuplicateEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var (playlistId, showA, _) = await CreateTwoShowPlaylistAsync();

        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, showA[0], MidPosition, Duration, playlistId, ct);
        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, showA[0], MidPosition, Duration, playlistId, ct);

        var entry = Assert.Single(await GetContinueWatchingEntriesAsync(playlistId, cancellationToken: ct));
        Assert.Equal(showA[0], entry.TVShowEpisodeId);
        Assert.Equal(MidPosition, entry.Position);
    }

    /// <summary>
    /// Bereits in der Datenbank vorhandene Mehrfach-Einträge einer Playlist (Altbestand aus der Zeit vor
    /// dieser Regel) werden beim nächsten Schreibvorgang dieser Playlist bereinigt - auch dann, wenn der
    /// geschriebene Eintrag selbst schon existiert und nur aktualisiert wird.
    /// </summary>
    [Fact]
    public async Task ExistingDuplicates_AreCleanedUpOnNextWrite()
    {
        var ct = TestContext.Current.CancellationToken;
        var (playlistId, showA, showB) = await CreateTwoShowPlaylistAsync();

        var stale = await SeedContinueWatchingEntryAsync(null, showA[0], playlistId, position: TimeSpan.FromMinutes(3));
        var current = await SeedContinueWatchingEntryAsync(null, showB[0], playlistId, position: TimeSpan.FromMinutes(4));
        Assert.Equal(2, (await GetContinueWatchingEntriesAsync(playlistId, cancellationToken: ct)).Count);

        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, showB[0], MidPosition, Duration, playlistId, ct);

        var entry = Assert.Single(await GetContinueWatchingEntriesAsync(playlistId, cancellationToken: ct));
        Assert.Equal(current.Id, entry.Id);
        Assert.Equal(showB[0], entry.TVShowEpisodeId);
        Assert.False(await _db.ContinueWatchingEntries.AsNoTracking().AnyAsync(x => x.Id == stale.Id, ct));
    }

    /// <summary>
    /// Bei einer öffentlichen Playlist eines anderen Anwenders gilt die Regel je Anwender: der Betrachter
    /// bekommt genau einen eigenen Eintrag, der Eintrag des Besitzers bleibt unberührt.
    /// </summary>
    [Fact]
    public async Task PublicPlaylist_ViewerAndOwnerKeepTheirOwnSingleEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, showA) = await CreateShowAsync("Serie A", 1);
        var (_, showB) = await CreateShowAsync("Serie B", 1);
        await GrantMediaSourceAccessForUserAsync(_testUserId);
        await GrantMediaSourceAccessForUserAsync(_otherUserId);

        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_otherUserId,
            (MediaTypeValues.TVShowEpisode, showA[0], 0),
            (MediaTypeValues.TVShowEpisode, showB[0], 1));
        await MakePlaylistPublicAsync(playlistId);

        // Besitzer schaut die erste Episode, Betrachter danach beide nacheinander.
        await _continueWatching.ProcessBufferedEntryAsync(_otherUserId, null, showA[0], MidPosition, Duration, playlistId, ct);
        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, showA[0], MidPosition, Duration, playlistId, ct);
        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, showB[0], MidPosition, Duration, playlistId, ct);

        Assert.Equal(showA[0], Assert.Single(await GetContinueWatchingEntriesAsync(playlistId, _otherUserId, ct)).TVShowEpisodeId);
        Assert.Equal(showB[0], Assert.Single(await GetContinueWatchingEntriesAsync(playlistId, _testUserId, ct)).TVShowEpisodeId);
    }
}
