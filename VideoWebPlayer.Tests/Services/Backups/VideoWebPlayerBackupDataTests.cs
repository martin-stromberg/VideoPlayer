using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Backups;
using Xunit;

namespace VideoWebPlayer.Tests.Services.Backups;

/// <summary>
/// Ensures that database backups from earlier schema versions can still be restored.
/// </summary>
public sealed class VideoWebPlayerBackupDataTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    [Fact]
    public async Task ReadFromAsync_LegacyBackupWithoutUnlockedMediaEntries_RestoresSuccessfully()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-no-unlockedmedia?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, userId) = await CreateBackupWithSeededDatabaseAsync(connection, ct);
        await using var _ = db;

        using var legacyStream = await BuildLegacyBackupStreamRemovingTablesAsync(backup, new[] { "UnlockedMediaEntries" }, ct);

        // This must not throw even though the backup lacks the new table.
        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(legacyStream, ct));

        Assert.Null(exception);
        Assert.False(await db.UnlockedMediaEntries.AnyAsync(ct));
        Assert.Equal(userId, (await db.Users.FirstAsync(ct)).Id);
    }

    [Fact]
    public async Task ReadFromAsync_LegacyBackupWithoutWatchedEntries_RestoresSuccessfully()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-no-watched?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, userId) = await CreateBackupWithSeededDatabaseAsync(connection, ct);
        await using var _ = db;

        using var legacyStream = await BuildLegacyBackupStreamRemovingTablesAsync(backup, new[] { "WatchedEntries" }, ct);

        // This must not throw even though the backup lacks the new table.
        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(legacyStream, ct));

        Assert.Null(exception);
        Assert.False(await db.WatchedEntries.AnyAsync(ct));
        Assert.Equal(userId, (await db.Users.FirstAsync(ct)).Id);
    }

    [Fact]
    public async Task ReadFromAsync_LegacyBackupWithoutContinueWatchingEndThreshold_RestoresSuccessfully()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-no-threshold?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, userId) = await CreateBackupWithSeededDatabaseAsync(connection, ct);
        await using var _ = db;

        using var legacyStream = await BuildLegacyBackupStreamWithoutContinueWatchingThresholdColumnAsync(backup, ct);

        // Verify the simulated legacy backup really lacks the new optional column.
        legacyStream.Position = 0;
        using (var checkArchive = new ZipArchive(legacyStream, ZipArchiveMode.Read, true))
        {
            var setupsEntryName = GetSetupsEntryName(checkArchive);
            var checkEntry = checkArchive.GetEntry(setupsEntryName)!;
            using var checkStream = checkEntry.Open();
            var checkText = await new StreamReader(checkStream).ReadToEndAsync(ct);
            Assert.DoesNotContain("ContinueWatchingEndThresholdSeconds", checkText);
        }
        legacyStream.Position = 0;

        // This must not throw even though the backup lacks the new column.
        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(legacyStream, ct));

        Assert.Null(exception);
        Assert.Equal(userId, (await db.Users.FirstAsync(ct)).Id);
    }

    [Fact]
    public async Task ReadFromAsync_LegacyBackupWithoutPlaylistsAndPlaylistEntries_RestoresSuccessfully()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-playlists?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, userId) = await CreateBackupWithSeededDatabaseAsync(connection, ct);
        await using var _ = db;

        using var legacyStream = await BuildLegacyBackupStreamRemovingTablesAsync(backup, new[] { "Playlists", "PlaylistEntries" }, ct);

        // This must not throw even though the backup lacks the new tables.
        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(legacyStream, ct));

        Assert.Null(exception);
        Assert.False(await db.Playlists.AnyAsync(ct));
        Assert.False(await db.PlaylistEntries.AnyAsync(ct));
        Assert.Equal(userId, (await db.Users.FirstAsync(ct)).Id);
    }

    /// <summary>
    /// Verifies that a backup taken before the <c>PlaylistEntryExclusions</c> table existed (Entwicklungsschritt 8,
    /// tracking titles the user deliberately removed from a playlist so the automatic backfill mechanism
    /// does not re-add them) can still be restored, analogous to
    /// <see cref="ReadFromAsync_LegacyBackupWithoutPlaylistsAndPlaylistEntries_RestoresSuccessfully"/> above.
    /// </summary>
    [Fact]
    public async Task ReadFromAsync_LegacyBackupWithoutPlaylistEntryExclusions_RestoresSuccessfully()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-playlist-exclusions?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, userId) = await CreateBackupWithSeededDatabaseAsync(connection, ct);
        await using var _ = db;

        var playlist = new Playlist
        {
            UserId = userId,
            Name = "Playlist-Mit-Ausschluss",
            SortMode = PlaylistSortMode.ByReleaseDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Playlists.Add(playlist);
        await db.SaveChangesAsync(ct);
        db.PlaylistEntryExclusions.Add(new PlaylistEntryExclusion
        {
            PlaylistId = playlist.Id,
            MediaType = "Movie",
            MediaId = 1,
            ExcludedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);

        using var legacyStream = await BuildLegacyBackupStreamRemovingTablesAsync(backup, new[] { "PlaylistEntryExclusions" }, ct);

        // This must not throw even though the backup lacks the new table.
        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(legacyStream, ct));

        Assert.Null(exception);
        Assert.False(await db.PlaylistEntryExclusions.AnyAsync(ct));
        Assert.Equal(userId, (await db.Users.FirstAsync(ct)).Id);
    }

    /// <summary>
    /// Verifies that a backup taken before the <c>PlaylistGenres</c> table existed (Entwicklungsschritt 9,
    /// tracking a playlist's automatically derived or manually overridden genres) can still be restored,
    /// analogous to <see cref="ReadFromAsync_LegacyBackupWithoutPlaylistEntryExclusions_RestoresSuccessfully"/> above.
    /// </summary>
    [Fact]
    public async Task ReadFromAsync_LegacyBackupWithoutPlaylistGenres_RestoresSuccessfully()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-playlist-genres?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, userId) = await CreateBackupWithSeededDatabaseAsync(connection, ct);
        await using var _ = db;

        var mediaSource = new MediaSource { Name = "Quelle", Path = "/test", Host = "localhost", Port = 22 };
        db.MediaSources.Add(mediaSource);
        await db.SaveChangesAsync(ct);
        var genre = new Genre { MediaSourceId = mediaSource.Id, Name = "Action" };
        db.Genres.Add(genre);
        await db.SaveChangesAsync(ct);

        var playlist = new Playlist
        {
            UserId = userId,
            Name = "Playlist-Mit-Genre",
            SortMode = PlaylistSortMode.ByReleaseDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Playlists.Add(playlist);
        await db.SaveChangesAsync(ct);
        db.PlaylistGenres.Add(new PlaylistGenre { PlaylistId = playlist.Id, GenreId = genre.Id, Count = 1 });
        await db.SaveChangesAsync(ct);

        using var legacyStream = await BuildLegacyBackupStreamRemovingTablesAsync(backup, new[] { "PlaylistGenres" }, ct);

        // This must not throw even though the backup lacks the new table.
        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(legacyStream, ct));

        Assert.Null(exception);
        Assert.False(await db.PlaylistGenres.AnyAsync(ct));
        Assert.Equal(userId, (await db.Users.FirstAsync(ct)).Id);
    }

    /// <summary>
    /// Verifies that a backup taken before <c>Playlists.GenresManuallyOverridden</c> existed
    /// (Entwicklungsschritt 9, a legacy column-level gap analogous to
    /// <see cref="ReadFromAsync_LegacyBackupWithoutSortOrderColumnInPlaylistEntries_RestoresSuccessfully"/>
    /// above) can still be restored, with the missing column defaulting to <see langword="false"/> (i.e. the
    /// restored playlist behaves as if its genres were never manually overridden, the correct fallback for
    /// a pre-Schritt-9 backup).
    /// </summary>
    [Fact]
    public async Task ReadFromAsync_LegacyBackupWithoutGenresManuallyOverriddenColumnInPlaylists_RestoresSuccessfully()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-no-genres-overridden-column?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, userId) = await CreateBackupWithSeededDatabaseAsync(connection, ct);
        await using var _ = db;

        var playlist = new Playlist
        {
            UserId = userId,
            Name = "Legacy-Playlist",
            SortMode = PlaylistSortMode.ByReleaseDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            GenresManuallyOverridden = true
        };
        db.Playlists.Add(playlist);
        await db.SaveChangesAsync(ct);

        using var legacyStream = await BuildLegacyBackupStreamWithoutColumnAsync(backup, "Playlists", "GenresManuallyOverridden", ct);

        // This must not throw even though the backup lacks the new column.
        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(legacyStream, ct));

        Assert.Null(exception);
        // AsNoTracking: ReadFromAsync restores via raw SQL, bypassing the change tracker entirely, so the
        // still-tracked pre-restore "playlist" instance (GenresManuallyOverridden = true) would otherwise
        // win identity resolution over the freshly queried row here.
        var restoredPlaylist = await db.Playlists.AsNoTracking().SingleAsync(ct);
        Assert.False(restoredPlaylist.GenresManuallyOverridden);
        Assert.Equal(userId, (await db.Users.FirstAsync(ct)).Id);
    }

    /// <summary>
    /// Verifies that a backup taken before <c>Playlists.IsPublic</c> existed (Entwicklungsschritt 11,
    /// oeffentliche Playlists) can still be restored, with the missing column defaulting to
    /// <see langword="false"/> - the restored playlist stays private, the only safe fallback for a
    /// pre-Schritt-11 backup (no playlist may become visible to other users by accident).
    /// </summary>
    [Fact]
    public async Task ReadFromAsync_LegacyBackupWithoutIsPublicColumnInPlaylists_RestoresAsPrivate()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-no-ispublic-column?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, userId) = await CreateBackupWithSeededDatabaseAsync(connection, ct);
        await using var _ = db;

        db.Playlists.Add(new Playlist
        {
            UserId = userId,
            Name = "Legacy-Playlist-Ohne-IsPublic",
            SortMode = PlaylistSortMode.ByReleaseDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsPublic = true
        });
        await db.SaveChangesAsync(ct);

        using var legacyStream = await BuildLegacyBackupStreamWithoutColumnAsync(backup, "Playlists", "IsPublic", ct);

        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(legacyStream, ct));

        Assert.Null(exception);
        // AsNoTracking: the restore bypasses the change tracker (see the GenresManuallyOverridden test).
        var restoredPlaylist = await db.Playlists.AsNoTracking().SingleAsync(ct);
        Assert.False(restoredPlaylist.IsPublic);
        Assert.Equal(userId, (await db.Users.FirstAsync(ct)).Id);
    }

    /// <summary>
    /// Verifies that a current backup round-trips <c>Playlists.IsPublic</c> = <see langword="true"/>
    /// unchanged (the column is optional on restore only for legacy backups, it is exported normally).
    /// </summary>
    [Fact]
    public async Task WriteToAndReadFromAsync_PublicPlaylist_KeepsIsPublic()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-ispublic-roundtrip?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, userId) = await CreateBackupWithSeededDatabaseAsync(connection, ct);
        await using var _ = db;

        db.Playlists.Add(new Playlist
        {
            UserId = userId,
            Name = "Oeffentliche Playlist",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsPublic = true
        });
        await db.SaveChangesAsync(ct);

        using var stream = new MemoryStream();
        await backup.WriteToAsync(stream, ct);
        stream.Position = 0;
        await backup.ReadFromAsync(stream, ct);

        Assert.True((await db.Playlists.AsNoTracking().SingleAsync(ct)).IsPublic);
    }

    /// <summary>
    /// Verifies that a backup taken before <c>Playlists.CoverPictureId</c>/<c>Playlists.CoverPictureIsUserUploaded</c>
    /// existed (Entwicklungsschritt 10, Playlist-Abbildungen) can still be restored, with both missing
    /// columns defaulting to <see langword="null"/>/<see langword="false"/> respectively (i.e. the restored
    /// playlist behaves as if it never had a cover, which is always safe regardless of what the pre-Schritt-10
    /// backup's playlist actually looked like).
    /// </summary>
    [Fact]
    public async Task ReadFromAsync_LegacyBackupWithoutCoverColumnsInPlaylists_RestoresSuccessfully()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-no-cover-columns?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, userId) = await CreateBackupWithSeededDatabaseAsync(connection, ct);
        await using var _ = db;

        var playlist = new Playlist
        {
            UserId = userId,
            Name = "Legacy-Playlist-Ohne-Cover",
            SortMode = PlaylistSortMode.ByReleaseDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Playlists.Add(playlist);
        await db.SaveChangesAsync(ct);

        using var legacyStream = await BuildLegacyBackupStreamWithoutColumnsAsync(
            backup, "Playlists", new[] { "CoverPictureId", "CoverPictureIsUserUploaded" }, ct);

        // This must not throw even though the backup lacks the new columns.
        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(legacyStream, ct));

        Assert.Null(exception);
        var restoredPlaylist = await db.Playlists.AsNoTracking().SingleAsync(ct);
        Assert.Null(restoredPlaylist.CoverPictureId);
        Assert.False(restoredPlaylist.CoverPictureIsUserUploaded);
        Assert.Equal(userId, (await db.Users.FirstAsync(ct)).Id);
    }

    /// <summary>
    /// Verifies that a backup taken before <c>Pictures.PlaylistId</c> existed (Entwicklungsschritt 10,
    /// Playlist-Abbildungen - the back-reference used for Cover-Picture cleanup, analogous to
    /// <c>Pictures.EpisodeId</c>) can still be restored, with the missing column defaulting to
    /// <see langword="null"/>.
    /// </summary>
    [Fact]
    public async Task ReadFromAsync_LegacyBackupWithoutPlaylistIdColumnInPictures_RestoresSuccessfully()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-no-picture-playlistid?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, userId) = await CreateBackupWithSeededDatabaseAsync(connection, ct);
        await using var _ = db;

        var playlist = new Playlist
        {
            UserId = userId,
            Name = "Legacy-Playlist-Mit-Bild",
            SortMode = PlaylistSortMode.ByReleaseDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Playlists.Add(playlist);
        await db.SaveChangesAsync(ct);
        db.Pictures.Add(new Picture { Type = "cover", Data = new byte[] { 1 }, ContentType = "image/jpeg", PlaylistId = playlist.Id });
        await db.SaveChangesAsync(ct);

        using var legacyStream = await BuildLegacyBackupStreamWithoutColumnAsync(backup, "Pictures", "PlaylistId", ct);

        // This must not throw even though the backup lacks the new column.
        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(legacyStream, ct));

        Assert.Null(exception);
        var restoredPicture = await db.Pictures.AsNoTracking().SingleAsync(p => p.Data.Length == 1, ct);
        Assert.Null(restoredPicture.PlaylistId);
        Assert.Equal(userId, (await db.Users.FirstAsync(ct)).Id);
    }

    /// <summary>
    /// Verifies the actual anti-orphan behavior (not just backward compatibility with a pre-Schritt-10
    /// backup, see <see cref="ReadFromAsync_LegacyBackupWithoutCoverColumnsInPlaylists_RestoresSuccessfully"/>
    /// above) for a playlist whose cover was automatically generated (<see cref="Playlist.CoverPictureIsUserUploaded"/>
    /// = <see langword="false"/>): the underlying <see cref="Picture"/> (<see cref="Picture.IsGeneratedBackground"/>
    /// = <see langword="true"/>) is excluded from the export by <c>BuildTableFilter</c>, so
    /// <c>BuildColumnSelectExpression</c>'s <c>CASE</c> override must export <c>NULL</c> for
    /// <c>Playlists.CoverPictureId</c> instead of the picture's id - otherwise the restore would leave a
    /// foreign key referencing a row that was never written back, which <see cref="ReadFromAsync"/> would
    /// catch via <c>PRAGMA foreign_key_check</c> (<c>EnsureNoSqliteForeignKeyViolationsAsync</c>) and fail
    /// the whole restore.
    /// </summary>
    [Fact]
    public async Task ReadFromAsync_RoundTripWithGeneratedCover_ClearsOrphanCoverPictureId()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-cover-generated?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, userId) = await CreateBackupWithSeededDatabaseAsync(connection, ct);
        await using var _ = db;

        var picture = new Picture
        {
            Type = "cover",
            Data = new byte[] { 1, 2, 3 },
            ContentType = "image/jpeg",
            IsGeneratedBackground = true
        };
        db.Pictures.Add(picture);
        await db.SaveChangesAsync(ct);

        var playlist = new Playlist
        {
            UserId = userId,
            Name = "Playlist-Mit-Generiertem-Cover",
            SortMode = PlaylistSortMode.ByReleaseDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CoverPictureId = picture.Id,
            CoverPictureIsUserUploaded = false
        };
        db.Playlists.Add(playlist);
        await db.SaveChangesAsync(ct);
        picture.PlaylistId = playlist.Id;
        await db.SaveChangesAsync(ct);

        using var backupStream = new MemoryStream();
        await backup.WriteToAsync(backupStream, ct);
        backupStream.Position = 0;

        // This must not throw (in particular no SQLite foreign key violation) even though the generated
        // picture was intentionally left out of the backup.
        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(backupStream, ct));

        Assert.Null(exception);
        var restoredPlaylist = await db.Playlists.AsNoTracking().SingleAsync(p => p.Name == "Playlist-Mit-Generiertem-Cover", ct);
        Assert.Null(restoredPlaylist.CoverPictureId);
        Assert.False(await db.Pictures.AnyAsync(p => p.Id == picture.Id, ct));
    }

    /// <summary>
    /// Counterpart of <see cref="ReadFromAsync_RoundTripWithGeneratedCover_ClearsOrphanCoverPictureId"/> for
    /// an uploaded cover (<see cref="Playlist.CoverPictureIsUserUploaded"/> = <see langword="true"/>): its
    /// underlying <see cref="Picture"/> is not filtered out of the export (only
    /// <see cref="Picture.IsGeneratedBackground"/> pictures are), so <c>Playlists.CoverPictureId</c> must
    /// survive the round trip unchanged instead of being nulled out.
    /// </summary>
    [Fact]
    public async Task ReadFromAsync_RoundTripWithUploadedCover_PreservesCoverPictureId()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-cover-uploaded?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, userId) = await CreateBackupWithSeededDatabaseAsync(connection, ct);
        await using var _ = db;

        var picture = new Picture
        {
            Type = "cover",
            Data = new byte[] { 4, 5, 6 },
            ContentType = "image/jpeg",
            IsGeneratedBackground = false
        };
        db.Pictures.Add(picture);
        await db.SaveChangesAsync(ct);

        var playlist = new Playlist
        {
            UserId = userId,
            Name = "Playlist-Mit-Hochgeladenem-Cover",
            SortMode = PlaylistSortMode.ByReleaseDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CoverPictureId = picture.Id,
            CoverPictureIsUserUploaded = true
        };
        db.Playlists.Add(playlist);
        await db.SaveChangesAsync(ct);
        picture.PlaylistId = playlist.Id;
        await db.SaveChangesAsync(ct);

        using var backupStream = new MemoryStream();
        await backup.WriteToAsync(backupStream, ct);
        backupStream.Position = 0;

        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(backupStream, ct));

        Assert.Null(exception);
        var restoredPlaylist = await db.Playlists.AsNoTracking().SingleAsync(p => p.Name == "Playlist-Mit-Hochgeladenem-Cover", ct);
        Assert.Equal(picture.Id, restoredPlaylist.CoverPictureId);
        Assert.True(restoredPlaylist.CoverPictureIsUserUploaded);
        Assert.True(await db.Pictures.AnyAsync(p => p.Id == picture.Id, ct));
    }

    /// <summary>
    /// Same as <see cref="BuildLegacyBackupStreamWithoutColumnAsync"/>, removing several columns from the
    /// same table at once (used where a single legacy backup plausibly predates more than one column added
    /// together, e.g. <c>CoverPictureId</c> and <c>CoverPictureIsUserUploaded</c> - both introduced by the
    /// same Entwicklungsschritt-10 migration).
    /// </summary>
    /// <param name="backup">The current-schema backup to derive the legacy archive from.</param>
    /// <param name="tableName">The name of the table to remove the columns from.</param>
    /// <param name="columnNames">The names of the columns to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rebuilt legacy backup archive, positioned at the start.</returns>
    private static async Task<MemoryStream> BuildLegacyBackupStreamWithoutColumnsAsync(
        VideoWebPlayerBackupData backup, string tableName, string[] columnNames, CancellationToken cancellationToken)
    {
        using var currentStream = new MemoryStream();
        await backup.WriteToAsync(currentStream, cancellationToken);
        currentStream.Position = 0;

        using var currentArchive = new ZipArchive(currentStream, ZipArchiveMode.Read, true);
        var indexEntry = currentArchive.GetEntry("index.json")!;
        JsonNode indexNode;
        await using (var indexStream = indexEntry.Open())
        {
            indexNode = (await JsonNode.ParseAsync(indexStream, cancellationToken: cancellationToken))!;
        }

        var tableEntry = indexNode["tables"]!.AsArray()
            .Single(t => string.Equals(t!["name"]!.GetValue<string>(), tableName, StringComparison.OrdinalIgnoreCase));
        var columnsArray = tableEntry!["columns"]!.AsArray();
        foreach (var columnName in columnNames)
        {
            var columnNode = columnsArray.FirstOrDefault(c => string.Equals(c!.GetValue<string>(), columnName, StringComparison.OrdinalIgnoreCase));
            if (columnNode is not null)
                columnsArray.Remove(columnNode);
        }

        var entryName = tableEntry["entryName"]!.GetValue<string>();
        var dataEntry = currentArchive.GetEntry(entryName)!;
        JsonNode dataNode;
        await using (var dataStream = dataEntry.Open())
        {
            dataNode = (await JsonNode.ParseAsync(dataStream, cancellationToken: cancellationToken))!;
        }
        foreach (var row in dataNode["rows"]!.AsArray())
        {
            foreach (var columnName in columnNames)
                row!.AsObject().Remove(columnName);
        }

        var result = new MemoryStream();
        using (var resultArchive = new ZipArchive(result, ZipArchiveMode.Create, true))
        {
            foreach (var entry in currentArchive.Entries)
            {
                var newEntry = resultArchive.CreateEntry(entry.FullName);
                if (string.Equals(entry.FullName, "index.json", StringComparison.Ordinal))
                {
                    await using var writeStream = newEntry.Open();
                    await JsonSerializer.SerializeAsync(writeStream, indexNode, JsonOptions, cancellationToken);
                }
                else if (string.Equals(entry.FullName, entryName, StringComparison.Ordinal))
                {
                    await using var writeStream = newEntry.Open();
                    await JsonSerializer.SerializeAsync(writeStream, dataNode, JsonOptions, cancellationToken);
                }
                else
                {
                    await using var sourceStream = entry.Open();
                    await using var writeStream = newEntry.Open();
                    await sourceStream.CopyToAsync(writeStream, cancellationToken);
                }
            }
        }

        result.Position = 0;
        return result;
    }

    /// <summary>
    /// Backs up <paramref name="backup"/>'s current schema and rebuilds the archive with the given
    /// table's given column removed from both the index metadata's column list and every already-backed-up
    /// row's data, simulating a backup taken before that column existed. Generic counterpart of
    /// <see cref="BuildLegacyBackupStreamWithoutPlaylistEntriesSortOrderColumnAsync"/> and
    /// <see cref="BuildLegacyBackupStreamWithoutContinueWatchingEntriesPlaylistIdColumnAsync"/> above, used
    /// where a single additional case does not warrant its own dedicated near-duplicate method.
    /// </summary>
    /// <param name="backup">The current-schema backup to derive the legacy archive from.</param>
    /// <param name="tableName">The name of the table to remove the column from.</param>
    /// <param name="columnName">The name of the column to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rebuilt legacy backup archive, positioned at the start.</returns>
    private static async Task<MemoryStream> BuildLegacyBackupStreamWithoutColumnAsync(
        VideoWebPlayerBackupData backup, string tableName, string columnName, CancellationToken cancellationToken)
    {
        using var currentStream = new MemoryStream();
        await backup.WriteToAsync(currentStream, cancellationToken);
        currentStream.Position = 0;

        using var originalArchive = new ZipArchive(currentStream, ZipArchiveMode.Read, true);
        var legacyStream = new MemoryStream();

        using (var legacyArchive = new ZipArchive(legacyStream, ZipArchiveMode.Create, true))
        {
            var indexEntry = originalArchive.GetEntry("index.json")!;
            JsonNode? indexNode;
            using (var indexStream = indexEntry.Open())
            {
                indexNode = await JsonNode.ParseAsync(indexStream, cancellationToken: cancellationToken);
            }

            var tables = indexNode!["tables"]!.AsArray();
            var table = tables.First(t =>
                string.Equals(t!["name"]!.GetValue<string>(), tableName, StringComparison.OrdinalIgnoreCase))!;
            var entryName = table["entryName"]!.GetValue<string>();

            var columns = table["columns"]!.AsArray();
            var column = columns.FirstOrDefault(c => string.Equals(c!.GetValue<string>(), columnName, StringComparison.OrdinalIgnoreCase));
            if (column is not null)
                columns.Remove(column);

            var newIndexEntry = legacyArchive.CreateEntry("index.json");
            using (var newIndexStream = newIndexEntry.Open())
            {
                await using var writer = new Utf8JsonWriter(newIndexStream, new JsonWriterOptions { Indented = true });
                indexNode!.WriteTo(writer, JsonOptions);
                await writer.FlushAsync(cancellationToken);
            }

            foreach (var entry in originalArchive.Entries)
            {
                if (entry.FullName == "index.json")
                    continue;

                if (string.Equals(entry.FullName, entryName, StringComparison.OrdinalIgnoreCase))
                {
                    JsonNode? dataNode;
                    using (var dataStream = entry.Open())
                    {
                        dataNode = await JsonNode.ParseAsync(dataStream, cancellationToken: cancellationToken);
                    }

                    foreach (var row in dataNode!["rows"]!.AsArray())
                        row!.AsObject().Remove(columnName);

                    var newDataEntry = legacyArchive.CreateEntry(entry.FullName);
                    using (var newDataStream = newDataEntry.Open())
                    {
                        await using var writer = new Utf8JsonWriter(newDataStream, new JsonWriterOptions { Indented = true });
                        dataNode!.WriteTo(writer, JsonOptions);
                        await writer.FlushAsync(cancellationToken);
                    }
                }
                else
                {
                    var newEntry = legacyArchive.CreateEntry(entry.FullName);
                    using var sourceStream = entry.Open();
                    using var destinationStream = newEntry.Open();
                    await sourceStream.CopyToAsync(destinationStream, cancellationToken);
                }
            }
        }

        legacyStream.Position = 0;
        return legacyStream;
    }

    /// <summary>
    /// Verifies that a backup taken before <c>PlaylistEntries.SortOrder</c> existed (a legacy column-level
    /// gap, distinct from the whole-table-missing case covered by
    /// <see cref="ReadFromAsync_LegacyBackupWithoutPlaylistsAndPlaylistEntries_RestoresSuccessfully"/>
    /// above) can still be restored, with the missing column defaulting to <c>null</c>.
    /// </summary>
    [Fact]
    public async Task ReadFromAsync_LegacyBackupWithoutSortOrderColumnInPlaylistEntries_RestoresSuccessfully()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-no-sortorder-column?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, userId) = await CreateBackupWithSeededDatabaseAsync(connection, ct);
        await using var _ = db;

        var playlist = new Playlist
        {
            UserId = userId,
            Name = "Legacy-Playlist",
            SortMode = PlaylistSortMode.ByReleaseDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Playlists.Add(playlist);
        await db.SaveChangesAsync(ct);
        db.PlaylistEntries.Add(new PlaylistEntry
        {
            PlaylistId = playlist.Id,
            MediaType = "Movie",
            MediaId = 1,
            AddedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);

        using var legacyStream = await BuildLegacyBackupStreamWithoutPlaylistEntriesSortOrderColumnAsync(backup, ct);

        // This must not throw even though the backup lacks the new column.
        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(legacyStream, ct));

        Assert.Null(exception);
        var restoredEntry = await db.PlaylistEntries.SingleAsync(ct);
        Assert.Null(restoredEntry.SortOrder);
        Assert.Equal(userId, (await db.Users.FirstAsync(ct)).Id);
    }

    /// <summary>
    /// Verifies that a backup taken before <c>ContinueWatchingEntries.PlaylistId</c> existed (a legacy
    /// column-level gap, analogous to
    /// <see cref="ReadFromAsync_LegacyBackupWithoutSortOrderColumnInPlaylistEntries_RestoresSuccessfully"/>
    /// above for <c>PlaylistEntries.SortOrder</c>) can still be restored, with the missing column
    /// defaulting to <c>null</c> - i.e. the restored entry behaves as if it has no playlist context, which
    /// is the correct fallback for a pre-Schritt-6 backup.
    /// </summary>
    [Fact]
    public async Task ReadFromAsync_LegacyBackupWithoutPlaylistIdColumnInContinueWatchingEntries_RestoresSuccessfully()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-no-playlistid-column?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, userId) = await CreateBackupWithSeededDatabaseAsync(connection, ct);
        await using var _ = db;

        db.ContinueWatchingEntries.Add(new ContinueWatchingEntry
        {
            UserId = userId,
            Position = TimeSpan.FromSeconds(300),
            UpdatedAt = DateTime.UtcNow,
            ListOrder = 1
        });
        await db.SaveChangesAsync(ct);

        using var legacyStream = await BuildLegacyBackupStreamWithoutContinueWatchingEntriesPlaylistIdColumnAsync(backup, ct);

        // This must not throw even though the backup lacks the new column.
        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(legacyStream, ct));

        Assert.Null(exception);
        var restoredEntry = await db.ContinueWatchingEntries.SingleAsync(ct);
        Assert.Null(restoredEntry.PlaylistId);
        Assert.Equal(userId, (await db.Users.FirstAsync(ct)).Id);
    }

    /// <summary>
    /// Prepares a current-schema database (over the given, already-open SQLite in-memory shared-cache
    /// connection) with a valid admin user and setup, and the <see cref="VideoWebPlayerBackupData"/>
    /// instance to back it up/restore into. Shared arrange logic for every legacy-backup-compatibility
    /// test above; the connection stays owned (and disposed via <c>using</c>) by the calling test method,
    /// since the in-memory shared-cache database only exists while it is open.
    /// </summary>
    /// <param name="connection">The already-open SQLite in-memory connection to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The seeded <see cref="ApplicationDbContext"/> (caller-owned, must be disposed), the <see cref="VideoWebPlayerBackupData"/> instance, and the seeded admin user's id.</returns>
    private static async Task<(ApplicationDbContext Db, VideoWebPlayerBackupData Backup, string UserId)> CreateBackupWithSeededDatabaseAsync(
        SqliteConnection connection, CancellationToken cancellationToken)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new ApplicationDbContext(options, new EventManager());
        await db.Database.EnsureCreatedAsync(cancellationToken);

        var userId = Guid.NewGuid().ToString();
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = "admin",
            NormalizedUserName = "ADMIN",
            Email = "admin@test.de",
            NormalizedEmail = "ADMIN@TEST.DE",
            PasswordHash = "hash",
            SecurityStamp = "stamp",
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            Sources = string.Empty,
            IsAdmin = true
        });

        db.Setups.Add(new Setup
        {
            DataVersion = 1,
            GenresChanged = false,
            ContinueWatchingEndThresholdSeconds = 42
        });

        await db.SaveChangesAsync(cancellationToken);

        var environment = new FakeWebHostEnvironment();
        var logger = NullLogger<VideoWebPlayerBackupData>.Instance;
        var factory = new VideoWebPlayerBackupDataFactory(new ServiceCollection().BuildServiceProvider(), environment, logger)
        {
            UserId = userId
        };

        var backup = new VideoWebPlayerBackupData("test", "VideoWebPlayer:Database", db, environment, logger, factory);

        return (db, backup, userId);
    }

    /// <summary>
    /// Backs up <paramref name="backup"/>'s current schema and rebuilds the archive without the given
    /// table(s) (removed from <c>index.json</c>'s <c>tables</c> array and their data entries dropped),
    /// simulating a backup taken before those tables existed.
    /// </summary>
    /// <param name="backup">The current-schema backup to derive the legacy archive from.</param>
    /// <param name="tableNamesToRemove">The table names to remove from the archive.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rebuilt legacy backup archive, positioned at the start.</returns>
    private static async Task<MemoryStream> BuildLegacyBackupStreamRemovingTablesAsync(
        VideoWebPlayerBackupData backup, IReadOnlyCollection<string> tableNamesToRemove, CancellationToken cancellationToken)
    {
        using var currentStream = new MemoryStream();
        await backup.WriteToAsync(currentStream, cancellationToken);
        currentStream.Position = 0;

        using var originalArchive = new ZipArchive(currentStream, ZipArchiveMode.Read, true);
        var legacyStream = new MemoryStream();

        using (var legacyArchive = new ZipArchive(legacyStream, ZipArchiveMode.Create, true))
        {
            var indexEntry = originalArchive.GetEntry("index.json")!;
            JsonNode? indexNode;
            using (var indexStream = indexEntry.Open())
            {
                indexNode = await JsonNode.ParseAsync(indexStream, cancellationToken: cancellationToken);
            }

            var tables = indexNode!["tables"]!.AsArray();
            var removedEntryNames = new List<string>();

            foreach (var tableName in tableNamesToRemove)
            {
                var table = tables.FirstOrDefault(t =>
                    string.Equals(t!["name"]!.GetValue<string>(), tableName, StringComparison.OrdinalIgnoreCase));
                if (table is null)
                    continue;

                removedEntryNames.Add(table["entryName"]!.GetValue<string>());
                tables.RemoveAt(tables.IndexOf(table));
            }

            var newIndexEntry = legacyArchive.CreateEntry("index.json");
            using (var newIndexStream = newIndexEntry.Open())
            {
                await using var writer = new Utf8JsonWriter(newIndexStream, new JsonWriterOptions { Indented = true });
                indexNode!.WriteTo(writer, JsonOptions);
                await writer.FlushAsync(cancellationToken);
            }

            foreach (var entry in originalArchive.Entries)
            {
                if (entry.FullName == "index.json")
                    continue;

                if (removedEntryNames.Any(name => string.Equals(entry.FullName, name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var newEntry = legacyArchive.CreateEntry(entry.FullName);
                using var sourceStream = entry.Open();
                using var destinationStream = newEntry.Open();
                await sourceStream.CopyToAsync(destinationStream, cancellationToken);
            }
        }

        legacyStream.Position = 0;
        return legacyStream;
    }

    /// <summary>
    /// Backs up <paramref name="backup"/>'s current schema and rebuilds the archive with an old-style
    /// <c>Setups</c> table that lacks the <c>ContinueWatchingEndThresholdSeconds</c> column, simulating a
    /// backup taken before that column existed.
    /// </summary>
    /// <param name="backup">The current-schema backup to derive the legacy archive from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rebuilt legacy backup archive, positioned at the start.</returns>
    private static async Task<MemoryStream> BuildLegacyBackupStreamWithoutContinueWatchingThresholdColumnAsync(
        VideoWebPlayerBackupData backup, CancellationToken cancellationToken)
    {
        using var currentStream = new MemoryStream();
        await backup.WriteToAsync(currentStream, cancellationToken);
        currentStream.Position = 0;

        using var originalArchive = new ZipArchive(currentStream, ZipArchiveMode.Read, true);
        var legacyStream = new MemoryStream();

        using (var legacyArchive = new ZipArchive(legacyStream, ZipArchiveMode.Create, true))
        {
            var indexEntry = originalArchive.GetEntry("index.json")!;
            JsonNode? indexNode;
            using (var indexStream = indexEntry.Open())
            {
                indexNode = await JsonNode.ParseAsync(indexStream, cancellationToken: cancellationToken);
            }

            var tables = indexNode!["tables"]!.AsArray();
            var setupsTable = tables.First(t =>
                string.Equals(t!["name"]!.GetValue<string>(), "Setups", StringComparison.OrdinalIgnoreCase))!;
            var setupsEntryName = setupsTable["entryName"]!.GetValue<string>();

            // Build an old-style Setups table without ContinueWatchingEndThresholdSeconds.
            setupsTable["columns"] = new JsonArray
            {
                "Id",
                "DataVersion",
                "GenresChanged",
                "ApplicationTitle",
                "ScanProcessIntervalMinutes",
                "MediaCollectionScanIntervalDays"
            };

            var newIndexEntry = legacyArchive.CreateEntry("index.json");
            using (var newIndexStream = newIndexEntry.Open())
            {
                await using var writer = new Utf8JsonWriter(newIndexStream, new JsonWriterOptions { Indented = true });
                indexNode!.WriteTo(writer, JsonOptions);
                await writer.FlushAsync(cancellationToken);
            }

            foreach (var entry in originalArchive.Entries)
            {
                if (entry.FullName == "index.json")
                    continue;

                if (string.Equals(entry.FullName, setupsEntryName, StringComparison.OrdinalIgnoreCase))
                {
                    var legacySetupsData = new JsonObject
                    {
                        ["rows"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["Id"] = 1,
                                ["DataVersion"] = 1,
                                ["GenresChanged"] = false,
                                ["ApplicationTitle"] = "Martins Videosammlung",
                                ["ScanProcessIntervalMinutes"] = 60,
                                ["MediaCollectionScanIntervalDays"] = 7
                            }
                        }
                    };

                    var newSetupsEntry = legacyArchive.CreateEntry(entry.FullName);
                    using (var newSetupsStream = newSetupsEntry.Open())
                    {
                        await using var writer = new Utf8JsonWriter(newSetupsStream, new JsonWriterOptions { Indented = true });
                        legacySetupsData.WriteTo(writer, JsonOptions);
                        await writer.FlushAsync(cancellationToken);
                    }
                }
                else
                {
                    var newEntry = legacyArchive.CreateEntry(entry.FullName);
                    using var sourceStream = entry.Open();
                    using var destinationStream = newEntry.Open();
                    await sourceStream.CopyToAsync(destinationStream, cancellationToken);
                }
            }
        }

        legacyStream.Position = 0;
        return legacyStream;
    }

    /// <summary>
    /// Backs up <paramref name="backup"/>'s current schema and rebuilds the archive with the
    /// <c>PlaylistEntries</c> table's <c>SortOrder</c> column removed from both the index metadata's
    /// column list and every already-backed-up row's data, simulating a backup taken before that column
    /// existed (as opposed to <see cref="BuildLegacyBackupStreamRemovingTablesAsync"/>, which removes an
    /// entire table).
    /// </summary>
    /// <param name="backup">The current-schema backup to derive the legacy archive from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rebuilt legacy backup archive, positioned at the start.</returns>
    private static async Task<MemoryStream> BuildLegacyBackupStreamWithoutPlaylistEntriesSortOrderColumnAsync(
        VideoWebPlayerBackupData backup, CancellationToken cancellationToken)
    {
        using var currentStream = new MemoryStream();
        await backup.WriteToAsync(currentStream, cancellationToken);
        currentStream.Position = 0;

        using var originalArchive = new ZipArchive(currentStream, ZipArchiveMode.Read, true);
        var legacyStream = new MemoryStream();

        using (var legacyArchive = new ZipArchive(legacyStream, ZipArchiveMode.Create, true))
        {
            var indexEntry = originalArchive.GetEntry("index.json")!;
            JsonNode? indexNode;
            using (var indexStream = indexEntry.Open())
            {
                indexNode = await JsonNode.ParseAsync(indexStream, cancellationToken: cancellationToken);
            }

            var tables = indexNode!["tables"]!.AsArray();
            var playlistEntriesTable = tables.First(t =>
                string.Equals(t!["name"]!.GetValue<string>(), "PlaylistEntries", StringComparison.OrdinalIgnoreCase))!;
            var playlistEntriesEntryName = playlistEntriesTable["entryName"]!.GetValue<string>();

            var columns = playlistEntriesTable["columns"]!.AsArray();
            var sortOrderColumn = columns.FirstOrDefault(c => string.Equals(c!.GetValue<string>(), "SortOrder", StringComparison.OrdinalIgnoreCase));
            if (sortOrderColumn is not null)
                columns.Remove(sortOrderColumn);

            var newIndexEntry = legacyArchive.CreateEntry("index.json");
            using (var newIndexStream = newIndexEntry.Open())
            {
                await using var writer = new Utf8JsonWriter(newIndexStream, new JsonWriterOptions { Indented = true });
                indexNode!.WriteTo(writer, JsonOptions);
                await writer.FlushAsync(cancellationToken);
            }

            foreach (var entry in originalArchive.Entries)
            {
                if (entry.FullName == "index.json")
                    continue;

                if (string.Equals(entry.FullName, playlistEntriesEntryName, StringComparison.OrdinalIgnoreCase))
                {
                    JsonNode? dataNode;
                    using (var dataStream = entry.Open())
                    {
                        dataNode = await JsonNode.ParseAsync(dataStream, cancellationToken: cancellationToken);
                    }

                    foreach (var row in dataNode!["rows"]!.AsArray())
                    {
                        row!.AsObject().Remove("SortOrder");
                    }

                    var newDataEntry = legacyArchive.CreateEntry(entry.FullName);
                    using (var newDataStream = newDataEntry.Open())
                    {
                        await using var writer = new Utf8JsonWriter(newDataStream, new JsonWriterOptions { Indented = true });
                        dataNode!.WriteTo(writer, JsonOptions);
                        await writer.FlushAsync(cancellationToken);
                    }
                }
                else
                {
                    var newEntry = legacyArchive.CreateEntry(entry.FullName);
                    using var sourceStream = entry.Open();
                    using var destinationStream = newEntry.Open();
                    await sourceStream.CopyToAsync(destinationStream, cancellationToken);
                }
            }
        }

        legacyStream.Position = 0;
        return legacyStream;
    }

    /// <summary>
    /// Backs up <paramref name="backup"/>'s current schema and rebuilds the archive with the
    /// <c>ContinueWatchingEntries</c> table's <c>PlaylistId</c> column removed from both the index
    /// metadata's column list and every already-backed-up row's data, simulating a backup taken before
    /// that column existed (analogous to
    /// <see cref="BuildLegacyBackupStreamWithoutPlaylistEntriesSortOrderColumnAsync"/> above for
    /// <c>PlaylistEntries.SortOrder</c>).
    /// </summary>
    /// <param name="backup">The current-schema backup to derive the legacy archive from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rebuilt legacy backup archive, positioned at the start.</returns>
    private static async Task<MemoryStream> BuildLegacyBackupStreamWithoutContinueWatchingEntriesPlaylistIdColumnAsync(
        VideoWebPlayerBackupData backup, CancellationToken cancellationToken)
    {
        using var currentStream = new MemoryStream();
        await backup.WriteToAsync(currentStream, cancellationToken);
        currentStream.Position = 0;

        using var originalArchive = new ZipArchive(currentStream, ZipArchiveMode.Read, true);
        var legacyStream = new MemoryStream();

        using (var legacyArchive = new ZipArchive(legacyStream, ZipArchiveMode.Create, true))
        {
            var indexEntry = originalArchive.GetEntry("index.json")!;
            JsonNode? indexNode;
            using (var indexStream = indexEntry.Open())
            {
                indexNode = await JsonNode.ParseAsync(indexStream, cancellationToken: cancellationToken);
            }

            var tables = indexNode!["tables"]!.AsArray();
            var continueWatchingEntriesTable = tables.First(t =>
                string.Equals(t!["name"]!.GetValue<string>(), "ContinueWatchingEntries", StringComparison.OrdinalIgnoreCase))!;
            var continueWatchingEntriesEntryName = continueWatchingEntriesTable["entryName"]!.GetValue<string>();

            var columns = continueWatchingEntriesTable["columns"]!.AsArray();
            var playlistIdColumn = columns.FirstOrDefault(c => string.Equals(c!.GetValue<string>(), "PlaylistId", StringComparison.OrdinalIgnoreCase));
            if (playlistIdColumn is not null)
                columns.Remove(playlistIdColumn);

            var newIndexEntry = legacyArchive.CreateEntry("index.json");
            using (var newIndexStream = newIndexEntry.Open())
            {
                await using var writer = new Utf8JsonWriter(newIndexStream, new JsonWriterOptions { Indented = true });
                indexNode!.WriteTo(writer, JsonOptions);
                await writer.FlushAsync(cancellationToken);
            }

            foreach (var entry in originalArchive.Entries)
            {
                if (entry.FullName == "index.json")
                    continue;

                if (string.Equals(entry.FullName, continueWatchingEntriesEntryName, StringComparison.OrdinalIgnoreCase))
                {
                    JsonNode? dataNode;
                    using (var dataStream = entry.Open())
                    {
                        dataNode = await JsonNode.ParseAsync(dataStream, cancellationToken: cancellationToken);
                    }

                    foreach (var row in dataNode!["rows"]!.AsArray())
                    {
                        row!.AsObject().Remove("PlaylistId");
                    }

                    var newDataEntry = legacyArchive.CreateEntry(entry.FullName);
                    using (var newDataStream = newDataEntry.Open())
                    {
                        await using var writer = new Utf8JsonWriter(newDataStream, new JsonWriterOptions { Indented = true });
                        dataNode!.WriteTo(writer, JsonOptions);
                        await writer.FlushAsync(cancellationToken);
                    }
                }
                else
                {
                    var newEntry = legacyArchive.CreateEntry(entry.FullName);
                    using var sourceStream = entry.Open();
                    using var destinationStream = newEntry.Open();
                    await sourceStream.CopyToAsync(destinationStream, cancellationToken);
                }
            }
        }

        legacyStream.Position = 0;
        return legacyStream;
    }

    private static string GetSetupsEntryName(ZipArchive archive)
    {
        var indexEntry = archive.GetEntry("index.json")!;
        using var indexStream = indexEntry.Open();
        var indexNode = JsonNode.Parse(indexStream)!;
        var tables = indexNode["tables"]!.AsArray();
        var setupsTable = tables.First(t =>
            string.Equals(t!["name"]!.GetValue<string>(), "Setups", StringComparison.OrdinalIgnoreCase))!;
        return setupsTable["entryName"]!.GetValue<string>();
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "VideoWebPlayer";
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public IFileProvider WebRootFileProvider { get; set; } = null!;
    }
}
