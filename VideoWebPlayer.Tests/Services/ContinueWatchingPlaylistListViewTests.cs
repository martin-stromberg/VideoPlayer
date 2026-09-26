using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Abnahme-Abweichung A4: Altbestand mit mehreren Weiterschauen-Einträgen je Playlist darf dem Anwender
/// nicht als mehrere Kacheln gezeigt werden. <c>GetListAsync</c> fasst rein lesend auf den zuletzt
/// aktualisierten Eintrag je Playlist zusammen; gelöscht wird dabei nichts (das erledigt erst der nächste
/// Schreibvorgang, BR-35).
/// </summary>
public class ContinueWatchingPlaylistListViewTests : ContinueWatchingPlaylistTestBase
{
    [Fact]
    public async Task GetList_LegacyDuplicatesOfOnePlaylist_ShowsOnlyMostRecentlyUpdatedEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var (playlistId, showA, showB) = await CreateTwoShowPlaylistAsync();

        var older = await SeedContinueWatchingEntryAsync(null, showA[0], playlistId, position: TimeSpan.FromMinutes(3));
        var newer = await SeedContinueWatchingEntryAsync(null, showB[0], playlistId, position: TimeSpan.FromMinutes(4));
        await SetUpdatedAtAsync(older.Id, DateTime.UtcNow.AddHours(-2), ct);
        await SetUpdatedAtAsync(newer.Id, DateTime.UtcNow.AddMinutes(-5), ct);

        var list = await _continueWatching.GetListAsync(CreateTestPrincipal(), ct);

        var item = Assert.Single(list);
        Assert.Equal(showB[0], item.Entry?.Id);
        Assert.Equal(playlistId, item.PlaylistId);

        // Rein lesend: beide Zeilen stehen weiterhin in der Datenbank.
        Assert.Equal(2, await _db.ContinueWatchingEntries.AsNoTracking()
            .CountAsync(x => x.UserId == _testUserId && x.PlaylistId == playlistId, ct));
    }

    [Fact]
    public async Task GetList_EntriesOfDifferentPlaylistsAndWithoutPlaylist_AreNotCollapsed()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, showA) = await CreateShowAsync("Serie A", 1);
        var (_, showB) = await CreateShowAsync("Serie B", 1);
        var (_, showC) = await CreateShowAsync("Serie C", 1);
        await GrantMediaSourceAccessForUserAsync(_testUserId);
        var playlist1 = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShowEpisode, showA[0]));
        var playlist2 = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShowEpisode, showB[0]));

        await SeedContinueWatchingEntryAsync(null, showA[0], playlist1);
        await SeedContinueWatchingEntryAsync(null, showB[0], playlist2);
        await SeedContinueWatchingEntryAsync(null, showC[0], null);

        var list = await _continueWatching.GetListAsync(CreateTestPrincipal(), ct);

        Assert.Equal(3, list.Count);
        Assert.Contains(list, x => x.PlaylistId == playlist1);
        Assert.Contains(list, x => x.PlaylistId == playlist2);
        Assert.Contains(list, x => x.PlaylistId is null);
    }

    /// <summary>
    /// Zwei Einträge desselben Videos - einer ohne Playlist, einer mit - bleiben beide sichtbar; die
    /// Zusammenfassung wirkt nur innerhalb einer Playlist.
    /// </summary>
    [Fact]
    public async Task GetList_SameVideoWithAndWithoutPlaylist_BothRemainVisible()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, episodes) = await CreateShowAsync("Serie", 1);
        await GrantMediaSourceAccessForUserAsync(_testUserId);
        var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShowEpisode, episodes[0]));

        await SeedContinueWatchingEntryAsync(null, episodes[0], null);
        await SeedContinueWatchingEntryAsync(null, episodes[0], playlistId);

        var list = await _continueWatching.GetListAsync(CreateTestPrincipal(), ct);

        Assert.Equal(2, list.Count);
        Assert.Contains(list, x => x.PlaylistId is null);
        Assert.Contains(list, x => x.PlaylistId == playlistId);
    }

    private async Task SetUpdatedAtAsync(long entryId, DateTime updatedAt, CancellationToken ct)
    {
        var entry = await _db.ContinueWatchingEntries.SingleAsync(x => x.Id == entryId, ct);
        entry.UpdatedAt = updatedAt;
        entry.ListOrder = updatedAt.Ticks;
        await _db.SaveChangesAsync(ct);
    }
}
