using System.Collections.Concurrent;
using msTools.Backup;
using VideoWebPlayer.Utils;

namespace VideoWebPlayer.Services.Backups;

/// <summary>
/// Manages resumable backup upload sessions and their temporary files.
/// </summary>
public sealed class BackupUploadSessionService
{
    /// <summary>
    /// Time after which an untouched upload session is considered abandoned.
    /// </summary>
    internal static readonly TimeSpan SessionTimeout = TimeSpan.FromHours(24);

    /// <summary>
    /// Minimum time between two cleanup runs so that per-chunk requests do not rescan the temp directory.
    /// </summary>
    internal static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Maximum time the cleanup waits for a session's write lock before treating the session as in use.
    /// </summary>
    internal static readonly TimeSpan CleanupLockTimeout = TimeSpan.FromMilliseconds(250);

    private const string TempFilePrefix = "vwp-backup-upload-";

    private readonly ConcurrentDictionary<Guid, BackupUploadSession> _sessions = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BackupUploadSessionService> _logger;
    private readonly string _tempDirectory;
    private int _cleanupInProgress;
    private long _lastCleanupTicks;

    /// <summary>
    /// Creates a new upload session service.
    /// </summary>
    /// <param name="scopeFactory">Scope factory used to resolve scoped services per call.</param>
    /// <param name="timeProvider">Time source for session expiry and cleanup.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="tempDirectory">
    /// Directory that receives the temporary upload files and is scanned for orphans.
    /// Defaults to <see cref="Path.GetTempPath"/>; tests inject an isolated directory.
    /// </param>
    public BackupUploadSessionService(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<BackupUploadSessionService> logger,
        string? tempDirectory = null)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
        _tempDirectory = string.IsNullOrWhiteSpace(tempDirectory) ? Path.GetTempPath() : tempDirectory;
    }

    /// <summary>
    /// Validates the upload metadata and starts a new upload session with a temp file.
    /// </summary>
    public async Task<BeginSessionResult> BeginSessionAsync(string? fileName, long totalLength, CancellationToken cancellationToken = default)
    {
        CleanupExpiredSessions();

        if (string.IsNullOrWhiteSpace(fileName)
            || !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
        {
            return BeginSessionResult.Failure("Ungültiger Dateiname.");
        }

        if (totalLength <= 0)
            return BeginSessionResult.Failure("Ungültige Upload-Größe.");

        long maxUploadSizeBytes;
        using (var scope = _scopeFactory.CreateScope())
        {
            var optionsProvider = scope.ServiceProvider.GetRequiredService<IBackupOptionsProvider>();
            var options = await optionsProvider.GetOptionsAsync(cancellationToken);
            maxUploadSizeBytes = options.MaxUploadSizeBytes;
        }

        if (totalLength > maxUploadSizeBytes)
            return BeginSessionResult.TooLarge($"Die Datei überschreitet das Upload-Limit von {ByteSizeHelper.FormatBytes(maxUploadSizeBytes)}.");

        var id = Guid.NewGuid();
        var tempPath = Path.Combine(_tempDirectory, $"{TempFilePrefix}{id:N}.tmp");
        FileStream stream;
        try
        {
            Directory.CreateDirectory(_tempDirectory);
            stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.Asynchronous);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not create temp file {TempPath} for backup upload {UploadId}.", tempPath, id);
            return BeginSessionResult.Failure("Die temporäre Upload-Datei konnte nicht erstellt werden.");
        }

        var session = new BackupUploadSession(id, fileName, totalLength, tempPath, stream, _timeProvider);
        _sessions[id] = session;
        _logger.LogInformation(
            "Backup upload session {UploadId} started for {FileName} ({TotalLength} bytes).",
            id,
            fileName,
            totalLength);
        return BeginSessionResult.Success(session);
    }

    /// <summary>
    /// Returns the session for the given upload id or <c>null</c> when it is unknown or expired.
    /// </summary>
    public BackupUploadSession? GetSession(Guid uploadId)
    {
        CleanupExpiredSessions();
        if (!_sessions.TryGetValue(uploadId, out var session))
            return null;

        session.Touch();
        if (session.IsDisposed)
            return null;

        return session;
    }

    /// <summary>
    /// Marks the session as completed, closes its stream and removes it from the registry.
    /// The temp file is left on disk for the caller to consume.
    /// </summary>
    public async Task CompleteSessionAsync(BackupUploadSession session)
    {
        _sessions.TryRemove(session.Id, out _);
        await session.WriteLock.WaitAsync();
        try
        {
            session.IsCompleted = true;
            await session.DisposeAsync();
        }
        finally
        {
            session.WriteLock.Release();
        }
    }

    /// <summary>
    /// Removes the session from the registry, closes its stream and deletes the temp file.
    /// </summary>
    public async Task AbortSessionAsync(BackupUploadSession session)
    {
        _sessions.TryRemove(session.Id, out _);
        await session.WriteLock.WaitAsync();
        try
        {
            await session.DisposeAsync();
        }
        finally
        {
            session.WriteLock.Release();
        }

        DeleteTempFile(session.TempPath);
    }

    private void CleanupExpiredSessions()
    {
        var nowTicks = _timeProvider.GetUtcNow().UtcTicks;
        var lastTicks = Interlocked.Read(ref _lastCleanupTicks);
        if (nowTicks - lastTicks < CleanupInterval.Ticks)
            return;

        if (Interlocked.CompareExchange(ref _lastCleanupTicks, nowTicks, lastTicks) != lastTicks)
            return;

        if (Interlocked.Exchange(ref _cleanupInProgress, 1) == 1)
            return;

        try
        {
            var cutoff = _timeProvider.GetUtcNow() - SessionTimeout;

            foreach (var pair in _sessions)
            {
                var session = pair.Value;
                if (session.LastAccessUtc >= cutoff)
                    continue;

                if (!_sessions.TryRemove(pair.Key, out session))
                    continue;

                if (!session.WriteLock.Wait(CleanupLockTimeout))
                {
                    _sessions.TryAdd(pair.Key, session);
                    continue;
                }

                try
                {
                    if (session.LastAccessUtc >= cutoff)
                    {
                        _sessions.TryAdd(pair.Key, session);
                        continue;
                    }

                    session.Dispose();
                    DeleteTempFile(session.TempPath);
                }
                finally
                {
                    session.WriteLock.Release();
                }
            }

            CleanupOrphanedTempFiles(cutoff);
        }
        finally
        {
            Volatile.Write(ref _cleanupInProgress, 0);
        }
    }

    private void CleanupOrphanedTempFiles(DateTimeOffset cutoff)
    {
        string[] files;
        try
        {
            files = Directory.GetFiles(_tempDirectory, $"{TempFilePrefix}*.tmp");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not enumerate orphaned backup upload temp files.");
            return;
        }

        foreach (var file in files)
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) >= cutoff.UtcDateTime)
                    continue;

                if (_sessions.Values.Any(session => string.Equals(session.TempPath, file, StringComparison.OrdinalIgnoreCase)))
                    continue;

                File.Delete(file);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete orphaned backup upload temp file {TempFile}.", file);
            }
        }
    }

    private void DeleteTempFile(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete backup upload temp file {TempFile}.", tempPath);
        }
    }
}

