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
        var setup = await _db.Setups.FirstOrDefaultAsync(cancellationToken);
        if (setup is not null)
        {
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

        setup = new Setup();
        _db.Setups.Add(setup);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Setup row created for program settings.");
        return setup;
    }

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
/// <returns>The parameter object for the atomic general-settings update.</returns>
public sealed record GeneralSettingsUpdate(
    string ApplicationTitle,
    int ScanProcessIntervalMinutes,
    int MediaCollectionScanIntervalDays,
    int ContinueWatchingEndThresholdSeconds,
    bool MdnsAdvertisementEnabled,
    string? DiscoveryPublicBaseUrl);
