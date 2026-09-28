using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Kundenrückmeldung „Verhalten der Weiterschauen-Liste", Szenarien 1 und 3: Wird beim Abspielen aus
/// einer Playlist die Endsequenz erreicht, muss der Nachfolger des Weiterschauen-Eintrags der nächste
/// abspielbare und zugängliche Titel der <b>Playlist</b> sein (in deren aktueller Sortierung) - nicht
/// die nächste Episode der Serie bzw. der nächste Film der Sammlung.
/// </summary>
public class ContinueWatchingPlaylistSuccessorTests : ContinueWatchingPlaylistTestBase
{
    /// <summary>
    /// Szenario 1: Zwei Serien (Original und Fortsetzung) in einer Playlist. Nach der letzten Episode
    /// der ersten Serie muss die erste Episode der zweiten Serie in der Weiterschauen-Liste stehen.
    /// </summary>
    [Fact]
    public async Task EndSequence_LastEpisodeOfFirstShow_ContinuesWithFirstEpisodeOfSecondShow()
    {
        var ct = TestContext.Current.CancellationToken;
        var (playlistId, showA, showB) = await CreateTwoShowPlaylistAsync();

        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, showA[1], MidPosition, Duration, playlistId, ct);
        Assert.Single(await GetContinueWatchingEntriesAsync(playlistId, cancellationToken: ct));

        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, showA[1], EndSequencePosition, Duration, playlistId, ct);

        var entry = Assert.Single(await GetContinueWatchingEntriesAsync(playlistId, cancellationToken: ct));
        Assert.Equal(showB[0], entry.TVShowEpisodeId);
        Assert.Equal(playlistId, entry.PlaylistId);
        Assert.Equal(TimeSpan.Zero, entry.Position);
    }

    /// <summary>
    /// Szenario 3: Die zweite Episode wurde aus der Playlist entfernt. Nachfolger der ersten Episode
    /// ist deshalb die dritte Episode, nicht die ausgeschlossene zweite.
    /// </summary>
    [Fact]
    public async Task EndSequence_EpisodeExcludedFromPlaylist_IsSkipped()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, episodes) = await CreateShowAsync("Serie", 3);
        await GrantMediaSourceAccessForUserAsync(_testUserId);
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.TVShowEpisode, episodes[0], 0),
            (MediaTypeValues.TVShowEpisode, episodes[2], 1));

        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, episodes[0], MidPosition, Duration, playlistId, ct);
        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, episodes[0], EndSequencePosition, Duration, playlistId, ct);

        var entry = Assert.Single(await GetContinueWatchingEntriesAsync(playlistId, cancellationToken: ct));
        Assert.Equal(episodes[2], entry.TVShowEpisodeId);
    }

    /// <summary>
    /// Ist die Playlist zu Ende, wird der Eintrag ersatzlos entfernt - auch dann, wenn die Serie selbst
    /// noch weitere Episoden hätte.
    /// </summary>
    [Fact]
    public async Task EndSequence_LastPlaylistTitle_RemovesEntryWithoutReplacement()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, episodes) = await CreateShowAsync("Serie", 3);
        await GrantMediaSourceAccessForUserAsync(_testUserId);
        // Nur die ersten beiden Episoden sind Teil der Playlist; Episode 3 existiert in der Serie weiter.
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.TVShowEpisode, episodes[0], 0),
            (MediaTypeValues.TVShowEpisode, episodes[1], 1));

        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, episodes[1], MidPosition, Duration, playlistId, ct);
        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, episodes[1], EndSequencePosition, Duration, playlistId, ct);

        Assert.Empty(await GetContinueWatchingEntriesAsync(playlistId, cancellationToken: ct));
    }

    /// <summary>
    /// Der Nachfolger folgt der manuellen Sortierung der Playlist, nicht der Serienreihenfolge: bei
    /// umgekehrter manueller Reihenfolge ist der Nachfolger von Episode 2 die Episode 1.
    /// </summary>
    [Fact]
    public async Task EndSequence_ManualSortOrder_FollowsPlaylistOrderNotSeriesOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, episodes) = await CreateShowAsync("Serie", 3);
        await GrantMediaSourceAccessForUserAsync(_testUserId);
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.TVShowEpisode, episodes[2], 0),
            (MediaTypeValues.TVShowEpisode, episodes[1], 1),
            (MediaTypeValues.TVShowEpisode, episodes[0], 2));

        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, episodes[1], MidPosition, Duration, playlistId, ct);
        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, episodes[1], EndSequencePosition, Duration, playlistId, ct);

        var entry = Assert.Single(await GetContinueWatchingEntriesAsync(playlistId, cancellationToken: ct));
        Assert.Equal(episodes[0], entry.TVShowEpisodeId);
    }

    /// <summary>
    /// In einer nach Erscheinungsdatum sortierten Playlist folgt der Nachfolger dieser Sortierung.
    /// </summary>
    [Fact]
    public async Task EndSequence_ByReleaseDateSortOrder_FollowsPlaylistOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var older = await CreateMovieWithReleaseDateAsync("Alter Film", new DateTime(2010, 1, 1));
        var newer = await CreateMovieWithReleaseDateAsync("Neuer Film", new DateTime(2020, 1, 1));
        await GrantMediaSourceAccessForUserAsync(_testUserId);
        // Einfuegereihenfolge absichtlich umgekehrt zum Erscheinungsdatum.
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.Movie, newer),
            (MediaTypeValues.Movie, older));

        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, older, null, MidPosition, Duration, playlistId, ct);
        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, older, null, EndSequencePosition, Duration, playlistId, ct);

        var entry = Assert.Single(await GetContinueWatchingEntriesAsync(playlistId, cancellationToken: ct));
        Assert.Equal(newer, entry.MovieId);
    }

    /// <summary>
    /// Ein für den Anwender gesperrter Titel wird als Nachfolger übersprungen.
    /// </summary>
    [Fact]
    public async Task EndSequence_LockedTitle_IsSkipped()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, episodes) = await CreateShowAsync("Serie", 1);
        var lockedMovie = await CreateStandaloneMovieAsync("Gesperrter Film", mediaSourceId: 99);
        var reachableMovie = await CreateStandaloneMovieAsync("Erreichbarer Film");
        await GrantMediaSourceAccessForUserAsync(_testUserId);

        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.TVShowEpisode, episodes[0], 0),
            (MediaTypeValues.Movie, lockedMovie, 1),
            (MediaTypeValues.Movie, reachableMovie, 2));

        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, episodes[0], MidPosition, Duration, playlistId, ct);
        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, episodes[0], EndSequencePosition, Duration, playlistId, ct);

        var entry = Assert.Single(await GetContinueWatchingEntriesAsync(playlistId, cancellationToken: ct));
        Assert.Equal(reachableMovie, entry.MovieId);
        Assert.Null(entry.TVShowEpisodeId);
    }

    /// <summary>
    /// Nicht abspielbare Sammel-Einträge (Serie, Staffel, Filmsammlung) werden als Nachfolger
    /// übersprungen; der Nachfolger darf ein Film nach einer Episode sein.
    /// </summary>
    [Fact]
    public async Task EndSequence_CollectionEntry_IsSkippedAndMovieFollowsEpisode()
    {
        var ct = TestContext.Current.CancellationToken;
        var (showId, episodes) = await CreateShowAsync("Serie", 1);
        var movieId = await CreateStandaloneMovieAsync("Film");
        await GrantMediaSourceAccessForUserAsync(_testUserId);

        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.TVShowEpisode, episodes[0], 0),
            (MediaTypeValues.TVShow, showId, 1),
            (MediaTypeValues.Movie, movieId, 2));

        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, episodes[0], MidPosition, Duration, playlistId, ct);
        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, episodes[0], EndSequencePosition, Duration, playlistId, ct);

        var entry = Assert.Single(await GetContinueWatchingEntriesAsync(playlistId, cancellationToken: ct));
        Assert.Equal(movieId, entry.MovieId);
    }

    /// <summary>
    /// Ohne Playlist-Bezug bleibt das bisherige Verhalten erhalten: Nachfolger ist die nächste Episode
    /// der Serie, auch wenn sie in keiner Playlist steht.
    /// </summary>
    [Fact]
    public async Task EndSequence_WithoutPlaylist_StillUsesSeriesOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, episodes) = await CreateShowAsync("Serie", 2);

        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, episodes[0], MidPosition, Duration, null, ct);
        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, episodes[0], EndSequencePosition, Duration, null, ct);

        var free = Assert.Single(await GetPlaylistlessEntriesAsync(ct));
        Assert.Equal(episodes[1], free.TVShowEpisodeId);
        Assert.Null(free.PlaylistId);
    }

    /// <summary>
    /// Der neu erzeugte Nachfolger-Eintrag ist in der Weiterschauen-Liste korrekt mit der Playlist und
    /// dem zugehörigen Playlist-Eintrag verknüpft, damit der Fortsetzen-Link
    /// (<c>/playlists/{PlaylistId}?entryId={PlaylistEntryId}</c>) funktioniert.
    /// </summary>
    [Fact]
    public async Task EndSequence_SuccessorEntry_ResolvesPlaylistEntryIdForResumeLink()
    {
        var ct = TestContext.Current.CancellationToken;
        var (playlistId, showA, showB) = await CreateTwoShowPlaylistAsync();

        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, showA[1], EndSequencePosition, Duration, playlistId, ct);

        var expectedPlaylistEntryId = await GetPlaylistEntryIdAsync(playlistId, MediaTypeValues.TVShowEpisode, showB[0], ct);
        var list = await _continueWatching.GetListAsync(CreateTestPrincipal(), ct);

        var dto = Assert.Single(list);
        Assert.Equal(playlistId, dto.PlaylistId);
        Assert.Equal(expectedPlaylistEntryId, dto.PlaylistEntryId);
        Assert.Equal(showB[0], dto.Entry?.Id);
    }

    /// <summary>
    /// Steht der gerade gespielte Titel gar nicht mehr in der Playlist (er wurde während der Wiedergabe
    /// entfernt), darf die Endsequenz keine Ausnahme auslösen. Der bereits beim Entfernen gesetzte
    /// Ersatz-Eintrag der Playlist bleibt unangetastet.
    /// </summary>
    [Fact]
    public async Task EndSequence_CurrentTitleNoLongerInPlaylist_DoesNotThrowAndKeepsReplacement()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, episodes) = await CreateShowAsync("Serie", 2);
        await GrantMediaSourceAccessForUserAsync(_testUserId);
        var playlistId = await CreateTestManualPlaylistWithEntriesAsync(_testUserId,
            (MediaTypeValues.TVShowEpisode, episodes[0], 0),
            (MediaTypeValues.TVShowEpisode, episodes[1], 1));

        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, episodes[0], MidPosition, Duration, playlistId, ct);

        // Der laufende Titel wird aus der Playlist entfernt; der Weiterschauen-Eintrag wandert dabei
        // bereits auf die zweite Episode.
        await _playlists.RemoveMediaFromPlaylistAsync(playlistId, _testUserId, MediaTypeValues.TVShowEpisode, episodes[0],
            confirmContinueWatchingRemoval: true, cancellationToken: ct);

        // Verspaeteter Endsequenz-Bericht des inzwischen entfernten Titels.
        await _continueWatching.ProcessBufferedEntryAsync(_testUserId, null, episodes[0], EndSequencePosition, Duration, playlistId, ct);

        var entry = Assert.Single(await GetContinueWatchingEntriesAsync(playlistId, cancellationToken: ct));
        Assert.Equal(episodes[1], entry.TVShowEpisodeId);
    }

    private async Task<long> CreateMovieWithReleaseDateAsync(string name, DateTime releaseDate)
    {
        var movie = new Movie { Name = name, MediaSourceId = 1, CreatedAt = DateTime.UtcNow, ReleaseDate = releaseDate };
        _db.Movies.Add(movie);
        await _db.SaveChangesAsync();
        return movie.Id;
    }
}
