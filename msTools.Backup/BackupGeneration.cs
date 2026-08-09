namespace msTools.Backup;

/// <summary>
/// Describes how a backup was created and how retention treats it.
/// </summary>
public enum BackupGeneration
{
    /// <summary>
    /// Backup created manually by an administrator.
    /// </summary>
    Manual,

    /// <summary>
    /// Backup imported by upload.
    /// </summary>
    Uploaded,

    /// <summary>
    /// Daily son generation.
    /// </summary>
    Son,

    /// <summary>
    /// Weekly father generation.
    /// </summary>
    Father,

    /// <summary>
    /// Monthly grandfather generation.
    /// </summary>
    Grandfather
}
