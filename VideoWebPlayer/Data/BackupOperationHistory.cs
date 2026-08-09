namespace VideoWebPlayer.Data;

/// <summary>
/// Records backup and restore operations for administrator auditability.
/// </summary>
public sealed class BackupOperationHistory
{
    /// <summary>
    /// Gets or sets the history row identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the operation start timestamp.
    /// </summary>
    public DateTime StartedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the operation completion timestamp.
    /// </summary>
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the operation name.
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the affected file name.
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Gets or sets the backup generation.
    /// </summary>
    public string? Generation { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the operation succeeded.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the administrator user identifier.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets the user-facing operation message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
