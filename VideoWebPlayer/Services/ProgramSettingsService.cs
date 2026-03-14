using Microsoft.EntityFrameworkCore;
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
}
