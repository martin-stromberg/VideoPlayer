using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services;

/// <summary>
/// Provides persisted program settings stored in the database.
/// </summary>
public sealed class ProgramSettingsService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ProgramSettingsService> _logger;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="logger">Logger instance.</param>
    public ProgramSettingsService(ApplicationDbContext db, ILogger<ProgramSettingsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Gets the single <see cref="Setup"/> row (creating it if missing).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The persisted setup row.</returns>
    public async Task<Setup> GetOrCreateSetupAsync(CancellationToken cancellationToken = default)
    {
        var setup = await ReadSetupAsync(cancellationToken);
        if (setup is null)
            setup = await CreateSetupAsync(cancellationToken);

        var changed = false;

        if (setup.ScanProcessIntervalMinutes <= 0)
        {
            setup.ScanProcessIntervalMinutes = 60;
            changed = true;
        }

        if (setup.MediaCollectionScanIntervalDays <= 0)
        {
            setup.MediaCollectionScanIntervalDays = 7;
            changed = true;
        }

        if (setup.ContinueWatchingEndThresholdSeconds <= 0)
        {
            setup.ContinueWatchingEndThresholdSeconds = 30;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(setup.ApplicationTitle))
        {
            setup.ApplicationTitle = "Martins Videosammlung";
            changed = true;
        }

        if (changed)
            await _db.SaveChangesAsync(cancellationToken);

        return setup;
    }

    // The Setups table carries exactly one row (enforced by the UX_Setups_SingleRow index);
    // without ORDER BY a corrupted duplicate database could answer this read non-deterministically.
    private Task<Setup?> ReadSetupAsync(CancellationToken cancellationToken)
        => _db.Setups.OrderBy(s => s.Id).FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Inserts the setup row. Two writers can race on an empty table (e.g. the startup data
    /// upgrade and the first request): the loser then hits the single-row unique index on
    /// <c>SaveChanges</c> and reads the winner's row instead of adding a second one.
    /// </summary>
    private async Task<Setup> CreateSetupAsync(CancellationToken cancellationToken)
    {
        var setup = new Setup();
        _db.Setups.Add(setup);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Setup row created for program settings.");
            return setup;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            _db.Entry(setup).State = EntityState.Detached;
            var existing = await ReadSetupAsync(cancellationToken);
            if (existing is null)
                throw;
            return existing;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is SqliteException { SqliteErrorCode: 19 };

    /// <summary>
    /// Returns scan intervals derived from persisted settings.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The scan process interval and the media collection scan interval.</returns>
    public async Task<(TimeSpan ScanProcessInterval, TimeSpan MediaCollectionScanInterval)> GetScanIntervalsAsync(CancellationToken cancellationToken = default)
    {
        var setup = await GetOrCreateSetupAsync(cancellationToken);

        var scanMinutes = setup.ScanProcessIntervalMinutes <= 0 ? 60 : setup.ScanProcessIntervalMinutes;
        var collectionDays = setup.MediaCollectionScanIntervalDays <= 0 ? 7 : setup.MediaCollectionScanIntervalDays;

        return (TimeSpan.FromMinutes(scanMinutes), TimeSpan.FromDays(collectionDays));
    }

    /// <summary>
    /// Updates scan-related program settings.
    /// </summary>
    /// <param name="scanProcessIntervalMinutes">Interval for the scan process in minutes (minimum 1).</param>
    /// <param name="mediaCollectionScanIntervalDays">Interval for re-scanning media collections in days (minimum 1).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task UpdateScanIntervalsAsync(int scanProcessIntervalMinutes, int mediaCollectionScanIntervalDays, CancellationToken cancellationToken = default)
    {
        var setup = await GetOrCreateSetupAsync(cancellationToken);

        setup.ScanProcessIntervalMinutes = Math.Max(1, scanProcessIntervalMinutes);
        setup.MediaCollectionScanIntervalDays = Math.Max(1, mediaCollectionScanIntervalDays);

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the configured application title.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The configured application title or the default title.</returns>
    public async Task<string> GetApplicationTitleAsync(CancellationToken cancellationToken = default)
    {
        var setup = await GetOrCreateSetupAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(setup.ApplicationTitle)
            ? "Martins Videosammlung"
            : setup.ApplicationTitle;
    }

    /// <summary>
    /// Gets the configured end-of-video threshold for continue-watching in seconds.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The configured end-of-video threshold.</returns>
    public async Task<TimeSpan> GetContinueWatchingEndThresholdAsync(CancellationToken cancellationToken = default)
    {
        var setup = await GetOrCreateSetupAsync(cancellationToken);
        var seconds = setup.ContinueWatchingEndThresholdSeconds <= 0
            ? 30
            : setup.ContinueWatchingEndThresholdSeconds;
        return TimeSpan.FromSeconds(seconds);
    }

    /// <summary>
    /// Updates the application title and program settings.
    /// </summary>
    /// <param name="settings">The new general settings values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="DiscoveryUrlValidationException">The public base URL is neither empty nor a valid absolute <c>http</c>/<c>https</c> URL.</exception>
    public async Task UpdateGeneralSettingsAsync(
        GeneralSettingsUpdate settings,
        CancellationToken cancellationToken = default)
    {
        var normalizedBaseUrl = DiscoveryUrlRules.NormalizePublicBaseUrl(settings.DiscoveryPublicBaseUrl);
        if (normalizedBaseUrl is not null && !DiscoveryUrlRules.IsValidPublicBaseUrl(normalizedBaseUrl))
            throw new DiscoveryUrlValidationException($"Die öffentliche Basis-URL {DiscoveryUrlRules.PublicBaseUrlRuleText}");

        var setup = await GetOrCreateSetupAsync(cancellationToken);

        setup.ApplicationTitle = string.IsNullOrWhiteSpace(settings.ApplicationTitle)
            ? "Martins Videosammlung"
            : settings.ApplicationTitle.Trim();
        setup.ScanProcessIntervalMinutes = Math.Max(1, settings.ScanProcessIntervalMinutes);
        setup.MediaCollectionScanIntervalDays = Math.Max(1, settings.MediaCollectionScanIntervalDays);
        setup.ContinueWatchingEndThresholdSeconds = Math.Max(0, settings.ContinueWatchingEndThresholdSeconds);
        setup.MdnsAdvertisementEnabled = settings.MdnsAdvertisementEnabled;
        setup.DiscoveryPublicBaseUrl = normalizedBaseUrl;
        setup.TvSenderEnabled = settings.TvSenderEnabled;

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the admin release switch for the automated TV channel.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Whether the TV channel feature is released.</returns>
    public async Task<bool> GetTvSenderEnabledAsync(CancellationToken cancellationToken = default)
    {
        var setup = await GetOrCreateSetupAsync(cancellationToken);
        return setup.TvSenderEnabled;
    }

    /// <summary>
    /// Persists the admin release switch for the automated TV channel.
    /// </summary>
    /// <param name="enabled">Whether the TV channel feature is released.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task UpdateTvSenderEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        var setup = await GetOrCreateSetupAsync(cancellationToken);
        setup.TvSenderEnabled = enabled;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Combined access check for the automated TV channel: the feature must be released
    /// globally (<see cref="Setup.TvSenderEnabled"/>) <em>and</em> the user must be either an
    /// administrator (implicit access) or listed in <see cref="TvSenderUserAccess"/>.
    /// </summary>
    /// <param name="userId">The user's <see cref="ApplicationUser"/> id, or
    /// <see langword="null"/> when unknown.</param>
    /// <param name="isAdmin">Whether the user is an administrator.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Whether the user may see and use the TV channel.</returns>
    public async Task<bool> IsTvSenderAccessGrantedAsync(string? userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        if (!await GetTvSenderEnabledAsync(cancellationToken))
            return false;
        if (isAdmin)
            return true;
        return !string.IsNullOrEmpty(userId)
            && await _db.TvSenderUserAccess.AnyAsync(a => a.UserId == userId, cancellationToken);
    }

    /// <summary>
    /// Gets the ids of the users explicitly released for the automated TV channel.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The allowed user ids (never <see langword="null"/>).</returns>
    public async Task<HashSet<string>> GetTvSenderAllowedUserIdsAsync(CancellationToken cancellationToken = default)
        => (await _db.TvSenderUserAccess.AsNoTracking().Select(a => a.UserId).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Replaces the per-user release list of the automated TV channel: rows whose user id is
    /// missing from <paramref name="userIds"/> are removed, new ids are inserted — in a single
    /// save so the allowlist is never half-written. Administrators need no row; the caller may
    /// pass their ids as well, they are stored but evaluated via the implicit admin access.
    /// </summary>
    /// <param name="userIds">The complete new set of allowed user ids.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task UpdateTvSenderAllowedUserIdsAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default)
    {
        var wanted = userIds.Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet(StringComparer.Ordinal);
        var existing = await _db.TvSenderUserAccess.Where(a => wanted.Contains(a.UserId)).ToListAsync(cancellationToken);

        // Remove rows not in the new set, then insert the missing ones.
        var kept = existing.Select(a => a.UserId).ToHashSet(StringComparer.Ordinal);
        _db.TvSenderUserAccess.RemoveRange(
            _db.TvSenderUserAccess.Where(a => !wanted.Contains(a.UserId)));
        _db.TvSenderUserAccess.AddRange(wanted.Except(kept, StringComparer.Ordinal)
            .Select(id => new TvSenderUserAccess { UserId = id }));

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the admin-maintained public base URL for discovery answers.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The normalized admin override, or <see langword="null"/> when none is set.</returns>
    public async Task<string?> GetDiscoveryPublicBaseUrlAsync(CancellationToken cancellationToken = default)
    {
        var setup = await GetOrCreateSetupAsync(cancellationToken);
        return DiscoveryUrlRules.NormalizePublicBaseUrl(setup.DiscoveryPublicBaseUrl);
    }

    /// <summary>
    /// Gets the admin switch for the mDNS advertisement of the server.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Whether the admin switch for mDNS advertisement is enabled.</returns>
    public async Task<bool> GetMdnsAdvertisementEnabledAsync(CancellationToken cancellationToken = default)
    {
        var setup = await GetOrCreateSetupAsync(cancellationToken);
        return setup.MdnsAdvertisementEnabled;
    }

    /// <summary>
    /// Persists the admin switch for the mDNS advertisement of the server.
    /// </summary>
    /// <param name="enabled">Whether the server announces itself via mDNS.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task UpdateMdnsAdvertisementEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        var setup = await GetOrCreateSetupAsync(cancellationToken);

        setup.MdnsAdvertisementEnabled = enabled;

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The German federal states (ISO-3166-2 <c>DE-*</c> codes) selectable for the holiday calendar
    /// of the automated TV channel's holiday rules.
    /// </summary>
    public static readonly IReadOnlyList<string> HolidayFederalStates =
    [
        "DE-BW", "DE-BY", "DE-BE", "DE-BB", "DE-HB", "DE-HH", "DE-HE", "DE-MV",
        "DE-NI", "DE-NW", "DE-RP", "DE-SL", "DE-SN", "DE-ST", "DE-SH", "DE-TH"
    ];

    /// <summary>
    /// Gets the configured federal state for the holiday calendar, or <see langword="null"/> when
    /// only Germany-wide holidays apply.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The configured federal-state code, or <see langword="null"/>.</returns>
    public async Task<string?> GetHolidayFederalStateAsync(CancellationToken cancellationToken = default)
    {
        var setup = await GetOrCreateSetupAsync(cancellationToken);
        return setup.HolidayFederalState;
    }

    /// <summary>
    /// Persists the federal state for the holiday calendar of the automated TV channel.
    /// </summary>
    /// <param name="federalState">One of <see cref="HolidayFederalStates"/>, or <see langword="null"/> for Germany-wide holidays only.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException">The value is neither <see langword="null"/> nor a known federal-state code.</exception>
    public async Task UpdateHolidayFederalStateAsync(string? federalState, CancellationToken cancellationToken = default)
    {
        if (federalState is not null && !HolidayFederalStates.Contains(federalState))
            throw new ArgumentException($"Unbekanntes Bundesland: {federalState}");

        var setup = await GetOrCreateSetupAsync(cancellationToken);
        setup.HolidayFederalState = federalState;

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the schedule horizon the automated TV channel plans ahead.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The configured horizon (default 48 hours when unset or invalid).</returns>
    public async Task<TimeSpan> GetProgramScheduleHorizonAsync(CancellationToken cancellationToken = default)
    {
        var setup = await GetOrCreateSetupAsync(cancellationToken);
        var hours = setup.ProgramScheduleHorizonHours <= 0 ? 48 : setup.ProgramScheduleHorizonHours;
        return TimeSpan.FromHours(hours);
    }

    /// <summary>
    /// Gets the interval at which the schedule worker extends the program schedule.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The configured interval (default 15 minutes when unset or invalid).</returns>
    public async Task<TimeSpan> GetProgramScheduleIntervalAsync(CancellationToken cancellationToken = default)
    {
        var setup = await GetOrCreateSetupAsync(cancellationToken);
        var minutes = setup.ProgramScheduleIntervalMinutes <= 0 ? 15 : setup.ProgramScheduleIntervalMinutes;
        return TimeSpan.FromMinutes(minutes);
    }

    /// <summary>
    /// Persists the schedule horizon and generation interval of the automated TV channel.
    /// </summary>
    /// <param name="horizonHours">Schedule horizon in hours (minimum 1).</param>
    /// <param name="intervalMinutes">Generation interval in minutes (minimum 1).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException">A value is smaller than 1.</exception>
    public async Task UpdateProgramScheduleSettingsAsync(int horizonHours, int intervalMinutes, CancellationToken cancellationToken = default)
    {
        if (horizonHours < 1)
            throw new ArgumentException("Der Planungshorizont muss mindestens 1 Stunde betragen.");
        if (intervalMinutes < 1)
            throw new ArgumentException("Das Generierungsintervall muss mindestens 1 Minute betragen.");

        var setup = await GetOrCreateSetupAsync(cancellationToken);
        setup.ProgramScheduleHorizonHours = horizonHours;
        setup.ProgramScheduleIntervalMinutes = intervalMinutes;

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the target HLS segment length of the live stream.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The configured segment length (default 6 seconds when unset or invalid).</returns>
    public async Task<TimeSpan> GetLiveStreamSegmentLengthAsync(CancellationToken cancellationToken = default)
    {
        var setup = await GetOrCreateSetupAsync(cancellationToken);
        var seconds = setup.LiveStreamSegmentLengthSeconds <= 0 ? 6 : setup.LiveStreamSegmentLengthSeconds;
        return TimeSpan.FromSeconds(seconds);
    }

    /// <summary>
    /// Gets the admin-configured segment directory of the live stream, or <see langword="null"/>
    /// when the default directory applies.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The configured directory, or <see langword="null"/>.</returns>
    public async Task<string?> GetLiveStreamSegmentDirectoryAsync(CancellationToken cancellationToken = default)
    {
        var setup = await GetOrCreateSetupAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(setup.LiveStreamSegmentDirectory) ? null : setup.LiveStreamSegmentDirectory;
    }

    /// <summary>
    /// Gets the retention of expired live stream segments.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The configured retention (default 30 minutes when unset or invalid).</returns>
    public async Task<TimeSpan> GetLiveStreamSegmentRetentionAsync(CancellationToken cancellationToken = default)
    {
        var setup = await GetOrCreateSetupAsync(cancellationToken);
        var minutes = setup.LiveStreamSegmentRetentionMinutes <= 0 ? 30 : setup.LiveStreamSegmentRetentionMinutes;
        return TimeSpan.FromMinutes(minutes);
    }

    /// <summary>
    /// Persists the live stream settings of the automated TV channel.
    /// </summary>
    /// <param name="segmentLengthSeconds">Target HLS segment length in seconds (1–300).</param>
    /// <param name="segmentDirectory">Segment directory; <see langword="null"/> or empty for the
    /// default <c>Data/LiveStream</c>. A relative path resolves against the content root.</param>
    /// <param name="retentionMinutes">Segment retention in minutes (minimum 1).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException">A value is outside its valid range or the path is invalid.</exception>
    public async Task UpdateLiveStreamSettingsAsync(int segmentLengthSeconds, string? segmentDirectory, int retentionMinutes, CancellationToken cancellationToken = default)
    {
        if (segmentLengthSeconds < 1 || segmentLengthSeconds > 300)
            throw new ArgumentException("Die Segmentlänge muss zwischen 1 und 300 Sekunden liegen.");
        if (retentionMinutes < 1)
            throw new ArgumentException("Die Segment-Aufbewahrung muss mindestens 1 Minute betragen.");

        string? directory = null;
        if (!string.IsNullOrWhiteSpace(segmentDirectory))
        {
            try
            {
                // Validation probe only — the raw value is stored; the segment store resolves
                // relative paths against the content root, not the process working directory.
                _ = Path.GetFullPath(segmentDirectory.Trim());
                directory = segmentDirectory.Trim();
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                throw new ArgumentException($"Ungültiges Segment-Verzeichnis: {segmentDirectory}");
            }
        }

        var setup = await GetOrCreateSetupAsync(cancellationToken);
        setup.LiveStreamSegmentLengthSeconds = segmentLengthSeconds;
        setup.LiveStreamSegmentDirectory = directory;
        setup.LiveStreamSegmentRetentionMinutes = retentionMinutes;

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the media item configured as the live stream's standby clip, or
    /// <see langword="null"/> when the embedded default clip applies.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The configured media item id, or <see langword="null"/>.</returns>
    public async Task<long?> GetLiveStreamStandbyMediaItemIdAsync(CancellationToken cancellationToken = default)
    {
        var setup = await GetOrCreateSetupAsync(cancellationToken);
        return setup.LiveStreamStandbyMediaItemId;
    }

    /// <summary>
    /// Persists the standby clip of the live stream's warm-up phase. <see langword="null"/>
    /// clears the selection so the embedded default clip applies. A selected item must exist,
    /// be a video file (<see cref="Utils.VideoFileExtensions"/>) and must not be classified
    /// (no movie/episode link) — the same eligibility rule as the clip-group candidates.
    /// </summary>
    /// <param name="mediaItemId">The media item to use as standby clip, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">The media item does not exist.</exception>
    /// <exception cref="ArgumentException">The media item is no video file or is classified.</exception>
    public async Task UpdateLiveStreamStandbyClipAsync(long? mediaItemId, CancellationToken cancellationToken = default)
    {
        if (mediaItemId is { } id)
        {
            var item = await _db.MediaItems
                .AsNoTracking()
                .Where(mi => mi.Id == id)
                .Select(mi => new { mi.Id, mi.Path })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException("Das gewählte Standby-Video wurde nicht gefunden.");
            await MediaItemClipEligibility.EnsureUnclassifiedVideoClipAsync(
                _db, id, item.Path, "als Standby-Clip ausgewählt werden", cancellationToken);
        }

        var setup = await GetOrCreateSetupAsync(cancellationToken);
        setup.LiveStreamStandbyMediaItemId = mediaItemId;
        await _db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Describes the administrator-submitted general program settings, written atomically by
/// <see cref="ProgramSettingsService.UpdateGeneralSettingsAsync"/>.
/// </summary>
/// <param name="ApplicationTitle">Application title shown in the UI.</param>
/// <param name="ScanProcessIntervalMinutes">Interval for the scan process in minutes.</param>
/// <param name="MediaCollectionScanIntervalDays">Interval for re-scanning media collections in days.</param>
/// <param name="ContinueWatchingEndThresholdSeconds">Seconds before the end of a video after which the position is no longer saved.</param>
/// <param name="MdnsAdvertisementEnabled">Whether the server announces itself via mDNS.</param>
/// <param name="DiscoveryPublicBaseUrl">Public base URL for discovery answers; empty clears the admin override. Must be an absolute <c>http</c>/<c>https</c> URL when set.</param>
/// <param name="TvSenderEnabled">Whether the automated TV channel feature is released.</param>
/// <returns>The parameter object for the atomic general-settings update.</returns>
public sealed record GeneralSettingsUpdate(
    string ApplicationTitle,
    int ScanProcessIntervalMinutes,
    int MediaCollectionScanIntervalDays,
    int ContinueWatchingEndThresholdSeconds,
    bool MdnsAdvertisementEnabled,
    string? DiscoveryPublicBaseUrl,
    bool TvSenderEnabled);
