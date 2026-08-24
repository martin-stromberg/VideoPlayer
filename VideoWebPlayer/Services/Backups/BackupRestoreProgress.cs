namespace VideoWebPlayer.Services.Backups;

/// <summary>
/// Describes the progress of a backup restore operation.
/// </summary>
public sealed record BackupRestoreProgress(
    string? DataSetName,
    int DataSetNumber,
    int DataSetTotal,
    int RecordNumber,
    int RecordTotal,
    string? Message);
