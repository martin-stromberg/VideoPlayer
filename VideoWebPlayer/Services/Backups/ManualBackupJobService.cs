namespace VideoWebPlayer.Services.Backups;

/// <summary>
/// Starts and tracks manual backup jobs outside the HTTP request lifetime.
/// </summary>
public sealed class ManualBackupJobService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ManualBackupJobService> _logger;
    private readonly object _syncRoot = new();
    private ManualBackupJobSnapshot _current = ManualBackupJobSnapshot.Idle;

    /// <summary>
    /// Creates a new manual backup job service.
    /// </summary>
    public ManualBackupJobService(IServiceScopeFactory scopeFactory, ILogger<ManualBackupJobService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current manual backup job state.
    /// </summary>
    public ManualBackupJobSnapshot GetSnapshot()
    {
        lock (_syncRoot)
        {
            return _current;
        }
    }

    /// <summary>
    /// Starts a manual backup when no other manual backup is active.
    /// </summary>
    public ManualBackupStartResult StartManualBackup(string? userId)
    {
        ManualBackupJobSnapshot snapshot;

        lock (_syncRoot)
        {
            if (_current.IsActive)
                return new ManualBackupStartResult(false, _current);

            snapshot = new ManualBackupJobSnapshot(
                Guid.NewGuid(),
                ManualBackupJobStatus.Queued,
                DateTimeOffset.UtcNow,
                null,
                "Backup wurde gestartet.",
                null);

            _current = snapshot;
        }

        _ = Task.Run(() => RunBackupAsync(snapshot.Id, userId));
        return new ManualBackupStartResult(true, snapshot);
    }

    private async Task RunBackupAsync(Guid jobId, string? userId)
    {
        SetSnapshot(jobId, ManualBackupJobStatus.Running, "Backup wird erstellt.", null, completedAtUtc: null);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var backupFacade = scope.ServiceProvider.GetRequiredService<VideoWebPlayerBackupFacade>();
            var result = await backupFacade.CreateManualBackupAsync(userId, CancellationToken.None);

            if (result.Succeeded)
            {
                SetSnapshot(jobId, ManualBackupJobStatus.Succeeded, result.Message, null, DateTimeOffset.UtcNow);
                return;
            }

            var error = string.Join(" ", result.Errors.DefaultIfEmpty(result.Message));
            SetSnapshot(jobId, ManualBackupJobStatus.Failed, result.Message, error, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual backup job {JobId} failed.", jobId);
            SetSnapshot(jobId, ManualBackupJobStatus.Failed, "Backup konnte nicht erstellt werden.", ex.Message, DateTimeOffset.UtcNow);
        }
    }

    private void SetSnapshot(
        Guid jobId,
        ManualBackupJobStatus status,
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
}

/// <summary>
/// Describes the execution state of a manual backup job.
/// </summary>
public enum ManualBackupJobStatus
{
    /// <summary>
    /// No manual backup job is known.
    /// </summary>
    Idle,

    /// <summary>
    /// The job has been accepted and is waiting for execution.
    /// </summary>
    Queued,

    /// <summary>
    /// The backup is currently being created.
    /// </summary>
    Running,

    /// <summary>
    /// The backup completed successfully.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The backup failed.
    /// </summary>
    Failed
}

/// <summary>
/// Immutable status snapshot for the current manual backup job.
/// </summary>
/// <param name="Id">The job identifier.</param>
/// <param name="Status">The current job status.</param>
/// <param name="StartedAtUtc">The time the job was started.</param>
/// <param name="CompletedAtUtc">The time the job completed.</param>
/// <param name="Message">A human-readable status message.</param>
/// <param name="Error">The failure message when the job failed.</param>
public sealed record ManualBackupJobSnapshot(
    Guid Id,
    ManualBackupJobStatus Status,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? Message,
    string? Error)
{
    /// <summary>
    /// Represents the empty initial state.
    /// </summary>
    public static ManualBackupJobSnapshot Idle { get; } = new(
        Guid.Empty,
        ManualBackupJobStatus.Idle,
        null,
        null,
        null,
        null);

    /// <summary>
    /// Indicates whether the job is queued or running.
    /// </summary>
    public bool IsActive => Status is ManualBackupJobStatus.Queued or ManualBackupJobStatus.Running;
}

/// <summary>
/// Result of a manual backup start attempt.
/// </summary>
/// <param name="Started">True when a new job was started.</param>
/// <param name="Snapshot">The job snapshot after the start attempt.</param>
public sealed record ManualBackupStartResult(bool Started, ManualBackupJobSnapshot Snapshot);
