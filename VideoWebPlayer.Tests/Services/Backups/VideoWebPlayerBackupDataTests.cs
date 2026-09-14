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
