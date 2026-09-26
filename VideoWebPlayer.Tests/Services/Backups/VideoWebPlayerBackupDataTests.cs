using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Backups;
using VideoWebPlayer.Tests.Helpers;
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
        var (db, backup, userId) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
        await using var _ = db;

        using var legacyStream = await LegacyBackupArchiveBuilder.RemoveTablesAsync(backup, new[] { "UnlockedMediaEntries" }, ct);

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
        var (db, backup, userId) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
        await using var _ = db;

        using var legacyStream = await LegacyBackupArchiveBuilder.RemoveTablesAsync(backup, new[] { "WatchedEntries" }, ct);

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
        var (db, backup, userId) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
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
        var (db, backup, userId) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
        await using var _ = db;

        using var legacyStream = await LegacyBackupArchiveBuilder.RemoveTablesAsync(backup, new[] { "Playlists", "PlaylistEntries" }, ct);

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
        var (db, backup, userId) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
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

        using var legacyStream = await LegacyBackupArchiveBuilder.RemoveTablesAsync(backup, new[] { "PlaylistEntryExclusions" }, ct);

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
        var (db, backup, userId) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
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

        using var legacyStream = await LegacyBackupArchiveBuilder.RemoveTablesAsync(backup, new[] { "PlaylistGenres" }, ct);

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
        var (db, backup, userId) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
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

        using var legacyStream = await LegacyBackupArchiveBuilder.RemoveColumnsAsync(backup, "Playlists", new[] { "GenresManuallyOverridden" }, ct);

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
        var (db, backup, userId) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
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

        using var legacyStream = await LegacyBackupArchiveBuilder.RemoveColumnsAsync(backup, "Playlists", new[] { "IsPublic" }, ct);

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
        var (db, backup, userId) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
        await using var _ = db;

        db.Playlists.Add(new Playlist
        {
            UserId = userId,
            Name = "Öffentliche Playlist",
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
        var (db, backup, userId) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
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

        using var legacyStream = await LegacyBackupArchiveBuilder.RemoveColumnsAsync(
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
        var (db, backup, userId) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
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

        using var legacyStream = await LegacyBackupArchiveBuilder.RemoveColumnsAsync(backup, "Pictures", new[] { "PlaylistId" }, ct);

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
        var (db, backup, userId) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
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
        var (db, backup, userId) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
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
    /// Verifies that a backup taken before the <c>PlaylistBackfillMarkers</c> table existed (automatic
    /// playlist backfill driven by markers, correction of Entwicklungsschritt 8) can still be restored: the
    /// table is optional, and no marker remains afterwards (the daily safety sweep catches up whatever a
    /// pre-marker backup could not know about).
    /// </summary>
    [Fact]
    public async Task ReadFromAsync_LegacyBackupWithoutPlaylistBackfillMarkers_RestoresSuccessfully()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-no-backfill-markers?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, _) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
        await using var __ = db;

        db.PlaylistBackfillMarkers.Add(new PlaylistBackfillMarker { MediaType = "TVShow", MediaId = 7 });
        await db.SaveChangesAsync(ct);

        using var legacyStream = await LegacyBackupArchiveBuilder.RemoveTablesAsync(backup, new[] { "PlaylistBackfillMarkers" }, ct);

        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(legacyStream, ct));

        Assert.Null(exception);
        Assert.False(await db.PlaylistBackfillMarkers.AsNoTracking().AnyAsync(ct));
    }

    /// <summary>
    /// Verifies that a current backup round-trips the pending backfill markers, so a restore does not lose
    /// "this collection medium has new children".
    /// </summary>
    [Fact]
    public async Task WriteToAndReadFromAsync_PlaylistBackfillMarkers_AreKept()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-backfill-markers-roundtrip?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, _) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
        await using var __ = db;

        db.PlaylistBackfillMarkers.Add(new PlaylistBackfillMarker { MediaType = "MovieCollection", MediaId = 3, Version = 4 });
        await db.SaveChangesAsync(ct);

        using var stream = new MemoryStream();
        await backup.WriteToAsync(stream, ct);
        stream.Position = 0;
        await backup.ReadFromAsync(stream, ct);

        var marker = await db.PlaylistBackfillMarkers.AsNoTracking().SingleAsync(ct);
        Assert.Equal("MovieCollection", marker.MediaType);
        Assert.Equal(3, marker.MediaId);
        Assert.Equal(4, marker.Version);
    }

    /// <summary>
    /// Verifies that a backup taken before <c>Setups.PlaylistBackfillLastSweepAt</c> existed can still be
    /// restored, with the missing column staying <see langword="null"/> (the safety sweep then counts as never
    /// run and is caught up shortly after the next start).
    /// </summary>
    [Fact]
    public async Task ReadFromAsync_LegacyBackupWithoutPlaylistBackfillLastSweepAtColumn_RestoresWithNull()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-no-lastsweep-column?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, _) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
        await using var __ = db;

        var setup = await db.Setups.FirstOrDefaultAsync(ct);
        if (setup is null)
        {
            setup = new Setup();
            db.Setups.Add(setup);
        }
        setup.PlaylistBackfillLastSweepAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        using var legacyStream = await LegacyBackupArchiveBuilder.RemoveColumnsAsync(backup, "Setups", new[] { "PlaylistBackfillLastSweepAt" }, ct);

        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(legacyStream, ct));

        Assert.Null(exception);
        Assert.Null((await db.Setups.AsNoTracking().FirstAsync(ct)).PlaylistBackfillLastSweepAt);
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
        var (db, backup, userId) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
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

        using var legacyStream = await LegacyBackupArchiveBuilder.RemoveColumnsAsync(backup, "PlaylistEntries", new[] { "SortOrder" }, ct);

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
        var (db, backup, userId) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
        await using var _ = db;

        db.ContinueWatchingEntries.Add(new ContinueWatchingEntry
        {
            UserId = userId,
            Position = TimeSpan.FromSeconds(300),
            UpdatedAt = DateTime.UtcNow,
            ListOrder = 1
        });
        await db.SaveChangesAsync(ct);

        using var legacyStream = await LegacyBackupArchiveBuilder.RemoveColumnsAsync(backup, "ContinueWatchingEntries", new[] { "PlaylistId" }, ct);

        // This must not throw even though the backup lacks the new column.
        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(legacyStream, ct));

        Assert.Null(exception);
        var restoredEntry = await db.ContinueWatchingEntries.SingleAsync(ct);
        Assert.Null(restoredEntry.PlaylistId);
        Assert.Equal(userId, (await db.Users.FirstAsync(ct)).Id);
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

    /// <summary>
    /// Verifies that a backup taken before <c>MediaSources.SourceType</c> existed (local directories as a
    /// media source type) can still be restored, with the missing column defaulting to
    /// <see cref="MediaSourceType.Sftp"/> — the only type that existed back then.
    /// </summary>
    [Fact]
    public async Task ReadFromAsync_LegacyBackupWithoutMediaSourceSourceType_RestoresWithSftpDefault()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-sourcetype?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, _) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
        await using var _ = db;

        db.MediaSources.Add(new MediaSource
        {
            Name = "SFTP Source",
            Path = "/media",
            Host = "host.local",
            Port = 22,
            SourceType = MediaSourceType.LocalDirectory,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);

        using var legacyStream = await LegacyBackupArchiveBuilder.RemoveColumnsAsync(
            backup, "MediaSources", new[] { "SourceType" }, ct);

        // Verify the simulated legacy backup really lacks the new optional column.
        var mediaSourceColumns = await LegacyBackupArchiveBuilder.ReadTableColumnsAsync(legacyStream, "MediaSources", ct);
        Assert.NotNull(mediaSourceColumns);
        Assert.DoesNotContain("SourceType", mediaSourceColumns!);
        Assert.Contains("Name", mediaSourceColumns!);

        // This must not throw even though the backup lacks the new column.
        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(legacyStream, ct));

        Assert.Null(exception);
        var restored = await db.MediaSources.AsNoTracking().SingleAsync(ms => ms.Name == "SFTP Source", ct);
        Assert.Equal(MediaSourceType.Sftp, restored.SourceType);
    }

}
