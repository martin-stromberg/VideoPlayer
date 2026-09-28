using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Verhalten rund um die Endsequenz eines Playlist-Titels: das bewusst beibehaltene Wieder-Anspielen
/// (Abnahme-Abweichung A1, als Geschäftsregel festgeschrieben) und die Sparsamkeit der
/// Nachfolger-Ermittlung bei wiederholten Meldungen in der Endzone (Abnahme-Abweichung A3).
/// </summary>
public class ContinueWatchingPlaylistEndSequenceTests : ContinueWatchingPlaylistTestBase
{
    /// <summary>
    /// Kontrolltest (keine Fehlerbehebung): Spielt der Anwender einen bereits beendeten Titel derselben
    /// Playlist erneut an - zurückspulen und pausieren, oder den Player schließen -, wird dieser Titel
    /// wieder zum einen Eintrag der Playlist und der zuvor eingefügte Nachfolger entfällt. Das ist gewollt:
    /// Der Anwender ist aktiv wieder beim alten Titel, und ohne Playlist gilt dasselbe (ein Eintrag je
    /// Serie). Siehe BR-35, Abschnitt „Erneutes Anspielen".
    /// </summary>
    [Fact]
    public async Task ReplayingFinishedTitleOfSamePlaylist_BecomesThePlaylistEntryAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        var (playlistId, showA, showB) = await CreateTwoShowPlaylistAsync();

        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, showA[1], EndSequencePosition, Duration, playlistId, ct);
        Assert.Equal(showB[0], Assert.Single(await GetContinueWatchingEntriesAsync(playlistId, cancellationToken: ct)).TVShowEpisodeId);

        // Zurueckgespult und pausiert: Meldung mit einer Position deutlich vor der Endsequenz.
        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, showA[1], MidPosition, Duration, playlistId, ct);

        var entry = Assert.Single(await GetContinueWatchingEntriesAsync(playlistId, cancellationToken: ct));
        Assert.Equal(showA[1], entry.TVShowEpisodeId);
        Assert.Equal(MidPosition, entry.Position);

        // Die Gesehen-Markierung des erneut angespielten Titels bleibt bestehen; nur die Weiterschauen-Liste
        // folgt dem Anwender.
        Assert.True(await _db.WatchedEntries.AsNoTracking()
            .AnyAsync(x => x.UserId == _testUserId && x.TVShowEpisodeId == showA[1], ct));
    }

    /// <summary>
    /// Ohne Playlist-Bezug gilt dieselbe Regel: Das erneute Anspielen einer beendeten Episode verdrängt den
    /// zuvor eingefügten Nachfolger derselben Serie.
    /// </summary>
    [Fact]
    public async Task ReplayingFinishedTitleWithoutPlaylist_BehavesTheSameWay()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, episodes) = await CreateShowAsync("Serie", 2);

        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, episodes[0], EndSequencePosition, Duration, null, ct);
        Assert.Equal(episodes[1], Assert.Single(await GetPlaylistlessEntriesAsync(ct)).TVShowEpisodeId);

        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, episodes[0], MidPosition, Duration, null, ct);

        var entry = Assert.Single(await GetPlaylistlessEntriesAsync(ct));
        Assert.Equal(episodes[0], entry.TVShowEpisodeId);
    }

    /// <summary>
    /// Kontrolltest zur A1-Untersuchung: Eine bereits gepufferte, ältere Meldung desselben Titels kann die
    /// später eintreffende Endsequenz-Meldung <b>nicht</b> überholen. <see cref="ContinueWatchingBuffer"/>
    /// führt je (Anwender, Film, Episode, Playlist) genau einen Schnappschuss und überschreibt ihn mit dem
    /// neueren; der Worker liest denselben Schlüssel danach ein zweites Mal ins Leere. Der Nachfolger bleibt
    /// also stehen - A1 ist nur über ein echtes Zurückspulen des Anwenders auslösbar, nicht über den Puffer.
    /// </summary>
    [Fact]
    public async Task BufferedOutdatedReport_IsSupersededByLaterEndSequenceReport()
    {
        var ct = TestContext.Current.CancellationToken;
        var (playlistId, showA, showB) = await CreateTwoShowPlaylistAsync();
        var user = await _db.Users.SingleAsync(u => u.Id == _testUserId, ct);

        // Beide Meldungen landen im Puffer, bevor der Worker ueberhaupt laeuft.
        await _continueWatching.ReportProgressAsync(user, null, showA[1], MidPosition, Duration, playlistId, ct);
        await _continueWatching.ReportProgressAsync(user, null, showA[1], EndSequencePosition, Duration, playlistId, ct);

        await FlushBufferAsync(2, ct);

        var entry = Assert.Single(await GetContinueWatchingEntriesAsync(playlistId, cancellationToken: ct));
        Assert.Equal(showB[0], entry.TVShowEpisodeId);
    }

    /// <summary>
    /// A3: Der Player meldet in den letzten 30 Sekunden rund zehnmal Fortschritt. Der Nachfolger darf dabei
    /// nur einmal ermittelt werden (ein Laden, Sortieren und Auflösen der ganzen Playlist), das Ergebnis muss
    /// über alle Wiederholungen dasselbe bleiben.
    /// </summary>
    [Fact]
    public async Task RepeatedEndSequenceReports_ResolveSuccessorOnlyOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var (playlistId, showA, showB) = await CreateTwoShowPlaylistAsync();

        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, showA[1], MidPosition, Duration, playlistId, ct);
        Assert.Equal(0, NextPlaylistEntryCallCount);

        for (var i = 0; i < 10; i++)
        {
            var position = Duration - TimeSpan.FromSeconds(28 - (i * 3));
            await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, showA[1], position, Duration, playlistId, ct);
        }

        Assert.Equal(1, NextPlaylistEntryCallCount);
        var entry = Assert.Single(await GetContinueWatchingEntriesAsync(playlistId, cancellationToken: ct));
        Assert.Equal(showB[0], entry.TVShowEpisodeId);
        Assert.Equal(TimeSpan.Zero, entry.Position);
    }

    /// <summary>
    /// A3-Randfall: Wer ohne vorherigen Eintrag direkt in die Endsequenz springt, muss den Nachfolger
    /// trotzdem bekommen - die Abkürzung darf nicht am fehlenden Vorgänger-Eintrag scheitern.
    /// </summary>
    [Fact]
    public async Task EndSequenceWithoutAnyPreviousEntry_StillResolvesSuccessorOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var (playlistId, showA, showB) = await CreateTwoShowPlaylistAsync();

        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, showA[1], EndSequencePosition, Duration, playlistId, ct);

        Assert.Equal(1, NextPlaylistEntryCallCount);
        Assert.Equal(showB[0], Assert.Single(await GetContinueWatchingEntriesAsync(playlistId, cancellationToken: ct)).TVShowEpisodeId);

        // Weitere Meldungen derselben Endzone aendern nichts mehr und laden die Playlist nicht erneut.
        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, showA[1], EndSequencePosition, Duration, playlistId, ct);
        Assert.Equal(1, NextPlaylistEntryCallCount);
    }

    /// <summary>
    /// A3 darf Szenario 1 nicht aushebeln: Läuft der letzte Titel der Playlist in die Endsequenz, während
    /// die Playlist bereits einen Eintrag für genau diesen Titel hat, wird der Eintrag entfernt und - weil es
    /// keinen Nachfolger gibt - nichts Neues angelegt.
    ///
    /// Hält zugleich die bekannte Grenze der Abkürzung fest: Am Ende einer Playlist bleibt danach kein
    /// Eintrag übrig, deshalb greift die Bedingung „Playlist hat noch keinen Eintrag" und jede weitere
    /// Meldung derselben Endzone ermittelt den Nachfolger erneut. Das betrifft nur den letzten Titel einer
    /// Playlist; für alle anderen Titel bleibt es bei genau einem Aufruf (siehe die Tests oben).
    /// </summary>
    [Fact]
    public async Task EndSequenceOfLastPlaylistTitle_RemovesEntryEvenWithShortCircuit()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, episodes) = await CreateShowAsync("Serie", 3);
        await GrantMediaSourceAccessForUserAsync(_testUserId);
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.TVShowEpisode, episodes[0], 0),
            (MediaTypeValues.TVShowEpisode, episodes[1], 1));

        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, episodes[1], MidPosition, Duration, playlistId, ct);
        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, episodes[1], EndSequencePosition, Duration, playlistId, ct);
        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, episodes[1], EndSequencePosition, Duration, playlistId, ct);

        Assert.Empty(await GetContinueWatchingEntriesAsync(playlistId, cancellationToken: ct));

        // Bekannte Grenze, siehe XML-Doc: beide Endsequenz-Meldungen ermitteln den Nachfolger.
        Assert.Equal(2, NextPlaylistEntryCallCount);
    }
}
