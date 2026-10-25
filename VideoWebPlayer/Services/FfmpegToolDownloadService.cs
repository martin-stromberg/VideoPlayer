using System.IO.Compression;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SharpCompress.Readers;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services;

/// <summary>
/// Downloads the platform-matching static ffmpeg package (containing <c>ffmpeg</c> and
/// <c>ffprobe</c>), extracts both binaries into the convention directory
/// <c>tools/ffmpeg/</c> next to the application and marks them executable on Unix.
/// The job runs outside the circuit/request lifetime; progress is exposed through a
/// lock-protected immutable snapshot like <see cref="Backups.ManualBackupJobService"/>.
/// The download only ever writes to <c>{baseDirectory}/tools/ffmpeg/</c> — configured
/// paths (<c>MediaProbe:FfprobePath</c>/<c>MediaProbe:FfmpegPath</c>) and <c>PATH</c> are
/// never touched.
/// </summary>
public sealed class FfmpegToolDownloadService : IFfmpegToolDownloadService
{
    internal const string HttpClientName = "FfmpegTools";

    private static readonly string[] AllowedBinaryNames = { "ffmpeg", "ffprobe", "ffmpeg.exe", "ffprobe.exe" };
    private static readonly TimeSpan ProgressUpdateInterval = TimeSpan.FromMilliseconds(250);

    // Stall watchdog for the body copy: with ResponseHeadersRead the client timeout ends at
    // the headers, so the job aborts only when the server sends nothing at all for this
    // window. Chosen over a total download budget, which would also kill slow-but-working
    // connections on the large (~145 MB) packages.
    private static readonly TimeSpan DefaultDownloadStallTimeout = TimeSpan.FromMinutes(2);

    // Zip-bomb guard: allowlisted entries are written without a server-side size contract,
    // so the staged copy is capped well above the real binary size (~80–140 MB).
    private const long DefaultMaxExtractedEntryBytes = 500L * 1024 * 1024;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MediaProbeOptions _options;
    private readonly ILogger<FfmpegToolDownloadService> _logger;
    private readonly string _baseDirectory;
    private readonly Func<(string Rid, string ArchiveKind)?> _platformSelector;
    private readonly TimeSpan _downloadStallTimeout;
    private readonly long _maxExtractedEntryBytes;
    private readonly object _syncRoot = new();
    private FfmpegToolDownloadJobSnapshot _current = FfmpegToolDownloadJobSnapshot.Idle;

