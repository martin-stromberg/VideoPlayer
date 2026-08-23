using msTools.Backup;

namespace VideoWebPlayer.Services.Backups;

/// <summary>
/// Creates concrete <see cref="IBackupData"/> instances during restore.
/// </summary>
public sealed class VideoWebPlayerBackupDataFactory : IBackupDataFactory
{
    private readonly VideoWebPlayerBackupDataProvider _provider;

    /// <summary>
    /// Gets or sets the user id to keep during restore.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets the progress reporter for restore operations.
    /// </summary>
    public IProgress<BackupRestoreProgress>? Progress { get; set; }

    /// <summary>
    /// Creates a new factory.
    /// </summary>
    public VideoWebPlayerBackupDataFactory(VideoWebPlayerBackupDataProvider provider)
    {
        _provider = provider;
    }

    /// <inheritdoc />
    public IBackupData Create(string contentType, string name) => contentType switch
    {
        "VideoWebPlayer:Database" => new VideoWebPlayerBackupData(name, contentType, _provider, this),
        _ => throw new NotSupportedException($"Unknown backup content type: {contentType}")
    };
}
