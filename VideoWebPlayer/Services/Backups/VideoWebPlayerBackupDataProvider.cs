using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using msTools.Backup;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services.Backups;

/// <summary>
/// Exports and restores the VideoWebPlayer application database for backups.
/// </summary>
public sealed class VideoWebPlayerBackupDataProvider : IBackupDataProvider
{
    private const int CurrentSchemaVersion = 1;
    private const string UsersTableName = "AspNetUsers";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<VideoWebPlayerBackupDataProvider> _logger;

    /// <summary>
    /// Creates a new data provider.
    /// </summary>
    public VideoWebPlayerBackupDataProvider(
        ApplicationDbContext db,
        IWebHostEnvironment environment,
        ILogger<VideoWebPlayerBackupDataProvider> logger)
    {
        _db = db;
        _environment = environment;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ProviderId => "VideoWebPlayer.ApplicationDbContext";

    /// <inheritdoc />
    public async Task ExportAsync(Stream target, BackupExportContext context, CancellationToken cancellationToken)
    {
        var payload = new DatabaseBackupPayload
        {
            ProviderId = ProviderId,
            SchemaVersion = CurrentSchemaVersion,
            CreatedAtUtc = context.CreatedAtUtc,
            Tables = new List<TablePayload>(),
            Files = await ExportGenreIconFilesAsync(context, cancellationToken)
        };

        var connection = _db.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        foreach (var table in GetTables())
        {
            var tablePayload = new TablePayload
            {
                Name = table.Name,
                Schema = table.Schema,
                Columns = table.Columns.Select(x => x.Name).ToList()
            };

            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT {string.Join(", ", table.Columns.Select(x => QuoteIdentifier(x.Name)))} FROM {QuoteTable(table)}";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
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

                tablePayload.Rows.Add(row);
            }

            payload.Tables.Add(tablePayload);
        }

        await JsonSerializer.SerializeAsync(target, payload, JsonOptions, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<BackupValidationResult> ValidateAsync(Stream source, CancellationToken cancellationToken)
    {
        try
        {
            var payload = await JsonSerializer.DeserializeAsync<DatabaseBackupPayload>(source, JsonOptions, cancellationToken);
            return ValidatePayload(payload);
        }
        catch (JsonException ex)
        {
            return BackupValidationResult.Invalid($"data.json ist kein gültiges JSON: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task RestoreAsync(Stream source, BackupRestoreContext context, CancellationToken cancellationToken)
    {
        var payload = await JsonSerializer.DeserializeAsync<DatabaseBackupPayload>(source, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("data.json ist leer.");

        var validation = ValidatePayload(payload);
        if (!validation.IsValid)
            throw new InvalidDataException(string.Join(" ", validation.Errors));

        var tables = GetTables();
        var tableMap = tables.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var connection = _db.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        Dictionary<string, object?>? currentAdminRow = null;
        if (!string.IsNullOrWhiteSpace(context.UserId) && tableMap.TryGetValue(UsersTableName, out var usersTable))
            currentAdminRow = await ReadUserRowAsync(connection, usersTable, context.UserId, cancellationToken);

        await using var stagedFiles = await StagedGenreIconRestore.PrepareAsync(
            _environment.WebRootPath,
            payload.Files,
            context.OpenPayloadEntryAsync,
            cancellationToken);

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

            foreach (var table in tables)
            {
                var tablePayload = payload.Tables.FirstOrDefault(x => string.Equals(x.Name, table.Name, StringComparison.OrdinalIgnoreCase));
                if (tablePayload is null)
                    continue;

                foreach (var row in tablePayload.Rows)
                    await InsertRowAsync(connection, dbTransaction, table, tablePayload.Columns, row, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(context.UserId) && tableMap.TryGetValue(UsersTableName, out usersTable))
                await EnsureAdminAccountAsync(connection, dbTransaction, usersTable, context.UserId, currentAdminRow, cancellationToken);

            if (sqlite)
                await EnsureNoSqliteForeignKeyViolationsAsync(connection, dbTransaction, cancellationToken);

            await stagedFiles.ApplyAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            committed = true;
            stagedFiles.Accept();
        }
        catch
        {
            if (!committed)
            {
                await transaction.RollbackAsync(cancellationToken);
                await stagedFiles.RollbackAsync(cancellationToken);
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

    private async Task<List<FilePayload>> ExportGenreIconFilesAsync(BackupExportContext context, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(_environment.WebRootPath, "images", "genres");
        if (!Directory.Exists(directory))
            return new List<FilePayload>();

        var files = new List<FilePayload>();
        var seenEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(directory, path).Replace('\\', '/');
            if (relative.Split('/').Any(part => part is "." or ".."))
                continue;

            var entryName = $"files/{relative}";
            if (!seenEntries.Add(entryName))
                throw new InvalidOperationException($"Doppelter Backup-Dateieintrag: {entryName}");

            files.Add(new FilePayload
            {
                RelativePath = relative,
                EntryName = entryName
            });

            context.FileAttachments.Add(new BackupFileAttachment(
                entryName,
                async (target, token) =>
                {
                    await using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    await source.CopyToAsync(target, token);
                }));
        }

        await Task.CompletedTask.WaitAsync(cancellationToken);
        return files;
    }

    private BackupValidationResult ValidatePayload(DatabaseBackupPayload? payload)
    {
        if (payload is null)
            return BackupValidationResult.Invalid("data.json ist leer.");

        var errors = new List<string>();
        if (!string.Equals(payload.ProviderId, ProviderId, StringComparison.Ordinal))
            errors.Add("data.json gehört nicht zum VideoWebPlayer-Provider.");
        if (payload.SchemaVersion != CurrentSchemaVersion)
            errors.Add($"Nicht unterstützte Daten-Schemaversion: {payload.SchemaVersion}.");
        if (payload.Tables is null)
            errors.Add("data.json enthält keine Tabellenliste.");
        if (payload.Files is null)
            errors.Add("data.json enthält keine Dateiliste.");

        var expectedTables = GetTables();
        var expectedByName = expectedTables.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var payloadByName = new Dictionary<string, TablePayload>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in payload.Tables ?? new List<TablePayload>())
        {
            if (string.IsNullOrWhiteSpace(table.Name))
            {
                errors.Add("data.json enthält eine Tabelle ohne Namen.");
                continue;
            }

            if (!payloadByName.TryAdd(table.Name, table))
                errors.Add($"Tabelle {table.Name} ist mehrfach enthalten.");
        }

        foreach (var expected in expectedTables)
        {
            if (!payloadByName.TryGetValue(expected.Name, out var table))
            {
                errors.Add($"Tabelle {expected.Name} fehlt.");
                continue;
            }

            if (table.Columns is null)
                errors.Add($"Tabelle {table.Name} enthält keine Spaltenliste.");
            if (table.Rows is null)
                errors.Add($"Tabelle {table.Name} enthält keine Zeilenliste.");

            var expectedColumns = expected.Columns.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var actualColumns = (table.Columns ?? new List<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var missing in expectedColumns.Except(actualColumns, StringComparer.OrdinalIgnoreCase))
                errors.Add($"Spalte {expected.Name}.{missing} fehlt.");
            foreach (var unexpected in actualColumns.Except(expectedColumns, StringComparer.OrdinalIgnoreCase))
                errors.Add($"Spalte {expected.Name}.{unexpected} ist unbekannt.");

            var rows = table.Rows ?? new List<Dictionary<string, JsonElement?>>();
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

    private async Task EnsureAdminAccountAsync(
        DbConnection connection,
        DbTransaction transaction,
        TableMetadata usersTable,
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

        currentAdminRow["Sources"] = string.Empty;
        currentAdminRow["IsAdmin"] = true;
        await InsertObjectRowAsync(connection, transaction, usersTable, currentAdminRow, cancellationToken);
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
        command.CommandText = BuildInsertCommand(table, columns);

        for (var i = 0; i < columns.Count; i++)
        {
            var column = table.Columns.First(x => string.Equals(x.Name, columns[i], StringComparison.OrdinalIgnoreCase));
            row.TryGetValue(columns[i], out var value);
            AddParameter(command, $"@p{i}", ToDbValue(value, column.ClrType));
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
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

    private static bool IsSafeFileEntryName(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName) || entryName.Contains(':') || entryName.Contains('\\') || !entryName.StartsWith("files/", StringComparison.Ordinal))
            return false;

        var parts = entryName.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 && parts.All(part => part != "." && part != "..");
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

    private sealed class DatabaseBackupPayload
    {
        public string ProviderId { get; set; } = string.Empty;
        public int SchemaVersion { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
        public List<TablePayload> Tables { get; set; } = new();
        public List<FilePayload> Files { get; set; } = new();
    }

    private sealed class TablePayload
    {
        public string Name { get; set; } = string.Empty;
        public string? Schema { get; set; }
        public List<string> Columns { get; set; } = new();
        public List<Dictionary<string, JsonElement?>> Rows { get; set; } = new();
    }

    private sealed class FilePayload
    {
        public string RelativePath { get; set; } = string.Empty;
        public string EntryName { get; set; } = string.Empty;
    }

    private sealed class StagedGenreIconRestore : IAsyncDisposable
    {
        private readonly string _targetDirectory;
        private readonly string _stagingDirectory;
        private readonly string _backupDirectory;
        private bool _applied;
        private bool _accepted;
        private bool _targetMovedToBackup;

        private StagedGenreIconRestore(string targetDirectory, string stagingDirectory, string backupDirectory)
        {
            _targetDirectory = targetDirectory;
            _stagingDirectory = stagingDirectory;
            _backupDirectory = backupDirectory;
        }

        public static async Task<StagedGenreIconRestore> PrepareAsync(
            string webRootPath,
            List<FilePayload> files,
            Func<string, CancellationToken, Task<Stream>>? openPayloadEntryAsync,
            CancellationToken cancellationToken)
        {
            var targetDirectory = Path.GetFullPath(Path.Combine(webRootPath, "images", "genres"));
            var parentDirectory = Path.GetDirectoryName(targetDirectory) ?? webRootPath;
            Directory.CreateDirectory(parentDirectory);

            var stagingDirectory = Path.Combine(parentDirectory, $".genres-restore-{Guid.NewGuid():N}");
            var backupDirectory = Path.Combine(parentDirectory, $".genres-backup-{Guid.NewGuid():N}");
            Directory.CreateDirectory(stagingDirectory);

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsSafeRelativePath(file.RelativePath))
                    throw new InvalidDataException($"Dateipfad {file.RelativePath} ist ungültig.");

                if (!IsSafeFileEntryName(file.EntryName))
                    throw new InvalidDataException($"ZIP-Eintrag {file.EntryName} für Datei {file.RelativePath} ist ungültig.");
                if (!string.Equals(file.EntryName, $"files/{file.RelativePath.Replace('\\', '/')}", StringComparison.Ordinal))
                    throw new InvalidDataException($"ZIP-Eintrag {file.EntryName} passt nicht zu Datei {file.RelativePath}.");
                if (openPayloadEntryAsync is null)
                    throw new InvalidDataException($"Payload-Eintrag {file.EntryName} kann nicht geöffnet werden.");

                var parts = file.RelativePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                var targetPath = Path.GetFullPath(Path.Combine(new[] { stagingDirectory }.Concat(parts).ToArray()));
                if (!IsWithinDirectory(targetPath, stagingDirectory))
                    throw new InvalidDataException($"Dateipfad {file.RelativePath} ist ungültig.");

                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                await using var source = await openPayloadEntryAsync(file.EntryName, cancellationToken);
                await using var target = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await source.CopyToAsync(target, cancellationToken);
            }

            return new StagedGenreIconRestore(targetDirectory, stagingDirectory, backupDirectory);
        }

        public Task ApplyAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (Directory.Exists(_targetDirectory))
                {
                    Directory.Move(_targetDirectory, _backupDirectory);
                    _targetMovedToBackup = true;
                }

                Directory.Move(_stagingDirectory, _targetDirectory);
                _applied = true;
                return Task.CompletedTask;
            }
            catch
            {
                RollbackFileSystemState();
                throw;
            }
        }

        public Task RollbackAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RollbackFileSystemState();
            return Task.CompletedTask;
        }

        public void Accept()
        {
            _accepted = true;
            TryDeleteDirectory(_backupDirectory);
        }

        public ValueTask DisposeAsync()
        {
            if (!_accepted)
                RollbackFileSystemState();

            TryDeleteDirectory(_stagingDirectory);
            if (_accepted)
                TryDeleteDirectory(_backupDirectory);

            return ValueTask.CompletedTask;
        }

        private void RollbackFileSystemState()
        {
            if (_applied)
            {
                TryDeleteDirectory(_targetDirectory);
                _applied = false;
            }

            if (_targetMovedToBackup && Directory.Exists(_backupDirectory) && !Directory.Exists(_targetDirectory))
            {
                Directory.Move(_backupDirectory, _targetDirectory);
                _targetMovedToBackup = false;
            }

            if (!_targetMovedToBackup)
                TryDeleteDirectory(_backupDirectory);
        }

        private static bool IsWithinDirectory(string path, string directory)
        {
            var fullDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var fullPath = Path.GetFullPath(path);
            return fullPath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase);
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
            }
            catch
            {
            }
        }
    }

    private sealed record TableMetadata(string Name, string? Schema, List<ColumnMetadata> Columns);

    private sealed record ColumnMetadata(string Name, Type ClrType);
}
