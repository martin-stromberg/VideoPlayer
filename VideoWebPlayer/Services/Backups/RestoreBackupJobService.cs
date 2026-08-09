using msTools.Backup;

namespace VideoWebPlayer.Services.Backups;

/// <summary>
/// Starts and tracks restore jobs outside the HTTP request lifetime.
/// </summary>
public sealed class RestoreBackupJobService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RestoreBackupJobService> _logger;
    private readonly object _syncRoot = new();
    private RestoreBackupJobSnapshot _current = RestoreBackupJobSnapshot.Idle;

    /// <summary>
    /// Creates a new restore job service.
    /// </summary>
    public RestoreBackupJobService(IServiceScopeFactory scopeFactory, ILogger<RestoreBackupJobService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current restore job state.
    /// </summary>
    public RestoreBackupJobSnapshot GetSnapshot()
    {
        lock (_syncRoot)
        {
            return _current;
        }
    }

    /// <summary>
    /// Starts a restore when no other restore is active.
    /// </summary>
    public RestoreBackupStartResult StartRestore(string fileName, string? userId, bool confirmRestore)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Backup-Dateiname fehlt.", nameof(fileName));

        RestoreBackupJobSnapshot snapshot;

        lock (_syncRoot)
        {
            if (_current.IsActive)
                return new RestoreBackupStartResult(false, _current);

            snapshot = new RestoreBackupJobSnapshot(
                Guid.NewGuid(),
                RestoreBackupJobStatus.Queued,
                fileName,
                DateTimeOffset.UtcNow,
                null,
                "Restore wurde gestartet.",
                null,
                RestoreBackupProgressSnapshot.Empty);

            _current = snapshot;
        }

        _ = Task.Run(() => RunRestoreAsync(snapshot.Id, fileName, userId, confirmRestore));
        return new RestoreBackupStartResult(true, snapshot);
    }

    private async Task RunRestoreAsync(Guid jobId, string fileName, string? userId, bool confirmRestore)
    {
        SetSnapshot(jobId, RestoreBackupJobStatus.Running, "Restore läuft.", null, completedAtUtc: null);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var backupFacade = scope.ServiceProvider.GetRequiredService<VideoWebPlayerBackupFacade>();
            var progress = new RestoreJobProgress(this, jobId);
            var result = await backupFacade.RestoreAsync(fileName, userId, confirmRestore, progress, CancellationToken.None);

            if (result.Succeeded)
            {
                SetSnapshot(jobId, RestoreBackupJobStatus.Succeeded, result.Message, null, DateTimeOffset.UtcNow);
                return;
            }

            var error = string.Join(" ", result.Errors.DefaultIfEmpty(result.Message));
            SetSnapshot(jobId, RestoreBackupJobStatus.Failed, result.Message, error, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore job {JobId} failed for {FileName}.", jobId, fileName);
            SetSnapshot(jobId, RestoreBackupJobStatus.Failed, "Backup konnte nicht wiederhergestellt werden.", ex.Message, DateTimeOffset.UtcNow);
        }
    }

    private void SetSnapshot(
        Guid jobId,
        RestoreBackupJobStatus status,
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

    private void SetProgress(Guid jobId, BackupRestoreProgress progress)
    {
        lock (_syncRoot)
        {
            if (_current.Id != jobId)
                return;

            _current = _current with
            {
                Progress = new RestoreBackupProgressSnapshot(
                    progress.DataSetName,
                    progress.DataSetNumber,
                    progress.DataSetTotal,
                    progress.RecordNumber,
                    progress.RecordTotal,
                    progress.Message),
                Message = progress.Message
            };
        }
    }

    private sealed class RestoreJobProgress : IProgress<BackupRestoreProgress>
    {
        private readonly RestoreBackupJobService _jobs;
        private readonly Guid _jobId;

        public RestoreJobProgress(RestoreBackupJobService jobs, Guid jobId)
        {
            _jobs = jobs;
            _jobId = jobId;
        }

        public void Report(BackupRestoreProgress value)
        {
            _jobs.SetProgress(_jobId, value);
        }
    }
}

/// <summary>
/// Describes the execution state of a restore job.
/// </summary>
public enum RestoreBackupJobStatus
{
    /// <summary>
    /// No restore job is known.
    /// </summary>
    Idle,

    /// <summary>
    /// The job has been accepted and is waiting for execution.
    /// </summary>
    Queued,

    /// <summary>
    /// The restore is currently running.
    /// </summary>
    Running,

    /// <summary>
    /// The restore completed successfully.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The restore failed.
    /// </summary>
    Failed
}

/// <summary>
/// Immutable status snapshot for the current restore job.
/// </summary>
public sealed record RestoreBackupJobSnapshot(
    Guid Id,
    RestoreBackupJobStatus Status,
    string? FileName,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? Message,
    string? Error,
    RestoreBackupProgressSnapshot Progress)
{
    /// <summary>
    /// Represents the empty initial state.
    /// </summary>
    public static RestoreBackupJobSnapshot Idle { get; } = new(
        Guid.Empty,
        RestoreBackupJobStatus.Idle,
        null,
        null,
        null,
        null,
        null,
        RestoreBackupProgressSnapshot.Empty);

    /// <summary>
    /// Indicates whether the job is queued or running.
    /// </summary>
    public bool IsActive => Status is RestoreBackupJobStatus.Queued or RestoreBackupJobStatus.Running;
}

/// <summary>
/// Two-level restore progress snapshot.
/// </summary>
public sealed record RestoreBackupProgressSnapshot(
    string? DataSetName,
    int DataSetNumber,
    int DataSetTotal,
    int RecordNumber,
    int RecordTotal,
    string? Message)
{
    /// <summary>
    /// Empty progress.
    /// </summary>
    public static RestoreBackupProgressSnapshot Empty { get; } = new(null, 0, 0, 0, 0, null);
}

/// <summary>
/// Result of a restore start attempt.
/// </summary>
public sealed record RestoreBackupStartResult(bool Started, RestoreBackupJobSnapshot Snapshot);
