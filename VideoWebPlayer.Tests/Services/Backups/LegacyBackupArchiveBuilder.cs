using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Backups;
using VideoWebPlayer.Tests.Helpers;

namespace VideoWebPlayer.Tests.Services.Backups;

/// <summary>
/// Shared arrange helpers for the legacy-backup compatibility tests: seeds a current-schema database
/// together with its <see cref="VideoWebPlayerBackupData"/> instance, and rewrites a current backup
/// archive into one that looks like it was taken before a table or a column existed.
/// </summary>
internal static class LegacyBackupArchiveBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    /// <summary>
    /// Prepares a current-schema database (over the given, already-open SQLite in-memory shared-cache
    /// connection) with a valid admin user and setup, and the <see cref="VideoWebPlayerBackupData"/>
    /// instance to back it up into and restore it from. The connection stays owned by the calling test
    /// method, since the in-memory shared-cache database only exists while it is open.
    /// </summary>
    /// <param name="connection">The already-open SQLite in-memory connection to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The seeded database context (caller-owned, must be disposed), the backup instance, and the seeded admin user's id.</returns>
    /// <!-- Tupel-Elemente: Db, Backup, UserId -->
    public static async Task<(ApplicationDbContext Db, VideoWebPlayerBackupData Backup, string UserId)> CreateSeededBackupAsync(
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

        var environment = new TestWebHostEnvironment();
        var logger = NullLogger<VideoWebPlayerBackupData>.Instance;
        var factory = new VideoWebPlayerBackupDataFactory(EmptyServiceProvider.Instance, environment, logger)
        {
            UserId = userId
        };

        var backup = new VideoWebPlayerBackupData("test", "VideoWebPlayer:Database", db, environment, logger, factory);

        return (db, backup, userId);
    }

    /// <summary>
    /// Writes <paramref name="backup"/> in the current schema and rebuilds the archive without the given
    /// tables (removed from <c>index.json</c>'s table list, their data entries dropped), simulating a
    /// backup taken before those tables existed.
    /// </summary>
    /// <param name="backup">The current-schema backup to derive the legacy archive from.</param>
    /// <param name="tableNames">The table names to remove from the archive.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rebuilt legacy backup archive, positioned at the start.</returns>
    public static async Task<MemoryStream> RemoveTablesAsync(
        VideoWebPlayerBackupData backup, IReadOnlyCollection<string> tableNames, CancellationToken cancellationToken)
    {
        using var currentStream = new MemoryStream();
        await backup.WriteToAsync(currentStream, cancellationToken);
        currentStream.Position = 0;

        using var originalArchive = new ZipArchive(currentStream, ZipArchiveMode.Read, true);
        var indexNode = await ReadIndexAsync(originalArchive, cancellationToken);
        var tables = indexNode["tables"]!.AsArray();
        var removedEntryNames = new List<string>();

        foreach (var tableName in tableNames)
        {
            var table = tables.FirstOrDefault(t =>
                string.Equals(t!["name"]!.GetValue<string>(), tableName, StringComparison.OrdinalIgnoreCase));
            if (table is null)
                continue;

            removedEntryNames.Add(table["entryName"]!.GetValue<string>());
            tables.RemoveAt(tables.IndexOf(table));
        }

        var legacyStream = new MemoryStream();
        using (var legacyArchive = new ZipArchive(legacyStream, ZipArchiveMode.Create, true))
        {
            await WriteJsonEntryAsync(legacyArchive, "index.json", indexNode, cancellationToken);

            foreach (var entry in originalArchive.Entries)
            {
                if (string.Equals(entry.FullName, "index.json", StringComparison.Ordinal))
                    continue;
                if (removedEntryNames.Any(name => string.Equals(entry.FullName, name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                await CopyEntryAsync(legacyArchive, entry, cancellationToken);
            }
        }

        legacyStream.Position = 0;
        return legacyStream;
    }

    /// <summary>
    /// Writes <paramref name="backup"/> in the current schema and rebuilds the archive with the given
    /// table's given columns removed from both the index metadata's column list and every already-written
    /// row, simulating a backup taken before those columns existed.
    /// </summary>
    /// <param name="backup">The current-schema backup to derive the legacy archive from.</param>
    /// <param name="tableName">The name of the table to remove the columns from.</param>
    /// <param name="columnNames">The names of the columns to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rebuilt legacy backup archive, positioned at the start.</returns>
    public static async Task<MemoryStream> RemoveColumnsAsync(
        VideoWebPlayerBackupData backup,
        string tableName,
        IReadOnlyCollection<string> columnNames,
        CancellationToken cancellationToken)
    {
        using var currentStream = new MemoryStream();
        await backup.WriteToAsync(currentStream, cancellationToken);
        currentStream.Position = 0;

        using var originalArchive = new ZipArchive(currentStream, ZipArchiveMode.Read, true);
        var indexNode = await ReadIndexAsync(originalArchive, cancellationToken);
        var table = indexNode["tables"]!.AsArray().First(t =>
            string.Equals(t!["name"]!.GetValue<string>(), tableName, StringComparison.OrdinalIgnoreCase))!;
        var entryName = table["entryName"]!.GetValue<string>();

        var columns = table["columns"]!.AsArray();
        foreach (var columnName in columnNames)
        {
            var column = columns.FirstOrDefault(c =>
                string.Equals(c!.GetValue<string>(), columnName, StringComparison.OrdinalIgnoreCase));
            if (column is not null)
                columns.Remove(column);
        }

        var legacyStream = new MemoryStream();
        using (var legacyArchive = new ZipArchive(legacyStream, ZipArchiveMode.Create, true))
        {
            await WriteJsonEntryAsync(legacyArchive, "index.json", indexNode, cancellationToken);

            foreach (var entry in originalArchive.Entries)
            {
                if (string.Equals(entry.FullName, "index.json", StringComparison.Ordinal))
                    continue;

                if (!string.Equals(entry.FullName, entryName, StringComparison.OrdinalIgnoreCase))
                {
                    await CopyEntryAsync(legacyArchive, entry, cancellationToken);
                    continue;
                }

                JsonNode dataNode;
                await using (var dataStream = entry.Open())
                {
                    dataNode = (await JsonNode.ParseAsync(dataStream, cancellationToken: cancellationToken))!;
                }

                foreach (var row in dataNode["rows"]!.AsArray())
                {
                    foreach (var columnName in columnNames)
                        row!.AsObject().Remove(columnName);
                }

                await WriteJsonEntryAsync(legacyArchive, entry.FullName, dataNode, cancellationToken);
            }
        }

        legacyStream.Position = 0;
        return legacyStream;
    }

    /// <summary>
    /// Reads a table's column list out of a backup archive's <c>index.json</c> — exactly the list the
    /// restore validates the current schema against. Used to verify that a simulated legacy archive really
    /// lacks the table or the columns under test, as an exact comparison of column names rather than a
    /// substring search over the payload text (where a name like <c>Kind</c> would also match
    /// <c>SomeOtherKindColumn</c> or any value containing that text).
    /// </summary>
    /// <param name="archiveStream">The backup archive to read; its position is restored afterwards.</param>
    /// <param name="tableName">The name of the table whose column list is read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The table's column names, or <c>null</c> when the archive does not contain that table at all.</returns>
    public static async Task<IReadOnlyList<string>?> ReadTableColumnsAsync(
        MemoryStream archiveStream, string tableName, CancellationToken cancellationToken)
    {
        var position = archiveStream.Position;
        try
        {
            archiveStream.Position = 0;
            using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, true);
            var indexNode = await ReadIndexAsync(archive, cancellationToken);
            var table = indexNode["tables"]!.AsArray().FirstOrDefault(t =>
                string.Equals(t!["name"]!.GetValue<string>(), tableName, StringComparison.OrdinalIgnoreCase));
            if (table is null)
                return null;

            return table["columns"]!.AsArray().Select(c => c!.GetValue<string>()).ToList();
        }
        finally
        {
            archiveStream.Position = position;
        }
    }

    private static async Task<JsonNode> ReadIndexAsync(ZipArchive archive, CancellationToken cancellationToken)
    {
        var indexEntry = archive.GetEntry("index.json")!;
        await using var indexStream = indexEntry.Open();
        return (await JsonNode.ParseAsync(indexStream, cancellationToken: cancellationToken))!;
    }

    private static async Task WriteJsonEntryAsync(
        ZipArchive archive, string entryName, JsonNode node, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryName);
        await using var entryStream = entry.Open();
        await using var writer = new Utf8JsonWriter(entryStream, new JsonWriterOptions { Indented = true });
        node.WriteTo(writer, JsonOptions);
        await writer.FlushAsync(cancellationToken);
    }

    private static async Task CopyEntryAsync(ZipArchive target, ZipArchiveEntry source, CancellationToken cancellationToken)
    {
        var newEntry = target.CreateEntry(source.FullName);
        await using var sourceStream = source.Open();
        await using var targetStream = newEntry.Open();
        await sourceStream.CopyToAsync(targetStream, cancellationToken);
    }

    /// <summary>
    /// Empty, non-disposable <see cref="IServiceProvider"/> for <see cref="VideoWebPlayerBackupDataFactory"/>,
    /// which in these tests is only used for its <c>UserId</c> and progress reporting and never resolves a
    /// service. A real container built here would be an <see cref="IDisposable"/> that nothing could release.
    /// </summary>
    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static readonly EmptyServiceProvider Instance = new();

        public object? GetService(Type serviceType) => null;
    }
}
