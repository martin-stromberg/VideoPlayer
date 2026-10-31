using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace VideoWebPlayer.Services;

/// <summary>
/// Owns the live stream's segment directory: resolves the effective location (admin-configured
/// or the default <c>Data/LiveStream</c> below the content root), serves individual files out of
/// it with strict path containment, removes expired segments by the configured retention and
/// clears the directory when a fresh streaming run starts.
/// </summary>
public sealed class LiveStreamSegmentStore
{
    /// <summary>The file name safe-whitelist: letters, digits, dot, underscore and dash only —
    /// no separators, so a served file can never leave the segment directory.</summary>
    internal static readonly Regex SafeFileName = new("^[A-Za-z0-9][A-Za-z0-9._-]*$", RegexOptions.Compiled);

    private static readonly string DefaultRelativeDirectory = Path.Combine("Data", "LiveStream");
    internal const string SegmentFilePrefix = "seg_";
    internal const string SegmentFileExtension = ".ts";

    private readonly IWebHostEnvironment _environment;
    private readonly ProgramSettingsService _settings;
    private readonly ILogger<LiveStreamSegmentStore> _logger;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    /// <param name="environment">Host environment (content root for default/relative directories).</param>
    /// <param name="settings">Program settings service.</param>
    /// <param name="logger">Logger instance.</param>
    public LiveStreamSegmentStore(
        IWebHostEnvironment environment,
        ProgramSettingsService settings,
        ILogger<LiveStreamSegmentStore> logger)
    {
        _environment = environment;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Resolves the effective segment directory and creates it when missing: the admin-configured
    /// path (absolute as-is, relative against the content root), or the default
    /// <c>Data/LiveStream</c> below the content root.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The absolute segment directory.</returns>
    public async Task<string> GetDirectoryAsync(CancellationToken cancellationToken = default)
    {
        var configured = await _settings.GetLiveStreamSegmentDirectoryAsync(cancellationToken);
        var directory = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(_environment.ContentRootPath, DefaultRelativeDirectory)
            : Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(_environment.ContentRootPath, configured);

        Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>
    /// Resolves a requested file name to an existing file inside the segment directory —
    /// <see langword="null"/> for invalid names, containment escapes or missing files.
    /// </summary>
    /// <param name="fileName">The requested file name (never a path).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The absolute file path, or <see langword="null"/>.</returns>
    public async Task<string?> TryResolveFileAsync(string fileName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName) || !SafeFileName.IsMatch(fileName))
            return null;

        var directory = Path.GetFullPath(await GetDirectoryAsync(cancellationToken));
        string candidate;
        try
        {
            candidate = Path.GetFullPath(Path.Combine(directory, fileName));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        var root = directory.EndsWith(Path.DirectorySeparatorChar) ? directory : directory + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase) && !candidate.Equals(directory, StringComparison.OrdinalIgnoreCase))
            return null;

        return File.Exists(candidate) ? candidate : null;
    }

    /// <summary>
    /// Removes expired HLS segment files: every <c>seg_*.ts</c> whose last write is older than
    /// <paramref name="retention"/> before <paramref name="utcNow"/>. The playlist and files of
    /// other kinds are never touched.
    /// </summary>
    /// <param name="utcNow">The reference time.</param>
    /// <param name="retention">How long segments are kept.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of deleted files.</returns>
    public async Task<int> SweepExpiredSegmentsAsync(DateTimeOffset utcNow, TimeSpan retention, CancellationToken cancellationToken = default)
    {
        var directory = await GetDirectoryAsync(cancellationToken);
        var cutoff = utcNow.UtcDateTime - retention;
        var deleted = 0;

        foreach (var file in Directory.EnumerateFiles(directory, $"{SegmentFilePrefix}*{SegmentFileExtension}"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                {
                    File.Delete(file);
                    deleted++;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogDebug(ex, "[LiveStreamSegmentStore] Abgelaufenes Segment '{File}' konnte nicht geloescht werden.", file);
            }
        }

        if (deleted > 0)
            _logger.LogInformation("[LiveStreamSegmentStore] {Count} abgelaufene Segmente entfernt.", deleted);
        return deleted;
    }

    /// <summary>
    /// Clears the segment directory completely — used once at worker start, since leftovers of a
    /// previous run carry no usable program point.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        var directory = await GetDirectoryAsync(cancellationToken);
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogDebug(ex, "[LiveStreamSegmentStore] Datei '{File}' konnte beim Leeren nicht geloescht werden.", file);
            }
        }
    }
}
