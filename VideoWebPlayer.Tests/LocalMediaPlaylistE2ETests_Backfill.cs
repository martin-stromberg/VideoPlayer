using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// A7, Teil 2: eine Playlist, die eine Serie aus einem lokalen Verzeichnis enthält, bekommt eine neu
/// hinzugekommene Episode nach dem "Neu erfassen" von selbst nachgeliefert — ohne dass jemand die Playlist
/// anfasst. Die Gegenprobe gehört dazu: ein "Neu erfassen" ohne neue Dateien liefert nichts nach.
/// </summary>
[Trait("Category", "E2E")]
[Collection(MediaSourceClassifierCollection.Name)]
public sealed class LocalMediaPlaylistE2ETests_Backfill : LocalMediaPlaylistE2ETestBase
{
    private const string ShowTitle = "Lokale Serie";

    [Fact]
    public async Task NewEpisodeInLocalDirectory_AfterRescan_IsBackfilledIntoThePlaylist()
    {
        var showDirectory = Path.Combine(RootDirectory, "Serie");
        WriteEpisodeFiles(showDirectory, ShowTitle, "episode01", "Folge 1", 1);
        await ScanEverythingAsync();

        var playlistId = await CreatePlaylistWithShowAsync("Serien-Playlist");
        var episodesBefore = await GetEpisodeTitlesAsync(playlistId);
        Assert.Equal(["Folge 1"], episodesBefore);

        WriteEpisodeFiles(showDirectory, ShowTitle, "episode02", "Folge 2", 2);
        await RescanCollectionAsync(await GetCollectionIdAsync(showDirectory));

        var summary = await RunPlaylistBackfillAsync();

        Assert.True(summary.EntriesAdded >= 1, $"Es wurde nichts nachgeliefert ({summary}).");
        Assert.Equal(0, summary.PlaylistsFailed);
        Assert.Equal(["Folge 1", "Folge 2"], await GetEpisodeTitlesAsync(playlistId));
    }

    [Fact]
    public async Task RescanWithoutNewFiles_LeavesThePlaylistUnchanged()
    {
        var showDirectory = Path.Combine(RootDirectory, "Serie");
        WriteEpisodeFiles(showDirectory, ShowTitle, "episode01", "Folge 1", 1);
        await ScanEverythingAsync();

        var playlistId = await CreatePlaylistWithShowAsync("Serien-Playlist-ohne-Neues");
        var before = await GetEpisodeTitlesAsync(playlistId);

        await RescanCollectionAsync(await GetCollectionIdAsync(showDirectory));
        var summary = await RunPlaylistBackfillAsync();

        Assert.Equal(0, summary.EntriesAdded);
        Assert.Equal(before, await GetEpisodeTitlesAsync(playlistId));
    }

    /// <summary>
    /// Creates a playlist and puts the whole scanned TV show into it, which cascade-adds its season and its
    /// episodes - the situation the automatic backfill refills later.
    /// </summary>
    /// <param name="name">The name of the playlist to create.</param>
    /// <returns>The id of the created playlist.</returns>
    private async Task<long> CreatePlaylistWithShowAsync(string name)
    {
        var showId = await ReadDatabaseAsync(db => db.TVShows.AsNoTracking()
            .Where(s => s.Name == ShowTitle)
            .Select(s => s.Id)
            .SingleAsync(TestContext.Current.CancellationToken));

        var playlist = await Client.CreatePlaylistAsync(new DtoCreatePlaylistRequest
        {
            Name = name,
            SortMode = PlaylistSortModeValues.ByReleaseDate
        });
        await Client.AddMediaToPlaylistAsync(playlist.Id,
            new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.TVShow, MediaId = showId });
        return playlist.Id;
    }

    /// <summary>
    /// Reads the episode titles currently in the playlist, in the order the playlist returns them; the
    /// organizational show and season entries are left out (they carry no playable title).
    /// </summary>
    /// <param name="playlistId">The playlist to read.</param>
    /// <returns>The episode titles of the playlist.</returns>
    private async Task<string[]> GetEpisodeTitlesAsync(long playlistId)
    {
        var ct = TestContext.Current.CancellationToken;
        var page = await Client.RequestPlaylistEntriesPagedAsync(playlistId, 1, 50, ct);
        var episodeIds = page.Entries
            .Where(e => e.MediaType == MediaTypeValues.TVShowEpisode)
            .Select(e => e.MediaId)
            .ToList();

        return await ReadDatabaseAsync(db => db.TVShowEpisodes.AsNoTracking()
            .Where(e => episodeIds.Contains(e.Id))
            .OrderBy(e => e.Number)
            .Select(e => e.Name)
            .ToArrayAsync(ct));
    }

    /// <summary>
    /// Resolves the media collection (the scanned directory) whose "Neu erfassen" the test triggers.
    /// </summary>
    /// <param name="directory">The directory on disk.</param>
    /// <returns>The id of the matching media collection.</returns>
    private Task<long> GetCollectionIdAsync(string directory)
        => ReadDatabaseAsync(db => db.MediaCollections.AsNoTracking()
            .Where(c => c.Path == directory)
            .Select(c => c.Id)
            .SingleAsync(TestContext.Current.CancellationToken));
}
