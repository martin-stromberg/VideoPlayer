namespace msTools.Backup;

/// <summary>
/// Describes a stored backup file.
/// </summary>
public sealed record BackupDescriptor(
    string FileName,
    string Path,
    long SizeBytes,
    DateTimeOffset CreatedAtUtc,
    BackupGeneration Generation,
    string ProviderId,
    int FormatVersion,
    bool IsValid,
    IReadOnlyList<string> ValidationErrors);

/// <summary>
/// Manifest written to each backup ZIP file.
/// </summary>
public sealed class BackupManifest
{
    /// <summary>
    /// Gets or sets the backup format version.
    /// </summary>
    public int FormatVersion { get; set; } = 1;

    /// <summary>
    /// Gets or sets the host data provider identifier.
    /// </summary>
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the application name.
    /// </summary>
    public string AppName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the backup generation.
    /// </summary>
    public BackupGeneration Generation { get; set; }

    /// <summary>
    /// Gets or sets the payload entries contained in the ZIP file.
    /// </summary>
    public List<string> PayloadEntries { get; set; } = new() { "data.json" };
}

/// <summary>
/// Describes an additional file payload entry written to a backup ZIP.
/// </summary>
public sealed record BackupFileAttachment(
    string EntryName,
    Func<Stream, CancellationToken, Task> WriteAsync);

/// <summary>
/// Describes the result of validating a backup stream.
/// </summary>
public sealed class BackupValidationResult
{
    /// <summary>
    /// Gets a valid result.
    /// </summary>
    public static BackupValidationResult Valid { get; } = new(true, Array.Empty<string>());

    /// <summary>
    /// Creates a validation result.
    /// </summary>
    public BackupValidationResult(bool isValid, IReadOnlyList<string> errors)
    {
        IsValid = isValid;
        Errors = errors;
    }

    /// <summary>
    /// Gets a value indicating whether validation succeeded.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Gets validation errors.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// Creates an invalid validation result.
    /// </summary>
    public static BackupValidationResult Invalid(params string[] errors) => new(false, errors);
}

/// <summary>
/// Describes a backup creation request.
/// </summary>
public sealed record BackupCreateRequest(BackupGeneration Generation, string AppName = "Application");

/// <summary>
/// Describes a restore request.
/// </summary>
public sealed record BackupRestoreRequest(string FileName, string? UserId, bool ConfirmRestore);

/// <summary>
/// Contains host context for restore operations.
/// </summary>
public sealed record BackupRestoreContext(
    string? UserId,
    Func<string, CancellationToken, Task<Stream>>? OpenPayloadEntryAsync = null);

/// <summary>
/// Describes the result of a backup operation.
/// </summary>
public sealed class BackupOperationResult
{
    private BackupOperationResult(bool succeeded, string message, BackupDescriptor? descriptor, IReadOnlyList<string> errors)
    {
        Succeeded = succeeded;
        Message = message;
        Descriptor = descriptor;
        Errors = errors;
    }

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// Gets a user-facing message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the affected backup descriptor, when available.
    /// </summary>
    public BackupDescriptor? Descriptor { get; }

    /// <summary>
    /// Gets technical or validation errors.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// Creates a successful operation result.
    /// </summary>
    public static BackupOperationResult Success(string message, BackupDescriptor? descriptor = null)
        => new(true, message, descriptor, Array.Empty<string>());

    /// <summary>
    /// Creates a failed operation result.
    /// </summary>
    public static BackupOperationResult Failure(string message, params string[] errors)
        => new(false, message, null, errors);
}
