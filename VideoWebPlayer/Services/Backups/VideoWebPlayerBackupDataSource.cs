using Microsoft.AspNetCore.Hosting;
using msTools.Backup;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services.Backups;

/// <summary>
/// Provides the VideoWebPlayer database as backup data for the new msTools.Backup object API.
/// </summary>
public sealed class VideoWebPlayerBackupDataSource : IBackupDataSource
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<VideoWebPlayerBackupData> _logger;

    /// <summary>
    /// Creates a new data source.
    /// </summary>
    public VideoWebPlayerBackupDataSource(
        ApplicationDbContext db,
        IWebHostEnvironment environment,
        ILogger<VideoWebPlayerBackupData> logger)
    {
        _db = db;
        _environment = environment;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IBackupData>> GetBackupDataAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<IBackupData> result = new List<IBackupData>
        {
            new VideoWebPlayerBackupData(
                "videowebplayer/database",
                "VideoWebPlayer:Database",
                _db,
                _environment,
                _logger)
        };
        return Task.FromResult(result);
    }
}