/// <summary>
/// State of a single resumable backup upload.
/// </summary>
public sealed class BackupUploadSession : IAsyncDisposable, IDisposable
{
    private readonly TimeProvider _timeProvider;
    private long _lastAccessTicks;
    private long _receivedBytes;
    private volatile bool _isCompleted;
    private volatile bool _isDisposed;

    internal BackupUploadSession(
        Guid id,
        string fileName,
        long totalLength,
        string tempPath,
        FileStream stream,
        TimeProvider timeProvider)
    {
        Id = id;
        FileName = fileName;
        TotalLength = totalLength;
        TempPath = tempPath;
        Stream = stream;
        _timeProvider = timeProvider;
        _lastAccessTicks = timeProvider.GetUtcNow().UtcTicks;
    }

    /// <summary>
    /// Gets the server-assigned upload id.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the client-provided file name.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// Gets the declared total upload size in bytes.
    /// </summary>
    public long TotalLength { get; }

    /// <summary>
    /// Gets the number of bytes received so far.
    /// </summary>
    public long ReceivedBytes
    {
        get => Interlocked.Read(ref _receivedBytes);
        private set => Interlocked.Exchange(ref _receivedBytes, value);
    }

    /// <summary>
    /// Gets the path of the temporary file receiving the chunks.
    /// </summary>
    public string TempPath { get; }

    /// <summary>
    /// Gets the stream writing into the temporary file.
    /// </summary>
    public FileStream Stream { get; }

    /// <summary>
    /// Gets the timestamp of the last session access.
    /// </summary>
    public DateTimeOffset LastAccessUtc => new(Interlocked.Read(ref _lastAccessTicks), TimeSpan.Zero);

    /// <summary>
    /// Gets a value indicating whether all bytes were received and the session was closed.
    /// </summary>
    public bool IsCompleted
    {
        get => _isCompleted;
        internal set => _isCompleted = value;
    }

    /// <summary>
    /// Gets a value indicating whether the session stream has been disposed.
    /// </summary>
    internal bool IsDisposed => _isDisposed;

    internal SemaphoreSlim WriteLock { get; } = new(1, 1);

    internal void Touch() => Interlocked.Exchange(ref _lastAccessTicks, _timeProvider.GetUtcNow().UtcTicks);