    /// <summary>
    /// Creates a new ffmpeg tool download service.
    /// </summary>
    public FfmpegToolDownloadService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        IOptions<MediaProbeOptions> options,
        ILogger<FfmpegToolDownloadService> logger)
        : this(scopeFactory, httpClientFactory, options, logger, AppContext.BaseDirectory, null)
    {
    }

    internal FfmpegToolDownloadService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        IOptions<MediaProbeOptions> options,
        ILogger<FfmpegToolDownloadService> logger,
        string baseDirectory,
        Func<(string Rid, string ArchiveKind)?>? platformSelector,
        TimeSpan? downloadStallTimeout = null,
        long? maxExtractedEntryBytes = null)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
        _baseDirectory = baseDirectory;
        _platformSelector = platformSelector ?? DetectPlatform;
        _downloadStallTimeout = downloadStallTimeout ?? DefaultDownloadStallTimeout;
        _maxExtractedEntryBytes = maxExtractedEntryBytes ?? DefaultMaxExtractedEntryBytes;
    }

    /// <inheritdoc />
    public FfmpegToolDownloadJobSnapshot GetSnapshot()
    {
        lock (_syncRoot)
        {
            return _current;
        }
    }

    /// <inheritdoc />
    public void AcknowledgeTerminalState(Guid snapshotId)
    {
        lock (_syncRoot)
        {
            // Id check: a late acknowledge (e.g. a Dismiss click against a long-shown
            // result) must not wipe a newer terminal snapshot nobody has seen yet.
            if (!_current.IsActive && _current.Id == snapshotId)
                _current = FfmpegToolDownloadJobSnapshot.Idle;
        }
    }

    /// <inheritdoc />
    public async Task<FfmpegToolDownloadStartResult> StartDownloadAsync(string userId, CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(userId, cancellationToken).ConfigureAwait(false);

        FfmpegToolDownloadJobSnapshot snapshot;
        lock (_syncRoot)
        {
            if (_current.IsActive)
                return new FfmpegToolDownloadStartResult(false, _current);

            snapshot = new FfmpegToolDownloadJobSnapshot(
                Guid.NewGuid(),
                FfmpegToolDownloadJobStatus.Queued,
                DateTimeOffset.UtcNow,
                null,
                "Download wurde gestartet.",
                null,
                0,
                null);

            _current = snapshot;
        }

        _ = Task.Run(() => RunJobAsync(snapshot.Id));
        return new FfmpegToolDownloadStartResult(true, snapshot);
    }

    private async Task RequireAdminAsync(string userId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Sie sind nicht angemeldet.");

        if (!user.IsAdmin)
            throw new ForbiddenAccessException("Nur Administratoren dürfen ffmpeg/ffprobe herunterladen.");
    }

    private static (string Rid, string ArchiveKind)? DetectPlatform()
    {
        if (RuntimeInformation.OSArchitecture != Architecture.X64)
            return null;
        if (OperatingSystem.IsWindows())
            return ("win-x64", "zip");
        if (OperatingSystem.IsLinux())
            return ("linux-x64", "tar.xz");
        return null;
    }

    private static (string FfmpegName, string FfprobeName) ExpectedBinaryNames(string rid)
        => rid.StartsWith("win", StringComparison.OrdinalIgnoreCase)
            ? ("ffmpeg.exe", "ffprobe.exe")
            : ("ffmpeg", "ffprobe");

    private async Task RunJobAsync(Guid jobId)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"ffmpeg-tools-{jobId:N}.download");
        var stagingDir = Path.Combine(_baseDirectory, "tools", "ffmpeg");
        var stagedFiles = new List<string>();

        try
        {
            var platform = _platformSelector();
            if (platform is null)
            {
                SetSnapshot(jobId, FfmpegToolDownloadJobStatus.Failed, "Download fehlgeschlagen.",
                    "Dieses System wird nicht unterstützt. Bitte ffmpeg und ffprobe manuell installieren — ein eigener Speicherort kann in der Server-Konfiguration (appsettings.json) hinterlegt werden.",
                    DateTimeOffset.UtcNow);
                return;
            }

            var (rid, archiveKind) = platform.Value;
            var (ffmpegName, ffprobeName) = ExpectedBinaryNames(rid);

            if (!_options.DownloadSources.TryGetValue(rid, out var url)
                || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                _logger.LogWarning("No usable ffmpeg download source configured for RID {Rid}: {Url}", rid, url);
                SetSnapshot(jobId, FfmpegToolDownloadJobStatus.Failed, "Download fehlgeschlagen.",
                    "Es ist keine gültige Download-Quelle für dieses System hinterlegt. Details stehen im Server-Protokoll.",
                    DateTimeOffset.UtcNow);
                return;
            }

            try
            {
                Directory.CreateDirectory(stagingDir);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Cannot create the ffmpeg staging directory {StagingDir}.", stagingDir);
                SetSnapshot(jobId, FfmpegToolDownloadJobStatus.Failed, "Download fehlgeschlagen.",
                    TargetNotWritableMessage,
                    DateTimeOffset.UtcNow);
                return;
            }

            SetSnapshot(jobId, FfmpegToolDownloadJobStatus.Running, "ffmpeg/ffprobe werden heruntergeladen.", null, null);
            await DownloadAsync(jobId, uri, tempFile).ConfigureAwait(false);

            SetSnapshot(jobId, FfmpegToolDownloadJobStatus.Running, "Paket wird entpackt.", null, null);
            var extracted = archiveKind == "zip"
                ? ExtractZip(tempFile, stagingDir, stagedFiles)
                : ExtractTarXz(tempFile, stagingDir, stagedFiles);

            var missing = new[] { ffmpegName, ffprobeName }
                .Where(name => !extracted.Contains(name))
                .ToList();
            if (missing.Count > 0)
            {
                // Same ordering as the catch path: cleanup before the terminal snapshot,
                // so observers never see Failed while staged *.tmp files are still on disk.
                TryDelete(tempFile);
                foreach (var staged in stagedFiles)
                    TryDelete(staged);
                stagedFiles.Clear();
                SetSnapshot(jobId, FfmpegToolDownloadJobStatus.Failed, "Download fehlgeschlagen.",
                    $"Das Paket enthält kein {string.Join("/", missing)}-Programm und ist daher nicht verwendbar.",
                    DateTimeOffset.UtcNow);
                return;
            }

            Install(stagingDir, ffmpegName, ffprobeName);
            SetSnapshot(jobId, FfmpegToolDownloadJobStatus.Succeeded,
                "ffmpeg und ffprobe wurden installiert.", null, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ffmpeg tool download job {JobId} failed.", jobId);
            // Cleanup BEFORE the terminal snapshot: observers waiting on the job must never
            // see Failed while staged *.tmp files are still on disk (flake in CI under load).
            TryDelete(tempFile);
            foreach (var staged in stagedFiles)
                TryDelete(staged);
            stagedFiles.Clear();
            SetSnapshot(jobId, FfmpegToolDownloadJobStatus.Failed, "Download fehlgeschlagen.",
                UserFacingError(ex), DateTimeOffset.UtcNow);
        }
        finally
        {
            TryDelete(tempFile);
            foreach (var staged in stagedFiles)
                TryDelete(staged);
        }
    }

    private const string TargetNotWritableMessage =
        "Der Server kann den Zielordner \"tools/ffmpeg/\" neben der Anwendung nicht beschreiben. " +
        "Bitte die Schreibrechte dieses Ordners prüfen — alternativ kann ein eigener Pfad für " +
        "ffmpeg/ffprobe in der Server-Konfiguration (appsettings.json) hinterlegt werden.";

    // The UI shows snapshot.Error verbatim to non-technical admins: only the deliberately
    // phrased FriendlyJobException texts are displayed as-is. Everything else maps to a
    // comprehensible fallback — the technical detail stays in the server log (LogError above).
    private static string UserFacingError(Exception ex) => ex switch
    {
        FriendlyJobException friendly => friendly.Message,
        OperationCanceledException =>
            "Der Download wurde abgebrochen, weil der Server zu lange keine Daten gesendet hat. Bitte den Download erneut starten.",
        HttpRequestException =>
            "Das Paket konnte nicht heruntergeladen werden. Bitte die Internetverbindung prüfen und den Download erneut starten.",
        IOException or UnauthorizedAccessException => TargetNotWritableMessage,
        _ => "Der Download ist fehlgeschlagen. Details stehen im Server-Protokoll."
    };

    private async Task DownloadAsync(Guid jobId, Uri uri, string tempFile)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        // Stall watchdog: the client timeout ends at the response headers
        // (ResponseHeadersRead), so the body copy gets its own cancellation — re-armed
        // after every received chunk it only fires when the server stops sending entirely.
        using var stallTimeout = new CancellationTokenSource(_downloadStallTimeout);
        using var response = await client
            .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, stallTimeout.Token)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            // The status code stays a diagnostics detail in the server log — the UI shows
            // only the comprehensible message (no technical token for non-technical admins).
            _logger.LogWarning("ffmpeg package request to {Uri} returned HTTP {StatusCode}.",
                uri, (int)response.StatusCode);
            throw new FriendlyJobException(
                "Das Paket konnte nicht heruntergeladen werden. " +
                "Bitte die Internetverbindung prüfen und den Download erneut starten.");
        }

        var total = response.Content.Headers.ContentLength;
        var lastReport = DateTimeOffset.MinValue;

        await using var source = await response.Content.ReadAsStreamAsync(stallTimeout.Token).ConfigureAwait(false);
        await using var target = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None);
        var buffer = new byte[81920];
        long received = 0;
        while (true)
        {
            stallTimeout.CancelAfter(_downloadStallTimeout);
            var read = await source.ReadAsync(buffer, stallTimeout.Token).ConfigureAwait(false);
            if (read <= 0)
                break;

            await target.WriteAsync(buffer.AsMemory(0, read), stallTimeout.Token).ConfigureAwait(false);
            received += read;
            var now = DateTimeOffset.UtcNow;
            if (now - lastReport >= ProgressUpdateInterval)
            {
                lastReport = now;
                UpdateProgress(jobId, received, total);
            }
        }
        UpdateProgress(jobId, received, total);
    }

    // Extraction is deliberately allowlist-driven: only the two tool file names are ever
    // extracted, any directory prefix is discarded (flat layout under the plain file name)
    // and entries with ".." segments or rooted/absolute paths are always discarded — even
    // when their file name is allowlisted.
    private HashSet<string> ExtractZip(string archivePath, string stagingDir, List<string> stagedFiles)
        => ExtractEntries(EnumerateZipEntries(archivePath), stagingDir, stagedFiles);

    private HashSet<string> ExtractTarXz(string archivePath, string stagingDir, List<string> stagedFiles)
        => ExtractEntries(EnumerateTarXzEntries(archivePath), stagingDir, stagedFiles);

    // Both enumerators dispose their archive/reader with the enumeration; stream factories
    // must be consumed while the archive is positioned on that entry (the tar reader is
    // forward-only).
    private static IEnumerable<(string Name, Func<Stream> OpenStream)> EnumerateZipEntries(string archivePath)
    {
        using var stream = File.OpenRead(archivePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
            yield return (entry.FullName, entry.Open);
    }

    private static IEnumerable<(string Name, Func<Stream> OpenStream)> EnumerateTarXzEntries(string archivePath)
    {
        using var stream = File.OpenRead(archivePath);
        using var reader = ReaderFactory.OpenReader(stream, new ReaderOptions());
        while (reader.MoveToNextEntry())
        {
            if (reader.Entry.IsDirectory)
                continue;
            yield return (reader.Entry.Key ?? string.Empty, reader.OpenEntryStream);
        }
    }

    private HashSet<string> ExtractEntries(
        IEnumerable<(string Name, Func<Stream> OpenStream)> entries,
        string stagingDir,
        List<string> stagedFiles)
    {
        try
        {
            var extracted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (entryName, openStream) in entries)
            {
                var fileName = EntryFileName(entryName);
                var stagingPath = Path.Combine(stagingDir, fileName + ".tmp");
                if (!IsExtractableEntry(entryName, fileName, stagingDir, stagingPath))
                    continue;

                using var input = openStream();
                StageEntry(input, stagingPath, stagedFiles);
                extracted.Add(fileName);
            }
            return extracted;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new FriendlyJobException(
                "Das heruntergeladene Paket konnte nicht entpackt werden.", ex);
        }
    }

    private static string EntryFileName(string entryName)
        => Path.GetFileName(entryName.Replace('\\', '/'));

    private static bool IsExtractableEntry(string entryName, string fileName, string stagingDir, string stagingPath)
    {
        if (!AllowedBinaryNames.Contains(fileName, StringComparer.OrdinalIgnoreCase))
            return false;

        var normalized = entryName.Replace('\\', '/');
        if (normalized.StartsWith('/')
            || normalized.Split('/').Any(segment => segment == "..")
            || normalized.Length >= 2 && normalized[1] == ':'
            || Path.IsPathRooted(entryName))
            return false;

        // Defense in depth: the normalized target must stay inside the staging directory.
        var fullTarget = Path.GetFullPath(stagingPath);
        var fullDir = Path.GetFullPath(stagingDir) + Path.DirectorySeparatorChar;
        return fullTarget.StartsWith(fullDir, StringComparison.OrdinalIgnoreCase);
    }

    private void StageEntry(Stream input, string stagingPath, List<string> stagedFiles)
    {
        // Registered before the copy so the finally-cleanup in RunJobAsync removes a
        // partially written *.tmp when the write aborts midway.
        stagedFiles.Add(stagingPath);
        try
        {
            using var output = new FileStream(stagingPath, FileMode.Create, FileAccess.Write, FileShare.None);
            var buffer = new byte[81920];
            long written = 0;
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                written += read;
                if (written > _maxExtractedEntryBytes)
                {
                    throw new FriendlyJobException(
                        $"Das Paket enthält eine unerwartet große Datei (mehr als {_maxExtractedEntryBytes / (1024 * 1024)} MB). Der Download wurde abgebrochen.");
                }
                output.Write(buffer, 0, read);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new FriendlyJobException(TargetNotWritableMessage, ex);
        }
    }

    // Both binaries are fully staged as *.tmp files first, then moved into place — so an
    // interrupted job cannot leave a truncated binary behind (see plan §Seiteneffekte for
    // the accepted residual risk between the two moves).
    private void Install(string stagingDir, string ffmpegName, string ffprobeName)
    {
        File.Move(Path.Combine(stagingDir, ffmpegName + ".tmp"), Path.Combine(stagingDir, ffmpegName), overwrite: true);
        File.Move(Path.Combine(stagingDir, ffprobeName + ".tmp"), Path.Combine(stagingDir, ffprobeName), overwrite: true);

        if (!OperatingSystem.IsWindows())
        {
            const UnixFileMode mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
            File.SetUnixFileMode(Path.Combine(stagingDir, ffmpegName), mode);
            File.SetUnixFileMode(Path.Combine(stagingDir, ffprobeName), mode);
        }
    }

    private void UpdateProgress(Guid jobId, long bytesReceived, long? totalBytes)
    {
        lock (_syncRoot)
        {
            if (_current.Id != jobId)
                return;

            _current = _current with { BytesReceived = bytesReceived, TotalBytes = totalBytes };
        }
    }

    private void SetSnapshot(
        Guid jobId,
        FfmpegToolDownloadJobStatus status,
        string? message,
        string? error,
        DateTimeOffset? completedAtUtc)
    {
        lock (_syncRoot)
        {
            if (_current.Id != jobId)
                return;

            _current = _current with
            {
                Status = status,
                Message = message,
                Error = error,
                CompletedAtUtc = completedAtUtc
            };
        }
    }

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete temporary file {Path}.", path);
        }
    }

    /// <summary>
    /// An <see cref="InvalidOperationException"/> whose message is deliberately phrased for
    /// non-technical admins — these are the only exception texts the UI shows verbatim
    /// (see <see cref="UserFacingError"/>).
    /// </summary>
    private sealed class FriendlyJobException(string message, Exception? inner = null)
        : InvalidOperationException(message, inner);
}

