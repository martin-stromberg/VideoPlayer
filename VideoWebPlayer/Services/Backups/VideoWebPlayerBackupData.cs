using System.Data;
using System.Data.Common;
using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using msTools.Backup;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services.Backups;

/// <summary>
/// Exports and restores the VideoWebPlayer application database as an object-based backup item.
/// </summary>
public sealed class VideoWebPlayerBackupData : IBackupData
{
    private const int CurrentSchemaVersion = 1;
    private const string UsersTableName = "AspNetUsers";
    private static readonly HashSet<string> OptionalRestoreTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "UpdateSettings",
        nameof(ApplicationDbContext.UnlockedMediaEntries),
        nameof(ApplicationDbContext.WatchedEntries),
        nameof(ApplicationDbContext.Actors),
        nameof(ApplicationDbContext.MovieActors),
        nameof(ApplicationDbContext.TVShowEpisodeActors)
    };

    private static readonly HashSet<string> OptionalRestoreColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "Setups.ApplicationTitle",
        $"Setups.{nameof(Setup.ContinueWatchingEndThresholdSeconds)}",
        $"{nameof(ApplicationDbContext.TVShowEpisodes)}.{nameof(TVShowEpisode.GeneratedBackgroundPictureId)}",
        $"{nameof(ApplicationDbContext.TVShowEpisodes)}.{nameof(TVShowEpisode.BackgroundImageRequiresUpdate)}",
        $"{nameof(ApplicationDbContext.TVShowEpisodes)}.{nameof(TVShowEpisode.BackgroundImageGeneratedAt)}",
        $"{nameof(ApplicationDbContext.TVShowEpisodes)}.{nameof(TVShowEpisode.IsManuallyEdited)}",
        $"{nameof(ApplicationDbContext.TVShows)}.{nameof(TVShow.IsManuallyEdited)}",
        $"{nameof(ApplicationDbContext.TVShowSeasons)}.{nameof(TVShowSeason.IsManuallyEdited)}",
        $"{nameof(ApplicationDbContext.Movies)}.{nameof(Movie.IsManuallyEdited)}",
        $"{nameof(ApplicationDbContext.MovieCollections)}.{nameof(MovieCollection.IsManuallyEdited)}",
        $"{nameof(ApplicationDbContext.Pictures)}.{nameof(Picture.IsGeneratedBackground)}",
        $"{nameof(ApplicationDbContext.Pictures)}.{nameof(Picture.EpisodeId)}",
        $"{nameof(ApplicationDbContext.ContinueWatchingEntries)}.{nameof(ContinueWatchingEntry.ListOrder)}",
        $"{nameof(ApplicationDbContext.Movies)}.{nameof(Movie.ActorsClassifiedAt)}",
        $"{nameof(ApplicationDbContext.TVShowEpisodes)}.{nameof(TVShowEpisode.ActorsClassifiedAt)}",
        $"{nameof(ApplicationDbContext.Setups)}.{nameof(Setup.ActorCollectionThresholdPercent)}",
        $"{nameof(ApplicationDbContext.MovieActors)}.{nameof(MovieActor.Role)}",
        $"{nameof(ApplicationDbContext.MovieActors)}.{nameof(MovieActor.Order)}",
        $"{nameof(ApplicationDbContext.TVShowEpisodeActors)}.{nameof(TVShowEpisodeActor.Role)}",
        $"{nameof(ApplicationDbContext.TVShowEpisodeActors)}.{nameof(TVShowEpisodeActor.Order)}"
    };

    private static readonly (string Table, string Column, bool DefaultValue)[] OptionalRestoreBoolDefaults =
    {
        (nameof(ApplicationDbContext.TVShowEpisodes), nameof(TVShowEpisode.BackgroundImageRequiresUpdate), false),
        (nameof(ApplicationDbContext.TVShowEpisodes), nameof(TVShowEpisode.IsManuallyEdited), false),
        (nameof(ApplicationDbContext.TVShows), nameof(TVShow.IsManuallyEdited), false),
        (nameof(ApplicationDbContext.TVShowSeasons), nameof(TVShowSeason.IsManuallyEdited), false),
        (nameof(ApplicationDbContext.Movies), nameof(Movie.IsManuallyEdited), false),
        (nameof(ApplicationDbContext.MovieCollections), nameof(MovieCollection.IsManuallyEdited), false),
        (nameof(ApplicationDbContext.Pictures), nameof(Picture.IsGeneratedBackground), false)
    };

    private static readonly (string Table, string Column, long DefaultValue)[] OptionalRestoreLongDefaults =
    {
        (nameof(ApplicationDbContext.ContinueWatchingEntries), nameof(ContinueWatchingEntry.ListOrder), 0L)
    };

    private static readonly (string Table, string Column, int DefaultValue)[] OptionalRestoreIntDefaults =
    {
        (nameof(ApplicationDbContext.Setups), nameof(Setup.ContinueWatchingEndThresholdSeconds), 30),
        (nameof(ApplicationDbContext.Setups), nameof(Setup.ActorCollectionThresholdPercent), 50)
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<VideoWebPlayerBackupData> _logger;
    private readonly VideoWebPlayerBackupDataFactory? _factory;
    private readonly BackupGeneration? _generation;
    private readonly DateTimeOffset? _createdAtUtc;

    /// <summary>
    /// Gets the unique storage location of this backup object.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the unique type identifier of this backup object.
    /// </summary>
    public string ContentType { get; }

    /// <summary>
    /// Creates a new backup data object.
    /// </summary>
    public VideoWebPlayerBackupData(
        string name,
        string contentType,
        ApplicationDbContext db,
        IWebHostEnvironment environment,
        ILogger<VideoWebPlayerBackupData> logger,
        VideoWebPlayerBackupDataFactory? factory = null,
        BackupGeneration? generation = null,
        DateTimeOffset? createdAtUtc = null)
    {
        Name = name;
        ContentType = contentType;
        _db = db;
        _environment = environment;
        _logger = logger;
        _factory = factory;
        _generation = generation;
        _createdAtUtc = createdAtUtc;
    }

    /// <inheritdoc />
    public async Task WriteToAsync(Stream target, CancellationToken cancellationToken)
    {
        var index = new DatabaseBackupIndex
        {
            ProviderId = "VideoWebPlayer.ApplicationDbContext",
            SchemaVersion = CurrentSchemaVersion,
            CreatedAtUtc = _createdAtUtc ?? DateTimeOffset.UtcNow,
            Tables = new List<TableIndex>(),
            Files = new List<FilePayload>(),
            Generation = _generation
        };

        var tempPath = Path.Combine(Path.GetTempPath(), $"vwp-backup-{Guid.NewGuid():N}.tmp");
        using var buffer = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose);
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var table in GetTables())
            {
                var entryName = CreateEntityEntryName(table);
                index.Tables.Add(new TableIndex
                {
                    Name = table.Name,
                    Schema = table.Schema,
                    Columns = table.Columns.Select(x => x.Name).ToList(),
                    EntryName = entryName
                });

                var entry = zip.CreateEntry(entryName);
                await using var entryStream = entry.Open();
                await WriteTablePayloadAsync(entryStream, table, cancellationToken);
            }

            var indexEntry = zip.CreateEntry("index.json");
            await using (var indexStream = indexEntry.Open())
            {
                await JsonSerializer.SerializeAsync(indexStream, index, JsonOptions, cancellationToken);
            }
        }

        buffer.Position = 0;
        await buffer.CopyToAsync(target, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ReadFromAsync(Stream source, CancellationToken cancellationToken)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"vwp-restore-{Guid.NewGuid():N}.tmp");
        using var tempFile = source.CanSeek
            ? null
            : new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose);

        var archiveStream = tempFile ?? source;
        if (tempFile is not null)
        {
            await source.CopyToAsync(tempFile, cancellationToken);
            tempFile.Position = 0;
        }

        using var zip = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: tempFile is null);
        var indexEntry = zip.GetEntry("index.json")
            ?? throw new InvalidDataException("index.json fehlt.");

        DatabaseBackupIndex payload;
        await using (var indexStream = indexEntry.Open())
        {
            payload = await JsonSerializer.DeserializeAsync<DatabaseBackupIndex>(indexStream, JsonOptions, cancellationToken)
                ?? throw new InvalidDataException("index.json ist leer.");
        }

        Func<string, CancellationToken, Task<Stream>> openPayloadEntryAsync = (entryName, token) =>
        {
            var entry = zip.GetEntry(entryName)
                ?? throw new FileNotFoundException($"Backup-Eintrag {entryName} fehlt.");
            return Task.FromResult(entry.Open());
        };

        var validation = await ValidatePayloadAsync(payload, openPayloadEntryAsync, cancellationToken);
        if (!validation.IsValid)
            throw new InvalidDataException(string.Join(" ", validation.Errors));

        var tables = GetTables();
        var userId = _factory?.UserId;
        ReportRestoreProgress(null, 0, tables.Count, 0, 0, "Restore wird vorbereitet.");

        var tableMap = tables.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var connection = _db.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        Dictionary<string, object?>? currentAdminRow = null;
        if (!string.IsNullOrWhiteSpace(userId) && tableMap.TryGetValue(UsersTableName, out var usersTable))
            currentAdminRow = await ReadUserRowAsync(connection, usersTable, userId, cancellationToken);

        var userIdMap = await CreateRestoreUserIdMapAsync(payload, userId, currentAdminRow, openPayloadEntryAsync, cancellationToken);

        var sqlite = IsSqliteConnection(connection);
        if (sqlite)
            await ExecuteNonQueryAsync(connection, null, "PRAGMA foreign_keys = OFF;", cancellationToken);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var dbTransaction = transaction.GetDbTransaction();
        var committed = false;

        try
        {
            foreach (var table in tables.AsEnumerable().Reverse())
                await ExecuteNonQueryAsync(connection, dbTransaction, $"DELETE FROM {QuoteTable(table)};", cancellationToken);

            for (var tableIndex = 0; tableIndex < tables.Count; tableIndex++)
            {
                var table = tables[tableIndex];
                var dataSetNumber = tableIndex + 1;
                var tablePayload = payload.Tables.FirstOrDefault(x => string.Equals(x.Name, table.Name, StringComparison.OrdinalIgnoreCase));
                if (tablePayload is null)
                    continue;

                ReportRestoreProgress(table.Name, dataSetNumber, tables.Count, 0, 0, "Datenbestand wird gelesen.");
                var tableData = await ReadTablePayloadAsync(tablePayload, openPayloadEntryAsync, cancellationToken);
                var rows = tableData.Rows ?? new List<Dictionary<string, JsonElement?>>();
                ReportRestoreProgress(table.Name, dataSetNumber, tables.Count, 0, rows.Count, "Datenbestand wird wiederhergestellt.");

                for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                {
                    var row = rows[rowIndex];
                    ApplyRestoreUserIdMap(table, row, userIdMap);
                    await InsertRowAsync(connection, dbTransaction, table, tablePayload.Columns, row, cancellationToken);
                    ReportRestoreProgress(
                        table.Name,
                        dataSetNumber,
                        tables.Count,
                        rowIndex + 1,
                        rows.Count,
                        "Datensatz wurde wiederhergestellt.");
                }
            }

            if (!string.IsNullOrWhiteSpace(userId) && tableMap.TryGetValue(UsersTableName, out usersTable))
                await EnsureAdminAccountAsync(connection, dbTransaction, usersTable, tables, userId, currentAdminRow, cancellationToken);

            if (sqlite)
                await EnsureNoSqliteForeignKeyViolationsAsync(connection, dbTransaction, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            committed = true;
            ReportRestoreProgress(null, tables.Count, tables.Count, 0, 0, "Restore wurde abgeschlossen.");
        }
        catch
        {
            if (!committed)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            _logger.LogWarning("Restore transaction rolled back.");
            throw;
        }
        finally
        {
            if (sqlite)
                await ExecuteNonQueryAsync(connection, null, "PRAGMA foreign_keys = ON;", cancellationToken);
        }
    }

    private void ReportRestoreProgress(
        string? dataSetName,
        int dataSetNumber,
        int dataSetTotal,
        int recordNumber,
        int recordTotal,
        string message)
    {
        _factory?.Progress?.Report(new BackupRestoreProgress(
            dataSetName,
            dataSetNumber,
            dataSetTotal,
            recordNumber,
            recordTotal,
            message));
    }

    private async Task WriteTablePayloadAsync(Stream target, TableMetadata table, CancellationToken cancellationToken)
    {
        var connection = _db.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        var columnList = string.Join(", ", table.Columns.Select(x => BuildColumnSelectExpression(table, x)));
        command.CommandText = $"SELECT {columnList} FROM {QuoteTable(table)}{BuildTableFilter(table)}";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        await using var writer = new Utf8JsonWriter(target, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WritePropertyName("rows");
        writer.WriteStartArray();

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, JsonElement?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < table.Columns.Count; i++)
            {
                var value = await reader.IsDBNullAsync(i, cancellationToken)
                    ? null
                    : reader.GetValue(i);
                row[table.Columns[i].Name] = value is null
                    ? null
                    : JsonSerializer.SerializeToElement(value, value.GetType(), JsonOptions);
            }

            JsonSerializer.Serialize(writer, row, JsonOptions);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        await writer.FlushAsync(cancellationToken);
    }

    private static string BuildColumnSelectExpression(TableMetadata table, ColumnMetadata column)
    {
        if (string.Equals(table.Name, nameof(ApplicationDbContext.TVShowEpisodes), StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(column.Name, nameof(TVShowEpisode.GeneratedBackgroundPictureId), StringComparison.OrdinalIgnoreCase))
                return $"NULL AS {QuoteIdentifier(column.Name)}";
            if (string.Equals(column.Name, nameof(TVShowEpisode.BackgroundImageRequiresUpdate), StringComparison.OrdinalIgnoreCase))
                return $"1 AS {QuoteIdentifier(column.Name)}";
        }

        return QuoteIdentifier(column.Name);
    }

    private static string BuildTableFilter(TableMetadata table)
    {
        if (string.Equals(table.Name, nameof(ApplicationDbContext.Pictures), StringComparison.OrdinalIgnoreCase)
            && table.Columns.Any(x => string.Equals(x.Name, nameof(Picture.IsGeneratedBackground), StringComparison.OrdinalIgnoreCase)))
        {
            return $" WHERE {QuoteIdentifier(nameof(Picture.IsGeneratedBackground))} = 0";
        }

        return string.Empty;
    }

    private static async Task<TableDataPayload> ReadTablePayloadAsync(
        TableIndex table,
        Func<string, CancellationToken, Task<Stream>>? openPayloadEntryAsync,
        CancellationToken cancellationToken)
    {
        if (openPayloadEntryAsync is null)
            throw new InvalidDataException($"Entitätsdatei {table.EntryName} kann nicht geöffnet werden.");

        await using var stream = await openPayloadEntryAsync(table.EntryName, cancellationToken);
        return await JsonSerializer.DeserializeAsync<TableDataPayload>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException($"Entitätsdatei {table.EntryName} ist leer.");
    }

    private async Task<BackupValidationResult> ValidatePayloadAsync(
        DatabaseBackupIndex? payload,
        Func<string, CancellationToken, Task<Stream>>? openPayloadEntryAsync,
        CancellationToken cancellationToken)
    {
        if (payload is null)
            return BackupValidationResult.Invalid("index.json ist leer.");

        var errors = new List<string>();
        if (!string.Equals(payload.ProviderId, "VideoWebPlayer.ApplicationDbContext", StringComparison.Ordinal))
            errors.Add("index.json gehört nicht zum VideoWebPlayer-Provider.");
        if (payload.SchemaVersion != CurrentSchemaVersion)
            errors.Add($"Nicht unterstützte Daten-Schemaversion: {payload.SchemaVersion}.");
        if (payload.Tables is null)
            errors.Add("index.json enthält keine Tabellenliste.");
        if (payload.Files is null)
            errors.Add("index.json enthält keine Dateiliste.");

        var expectedTables = GetTables();
        var expectedByName = expectedTables.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var payloadByName = new Dictionary<string, TableIndex>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in payload.Tables ?? new List<TableIndex>())
        {
            if (string.IsNullOrWhiteSpace(table.Name))
            {
                errors.Add("index.json enthält eine Tabelle ohne Namen.");
                continue;
            }

            if (!payloadByName.TryAdd(table.Name, table))
                errors.Add($"Tabelle {table.Name} ist mehrfach enthalten.");
        }

        foreach (var expected in expectedTables)
        {
            if (!payloadByName.TryGetValue(expected.Name, out var table))
            {
                if (OptionalRestoreTables.Contains(expected.Name))
                    continue;

                errors.Add($"Tabelle {expected.Name} fehlt.");
                continue;
            }

            if (table.Columns is null)
                errors.Add($"Tabelle {table.Name} enthält keine Spaltenliste.");
            if (!IsSafeEntityEntryName(table.EntryName))
                errors.Add($"Entitätsdatei {table.EntryName} für Tabelle {table.Name} ist ungültig.");
            if (!string.Equals(table.EntryName, CreateEntityEntryName(expected), StringComparison.Ordinal))
                errors.Add($"Entitätsdatei {table.EntryName} passt nicht zu Tabelle {table.Name}.");

            var expectedColumns = expected.Columns.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var actualColumns = (table.Columns ?? new List<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var missing in expectedColumns.Except(actualColumns, StringComparer.OrdinalIgnoreCase))
            {
                if (IsOptionalRestoreColumn(expected.Name, missing))
                    continue;

                errors.Add($"Spalte {expected.Name}.{missing} fehlt.");
            }

            foreach (var unexpected in actualColumns.Except(expectedColumns, StringComparer.OrdinalIgnoreCase))
                errors.Add($"Spalte {expected.Name}.{unexpected} ist unbekannt.");

            if (errors.Count > 0 || openPayloadEntryAsync is null)
                continue;

            TableDataPayload tableData;
            try
            {
                tableData = await ReadTablePayloadAsync(table, openPayloadEntryAsync, cancellationToken);
            }
            catch (Exception ex) when (ex is JsonException or IOException or InvalidDataException or FileNotFoundException)
            {
                errors.Add($"Entitätsdatei {table.EntryName} ist nicht lesbar: {ex.Message}");
                continue;
            }

            var rows = tableData.Rows ?? new List<Dictionary<string, JsonElement?>>();
            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                if (rows[rowIndex] is null)
                {
                    errors.Add($"Zeile {rowIndex + 1} in Tabelle {expected.Name} ist ungültig.");
                    continue;
                }

                var unknownRowColumns = rows[rowIndex].Keys.Except(expectedColumns, StringComparer.OrdinalIgnoreCase).ToList();
                foreach (var unknown in unknownRowColumns)
                    errors.Add($"Zeile {rowIndex + 1} in Tabelle {expected.Name} enthält unbekannte Spalte {unknown}.");
            }
        }

        if ((payload.Tables ?? new List<TableIndex>()).Count > 0 && openPayloadEntryAsync is null)
            errors.Add("Entitätsdateien können nicht geöffnet werden.");

        foreach (var unexpected in payloadByName.Keys.Except(expectedByName.Keys, StringComparer.OrdinalIgnoreCase))
            errors.Add($"Tabelle {unexpected} ist unbekannt.");

        foreach (var file in payload.Files ?? new List<FilePayload>())
        {
            if (!IsSafeRelativePath(file.RelativePath))
                errors.Add($"Dateipfad {file.RelativePath} ist ungültig.");
            if (!IsSafeFileEntryName(file.EntryName))
                errors.Add($"ZIP-Eintrag {file.EntryName} für Datei {file.RelativePath} ist ungültig.");
            if (IsSafeRelativePath(file.RelativePath)
                && IsSafeFileEntryName(file.EntryName)
                && !string.Equals(file.EntryName, $"files/{file.RelativePath.Replace('\\', '/')}", StringComparison.Ordinal))
                errors.Add($"ZIP-Eintrag {file.EntryName} passt nicht zu Datei {file.RelativePath}.");
        }

        return errors.Count == 0 ? BackupValidationResult.Valid : new BackupValidationResult(false, errors);
    }

    private static async Task<Dictionary<string, string>> CreateRestoreUserIdMapAsync(
        DatabaseBackupIndex payload,
        string? userId,
        Dictionary<string, object?>? currentAdminRow,
        Func<string, CancellationToken, Task<Stream>>? openPayloadEntryAsync,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(userId) || currentAdminRow is null)
            return result;

        var usersPayload = payload.Tables.FirstOrDefault(x => string.Equals(x.Name, UsersTableName, StringComparison.OrdinalIgnoreCase));
        if (usersPayload is null)
            return result;

        var currentNormalizedUserName = GetString(currentAdminRow, "NormalizedUserName");
        var currentNormalizedEmail = GetString(currentAdminRow, "NormalizedEmail");
        if (string.IsNullOrWhiteSpace(currentNormalizedUserName) && string.IsNullOrWhiteSpace(currentNormalizedEmail))
            return result;

        var users = await ReadTablePayloadAsync(usersPayload, openPayloadEntryAsync, cancellationToken);
        foreach (var row in users.Rows ?? new List<Dictionary<string, JsonElement?>>())
        {
            var backupUserId = GetString(row, "Id");
            if (string.IsNullOrWhiteSpace(backupUserId) || string.Equals(backupUserId, userId, StringComparison.Ordinal))
                continue;

            var sameUserName = !string.IsNullOrWhiteSpace(currentNormalizedUserName)
                && string.Equals(GetString(row, "NormalizedUserName"), currentNormalizedUserName, StringComparison.Ordinal);
            var sameEmail = !string.IsNullOrWhiteSpace(currentNormalizedEmail)
                && string.Equals(GetString(row, "NormalizedEmail"), currentNormalizedEmail, StringComparison.Ordinal);

            if (sameUserName || sameEmail)
            {
                result[backupUserId] = userId;
                break;
            }
        }

        return result;
    }

    private static void ApplyRestoreUserIdMap(
        TableMetadata table,
        Dictionary<string, JsonElement?> row,
        IReadOnlyDictionary<string, string> userIdMap)
    {
        if (userIdMap.Count == 0)
            return;

        if (string.Equals(table.Name, UsersTableName, StringComparison.OrdinalIgnoreCase)
            && TryMapJsonString(row, "Id", userIdMap, out var mappedUserId))
        {
            row["Id"] = ToJsonElement(mappedUserId);
            if (row.ContainsKey("IsAdmin"))
                row["IsAdmin"] = ToJsonElement(true);
        }

        if (TryMapJsonString(row, "UserId", userIdMap, out mappedUserId))
            row["UserId"] = ToJsonElement(mappedUserId);
    }

    private static bool TryMapJsonString(
        Dictionary<string, JsonElement?> row,
        string column,
        IReadOnlyDictionary<string, string> map,
        out string mappedValue)
    {
        mappedValue = string.Empty;
        var value = GetString(row, column);
        return !string.IsNullOrWhiteSpace(value) && map.TryGetValue(value, out mappedValue);
    }

    private static string? GetString(Dictionary<string, object?> row, string column)
        => row.TryGetValue(column, out var value) && value is not null && value != DBNull.Value
            ? Convert.ToString(value, CultureInfo.InvariantCulture)
            : null;

    private static string? GetString(Dictionary<string, JsonElement?> row, string column)
    {
        if (!row.TryGetValue(column, out var value)
            || value is null
            || value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        return value.Value.ValueKind == JsonValueKind.String
            ? value.Value.GetString()
            : value.Value.GetRawText();
    }

    private static JsonElement ToJsonElement<T>(T value)
        => JsonSerializer.SerializeToElement(value, JsonOptions);

    private async Task EnsureAdminAccountAsync(
        DbConnection connection,
        DbTransaction transaction,
        TableMetadata usersTable,
        List<TableMetadata> allTables,
        string userId,
        Dictionary<string, object?>? currentAdminRow,
        CancellationToken cancellationToken)
    {
        var count = await CountUserAsync(connection, transaction, usersTable, userId, cancellationToken);
        if (count > 0)
        {
            await ExecuteUserUpdateAsync(connection, transaction, usersTable, userId, cancellationToken);
            return;
        }

        if (currentAdminRow is null)
            return;

        await DeleteConflictingUsersAsync(connection, transaction, usersTable, allTables, userId, currentAdminRow, cancellationToken);

        currentAdminRow["Sources"] = string.Empty;
        currentAdminRow["IsAdmin"] = true;
        await InsertObjectRowAsync(connection, transaction, usersTable, currentAdminRow, cancellationToken);
    }

    private static async Task DeleteConflictingUsersAsync(
        DbConnection connection,
        DbTransaction transaction,
        TableMetadata usersTable,
        List<TableMetadata> allTables,
        string userId,
        Dictionary<string, object?> currentAdminRow,
        CancellationToken cancellationToken)
    {
        var conflictColumns = new[] { "NormalizedUserName", "NormalizedEmail" }
            .Where(column => usersTable.Columns.Any(x => string.Equals(x.Name, column, StringComparison.OrdinalIgnoreCase))
                && currentAdminRow.TryGetValue(column, out var value)
                && value is not null
                && value != DBNull.Value
                && !string.IsNullOrWhiteSpace(Convert.ToString(value, CultureInfo.InvariantCulture)))
            .ToList();

        if (conflictColumns.Count == 0)
            return;

        var conflictingUserIds = await ReadConflictingUserIdsAsync(
            connection,
            transaction,
            usersTable,
            userId,
            currentAdminRow,
            conflictColumns,
            cancellationToken);

        if (conflictingUserIds.Count == 0)
            return;

        foreach (var table in allTables)
        {
            if (string.Equals(table.Name, usersTable.Name, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!table.Columns.Any(x => string.Equals(x.Name, "UserId", StringComparison.OrdinalIgnoreCase)))
                continue;

            await DeleteRowsForUsersAsync(connection, transaction, table, "UserId", conflictingUserIds, cancellationToken);
        }

        await DeleteRowsForUsersAsync(connection, transaction, usersTable, "Id", conflictingUserIds, cancellationToken);
    }

    private static async Task<List<string>> ReadConflictingUserIdsAsync(
        DbConnection connection,
        DbTransaction transaction,
        TableMetadata usersTable,
        string userId,
        Dictionary<string, object?> currentAdminRow,
        List<string> conflictColumns,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SELECT {QuoteIdentifier("Id")}
            FROM {QuoteTable(usersTable)}
            WHERE {QuoteIdentifier("Id")} <> @id
              AND ({string.Join(" OR ", conflictColumns.Select((column, index) => $"{QuoteIdentifier(column)} = @conflict{index}"))});
            """;
        AddParameter(command, "@id", userId);
        for (var i = 0; i < conflictColumns.Count; i++)
            AddParameter(command, $"@conflict{i}", currentAdminRow[conflictColumns[i]] ?? DBNull.Value);

        var result = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!await reader.IsDBNullAsync(0, cancellationToken))
                result.Add(reader.GetString(0));
        }

        return result;
    }

    private static async Task DeleteRowsForUsersAsync(
        DbConnection connection,
        DbTransaction transaction,
        TableMetadata table,
        string userIdColumn,
        List<string> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
            return;

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"DELETE FROM {QuoteTable(table)} WHERE {QuoteIdentifier(userIdColumn)} IN ({string.Join(", ", userIds.Select((_, index) => $"@user{index}"))});";
        for (var i = 0; i < userIds.Count; i++)
            AddParameter(command, $"@user{i}", userIds[i]);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> CountUserAsync(
        DbConnection connection,
        DbTransaction transaction,
        TableMetadata usersTable,
        string userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT COUNT(*) FROM {QuoteTable(usersTable)} WHERE {QuoteIdentifier("Id")} = @id;";
        AddParameter(command, "@id", userId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value);
    }

    private static async Task ExecuteUserUpdateAsync(
        DbConnection connection,
        DbTransaction transaction,
        TableMetadata usersTable,
        string userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"UPDATE {QuoteTable(usersTable)} SET {QuoteIdentifier("IsAdmin")} = @isAdmin WHERE {QuoteIdentifier("Id")} = @id;";
        AddParameter(command, "@isAdmin", true);
        AddParameter(command, "@id", userId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<Dictionary<string, object?>?> ReadUserRowAsync(
        DbConnection connection,
        TableMetadata table,
        string userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {string.Join(", ", table.Columns.Select(x => QuoteIdentifier(x.Name)))} FROM {QuoteTable(table)} WHERE {QuoteIdentifier("Id")} = @id;";
        AddParameter(command, "@id", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < table.Columns.Count; i++)
        {
            result[table.Columns[i].Name] = await reader.IsDBNullAsync(i, cancellationToken)
                ? null
                : reader.GetValue(i);
        }

        return result;
    }

    private static async Task InsertObjectRowAsync(
        DbConnection connection,
        DbTransaction transaction,
        TableMetadata table,
        Dictionary<string, object?> row,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var columns = table.Columns.Where(x => row.ContainsKey(x.Name)).ToList();
        command.CommandText = BuildInsertCommand(table, columns.Select(x => x.Name).ToList());
        for (var i = 0; i < columns.Count; i++)
            AddParameter(command, $"@p{i}", row[columns[i].Name] ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertRowAsync(
        DbConnection connection,
        DbTransaction transaction,
        TableMetadata table,
        List<string> columns,
        Dictionary<string, JsonElement?> row,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var insertColumns = AddOptionalRestoreDefaults(table, columns, row);
        command.CommandText = BuildInsertCommand(table, insertColumns);

        for (var i = 0; i < insertColumns.Count; i++)
        {
            var column = table.Columns.First(x => string.Equals(x.Name, insertColumns[i], StringComparison.OrdinalIgnoreCase));
            row.TryGetValue(insertColumns[i], out var value);
            AddParameter(command, $"@p{i}", ToDbValue(value, column.ClrType));
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static List<string> AddOptionalRestoreDefaults(
        TableMetadata table,
        List<string> columns,
        Dictionary<string, JsonElement?> row)
    {
        var insertColumns = columns.ToList();
        if (string.Equals(table.Name, "Setups", StringComparison.OrdinalIgnoreCase)
            && !insertColumns.Contains("ApplicationTitle", StringComparer.OrdinalIgnoreCase)
            && table.Columns.Any(x => string.Equals(x.Name, "ApplicationTitle", StringComparison.OrdinalIgnoreCase)))
        {
            insertColumns.Add("ApplicationTitle");
            row["ApplicationTitle"] = JsonSerializer.SerializeToElement("Martins Videosammlung", JsonOptions);
        }

        foreach (var (tableName, columnName, defaultValue) in OptionalRestoreBoolDefaults)
        {
            if (!string.Equals(table.Name, tableName, StringComparison.OrdinalIgnoreCase)
                || insertColumns.Contains(columnName, StringComparer.OrdinalIgnoreCase)
                || !table.Columns.Any(x => string.Equals(x.Name, columnName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            insertColumns.Add(columnName);
            row[columnName] = JsonSerializer.SerializeToElement(defaultValue, JsonOptions);
        }

        foreach (var (tableName, columnName, defaultValue) in OptionalRestoreLongDefaults)
        {
            if (!string.Equals(table.Name, tableName, StringComparison.OrdinalIgnoreCase)
                || insertColumns.Contains(columnName, StringComparer.OrdinalIgnoreCase)
                || !table.Columns.Any(x => string.Equals(x.Name, columnName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            insertColumns.Add(columnName);
            row[columnName] = JsonSerializer.SerializeToElement(defaultValue, JsonOptions);
        }

        foreach (var (tableName, columnName, defaultValue) in OptionalRestoreIntDefaults)
        {
            if (!string.Equals(table.Name, tableName, StringComparison.OrdinalIgnoreCase)
                || insertColumns.Contains(columnName, StringComparer.OrdinalIgnoreCase)
                || !table.Columns.Any(x => string.Equals(x.Name, columnName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            insertColumns.Add(columnName);
            row[columnName] = JsonSerializer.SerializeToElement(defaultValue, JsonOptions);
        }

        return insertColumns;
    }

    private static string BuildInsertCommand(TableMetadata table, List<string> columns)
    {
        var columnList = string.Join(", ", columns.Select(QuoteIdentifier));
        var parameterList = string.Join(", ", columns.Select((_, i) => $"@p{i}"));
        return $"INSERT INTO {QuoteTable(table)} ({columnList}) VALUES ({parameterList});";
    }

    private static object ToDbValue(JsonElement? element, Type clrType)
    {
        if (element is null || element.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return DBNull.Value;

        var type = Nullable.GetUnderlyingType(clrType) ?? clrType;
        var value = element.Value;

        if (type == typeof(string))
            return value.GetString() ?? string.Empty;
        if (type == typeof(int))
            return value.GetInt32();
        if (type == typeof(long))
            return value.GetInt64();
        if (type == typeof(short))
            return value.GetInt16();
        if (type == typeof(byte))
            return value.GetByte();
        if (type == typeof(bool))
            return value.ValueKind == JsonValueKind.Number ? value.GetInt32() != 0 : value.GetBoolean();
        if (type == typeof(DateTime))
            return value.ValueKind == JsonValueKind.String && DateTime.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTime)
                ? dateTime
                : value.GetDateTime();
        if (type == typeof(DateTimeOffset))
            return value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTimeOffset)
                ? dateTimeOffset
                : value.GetDateTimeOffset();
        if (type == typeof(Guid))
            return value.GetGuid();
        if (type == typeof(decimal))
            return value.GetDecimal();
        if (type == typeof(double))
            return value.GetDouble();
        if (type == typeof(float))
            return value.GetSingle();
        if (type == typeof(byte[]))
            return Convert.FromBase64String(value.GetString() ?? string.Empty);
        if (type.IsEnum)
            return value.ValueKind == JsonValueKind.String
                ? Enum.Parse(type, value.GetString() ?? string.Empty)
                : Enum.ToObject(type, value.GetInt32());

        return JsonSerializer.Deserialize(value.GetRawText(), type, JsonOptions) ?? DBNull.Value;
    }

    private static async Task ExecuteNonQueryAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        if (transaction is not null)
            command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureNoSqliteForeignKeyViolationsAsync(
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "PRAGMA foreign_key_check;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
            throw new InvalidDataException("Restore verletzt SQLite-Foreign-Key-Constraints.");
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static async Task EnsureOpenAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);
    }

    private static bool IsSqliteConnection(DbConnection connection)
        => connection.GetType().Name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase);

    private static bool IsSafeRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) || relativePath.Contains(':'))
            return false;

        var parts = relativePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 && parts.All(part => part != "." && part != "..");
    }

    private static bool IsOptionalRestoreColumn(string tableName, string columnName)
        => OptionalRestoreColumns.Contains($"{tableName}.{columnName}");

    private static bool IsSafeFileEntryName(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName) || entryName.Contains(':') || entryName.Contains('\\') || !entryName.StartsWith("files/", StringComparison.Ordinal))
            return false;

        var parts = entryName.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 && parts.All(part => part != "." && part != "..");
    }

    private static bool IsSafeEntityEntryName(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName) || entryName.Contains(':') || entryName.Contains('\\') || !entryName.StartsWith("entities/", StringComparison.Ordinal))
            return false;

        var parts = entryName.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 && parts.All(part => part != "." && part != "..") && entryName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateEntityEntryName(TableMetadata table)
        => $"entities/{CreateSafeEntrySegment(table.Schema is null ? table.Name : $"{table.Schema}-{table.Name}")}.json";

    private static string CreateSafeEntrySegment(string value)
    {
        var chars = value
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '-')
            .ToArray();

        var result = new string(chars).Trim('.', '-');
        return string.IsNullOrWhiteSpace(result) ? "entity" : result;
    }

    private List<TableMetadata> GetTables()
    {
        return _db.Model.GetEntityTypes()
            .Where(x => x.GetTableName() is not null)
            .GroupBy(x => new { Name = x.GetTableName()!, Schema = x.GetSchema() })
            .Select(group =>
            {
                var storeObject = StoreObjectIdentifier.Table(group.Key.Name, group.Key.Schema);
                var columns = group
                    .SelectMany(entity => entity.GetProperties())
                    .Select(property => new { Name = property.GetColumnName(storeObject), property.ClrType })
                    .Where(column => !string.IsNullOrWhiteSpace(column.Name))
                    .Select(column => new ColumnMetadata(column.Name!, column.ClrType))
                    .GroupBy(column => column.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(columnGroup => columnGroup.First())
                    .OrderBy(column => column.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new TableMetadata(group.Key.Name, group.Key.Schema, columns);
            })
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string QuoteTable(TableMetadata table)
        => table.Schema is null
            ? QuoteIdentifier(table.Name)
            : $"{QuoteIdentifier(table.Schema)}.{QuoteIdentifier(table.Name)}";

    private static string QuoteIdentifier(string identifier)
        => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private sealed class DatabaseBackupIndex
    {
        public string ProviderId { get; set; } = string.Empty;
        public int SchemaVersion { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
        public BackupGeneration? Generation { get; set; }
        public List<TableIndex> Tables { get; set; } = new();
        public List<FilePayload> Files { get; set; } = new();
    }

    private sealed class TableIndex
    {
        public string Name { get; set; } = string.Empty;
        public string? Schema { get; set; }
        public List<string> Columns { get; set; } = new();
        public string EntryName { get; set; } = string.Empty;
    }

    private sealed class TableDataPayload
    {
        public List<Dictionary<string, JsonElement?>> Rows { get; set; } = new();
    }

    private sealed class FilePayload
    {
        public string RelativePath { get; set; } = string.Empty;
        public string EntryName { get; set; } = string.Empty;
    }

    private sealed record TableMetadata(string Name, string? Schema, List<ColumnMetadata> Columns);

    private sealed record ColumnMetadata(string Name, Type ClrType);
}