    /// <summary>
    /// Appends a request body chunk at the given offset to the session temp file.
    /// </summary>
    public async Task<AppendChunkResult> AppendChunkAsync(long offset, Stream source, long contentLength, CancellationToken cancellationToken = default)
    {
        await WriteLock.WaitAsync(cancellationToken);
        try
        {
            Touch();

            if (IsCompleted)
                return AppendChunkResult.Rejected(ReceivedBytes, "Der Upload wurde bereits abgeschlossen.");

            if (IsDisposed)
                return AppendChunkResult.Rejected(ReceivedBytes, "Die Upload-Session wurde beendet. Bitte starten Sie den Upload erneut.");

            if (offset != ReceivedBytes)
                return AppendChunkResult.ResumeRequired(ReceivedBytes);

            if (offset + contentLength > TotalLength)
                return AppendChunkResult.Rejected(ReceivedBytes, "Der Chunk überschreitet die deklarierte Gesamtgröße.");

            Stream.Seek(offset, SeekOrigin.Begin);

            var buffer = new byte[81920];
            long written = 0;
            try
            {
                while (written < contentLength)
                {
                    var requested = (int)Math.Min(buffer.Length, contentLength - written);
                    var read = await source.ReadAsync(buffer.AsMemory(0, requested), cancellationToken);
                    if (read == 0)
                        break;

                    await Stream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    written += read;
                }
            }
            catch
            {
                Stream.SetLength(ReceivedBytes);
                await Stream.FlushAsync(CancellationToken.None);
                throw;
            }

            ReceivedBytes = offset + written;
            if (written < contentLength)
            {
                Stream.SetLength(ReceivedBytes);
                await Stream.FlushAsync(CancellationToken.None);
                return AppendChunkResult.ResumeRequired(ReceivedBytes);
            }

            await Stream.FlushAsync(CancellationToken.None);
            return AppendChunkResult.Written(ReceivedBytes);
        }
        finally
        {
            WriteLock.Release();
        }
    }

    // The WriteLock is deliberately not disposed: requests already waiting in
    // WriteLock.WaitAsync must exit through the IsCompleted/IsDisposed checks in
    // AppendChunkAsync instead of running into an ObjectDisposedException.

    /// <inheritdoc />
    public void Dispose()
    {
        _isDisposed = true;
        Stream.Dispose();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _isDisposed = true;
        await Stream.DisposeAsync();
    }
}

/// <summary>
/// Outcome of an attempted chunk append.
/// </summary>
public enum BackupUploadAppendStatus
{
    /// <summary>
    /// The chunk was fully written at the expected offset.
    /// </summary>
    Written,

    /// <summary>
    /// The chunk could not be written completely; the upload should resume at the returned offset.
    /// </summary>
    ResumeRequired,

    /// <summary>
    /// The chunk was rejected because of a validation error.
    /// </summary>
    Rejected
}

/// <summary>
/// Result of a chunk append operation.
/// </summary>
/// <param name="Status">The append outcome.</param>
/// <param name="Offset">The offset the client should continue from.</param>
/// <param name="Error">The validation error when the chunk was rejected.</param>
public sealed record AppendChunkResult(BackupUploadAppendStatus Status, long Offset, string? Error)
{
    /// <summary>
    /// Creates a result for a fully written chunk.
    /// </summary>
    public static AppendChunkResult Written(long offset)
        => new(BackupUploadAppendStatus.Written, offset, null);

    /// <summary>
    /// Creates a result telling the client to resume at the given offset.
    /// </summary>
    public static AppendChunkResult ResumeRequired(long offset)
        => new(BackupUploadAppendStatus.ResumeRequired, offset, null);

    /// <summary>
    /// Creates a result for a rejected chunk.
    /// </summary>
    public static AppendChunkResult Rejected(long offset, string error)
        => new(BackupUploadAppendStatus.Rejected, offset, error);
}

/// <summary>
/// Result of a session creation attempt.
/// </summary>
/// <param name="Session">The created session when the attempt succeeded.</param>
/// <param name="Error">The validation error when the attempt failed.</param>
/// <param name="ExceedsUploadLimit">True when the upload exceeds the configured upload limit.</param>
public sealed record BeginSessionResult(BackupUploadSession? Session, string? Error, bool ExceedsUploadLimit)
{
    /// <summary>
    /// Gets a value indicating whether a session was created.
    /// </summary>
    public bool Succeeded => Session is not null;

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static BeginSessionResult Success(BackupUploadSession session)
        => new(session, null, false);

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    public static BeginSessionResult Failure(string error)
        => new(null, error, false);

    /// <summary>
    /// Creates a failure result for uploads above the configured limit.
    /// </summary>
    public static BeginSessionResult TooLarge(string error)
        => new(null, error, true);
}

/// <summary>
/// Response of the upload status endpoint.
/// </summary>
/// <param name="UploadId">The server-assigned upload id.</param>
/// <param name="FileName">The client-provided file name.</param>
/// <param name="UploadLength">The declared total upload size in bytes.</param>
/// <param name="UploadOffset">The offset the client should continue from.</param>
public sealed record BackupUploadStatusResponse(Guid UploadId, string FileName, long UploadLength, long UploadOffset);