/// <summary>
/// Describes the execution state of an ffmpeg tool download job.
/// </summary>
public enum FfmpegToolDownloadJobStatus
{
    /// <summary>
    /// No download job is known.
    /// </summary>
    Idle,

    /// <summary>
    /// The job has been accepted and is waiting for execution.
    /// </summary>
    Queued,

    /// <summary>
    /// The package is being downloaded, extracted or installed.
    /// </summary>
    Running,

    /// <summary>
    /// Both binaries were installed successfully.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The download or installation failed.
    /// </summary>
    Failed
}

/// <summary>
/// Immutable status snapshot for the current ffmpeg tool download job.
/// </summary>
/// <param name="Id">The job identifier.</param>
/// <param name="Status">The current job status.</param>
/// <param name="StartedAtUtc">The time the job was started.</param>
/// <param name="CompletedAtUtc">The time the job completed.</param>
/// <param name="Message">A human-readable status message.</param>
/// <param name="Error">The failure message when the job failed.</param>
/// <param name="BytesReceived">The number of package bytes downloaded so far.</param>
/// <param name="TotalBytes">The total package size, or <c>null</c> when the server did not
/// send a <c>Content-Length</c> header.</param>
public sealed record FfmpegToolDownloadJobSnapshot(
    Guid Id,
    FfmpegToolDownloadJobStatus Status,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? Message,
    string? Error,
    long BytesReceived,
    long? TotalBytes)
{
    /// <summary>
    /// Represents the empty initial state.
    /// </summary>
    public static FfmpegToolDownloadJobSnapshot Idle { get; } = new(
        Guid.Empty,
        FfmpegToolDownloadJobStatus.Idle,
        null,
        null,
        null,
        null,
        0,
        null);

    /// <summary>
    /// Indicates whether the job is queued or running.
    /// </summary>
    public bool IsActive => Status is FfmpegToolDownloadJobStatus.Queued or FfmpegToolDownloadJobStatus.Running;
}

/// <summary>
/// Result of a download start attempt.
/// </summary>
/// <param name="Started">True when a new job was started.</param>
/// <param name="Snapshot">The job snapshot after the start attempt.</param>
public sealed record FfmpegToolDownloadStartResult(bool Started, FfmpegToolDownloadJobSnapshot Snapshot);
