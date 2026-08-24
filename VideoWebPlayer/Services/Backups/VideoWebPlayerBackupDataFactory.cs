using Microsoft.AspNetCore.Hosting;
using msTools.Backup;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services.Backups;

/// <summary>
/// Creates VideoWebPlayer backup data objects for the new msTools.Backup object API.
/// </summary>
public sealed class VideoWebPlayerBackupDataFactory : IBackupDataFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<VideoWebPlayerBackupData> _logger;

    /// <summary>
    /// Gets or sets the user id to preserve as admin during restore.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets the progress reporter for restore operations.
    /// </summary>
    public IProgress<BackupRestoreProgress>? Progress { get; set; }

    /// <summary>
    /// Creates a new factory.
    /// </summary>
    public VideoWebPlayerBackupDataFactory(
        IServiceProvider serviceProvider,
        IWebHostEnvironment environment,
        ILogger<VideoWebPlayerBackupData> logger)
    {
        _serviceProvider = serviceProvider;
        _environment = environment;
        _logger = logger;
    }

    /// <inheritdoc />
    public IBackupData Create(string contentType, string name)
    {
        if (!string.Equals(contentType, "VideoWebPlayer:Database", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Backup content type '{contentType}' is not supported.");

        var db = _serviceProvider.GetRequiredService<ApplicationDbContext>();
        return new VideoWebPlayerBackupData(name, contentType, db, _environment, _logger, this);
    }
}
