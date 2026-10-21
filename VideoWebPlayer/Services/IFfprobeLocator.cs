namespace VideoWebPlayer.Services;

/// <summary>
/// Resolves the file system location of the ffprobe executable.
/// </summary>
public interface IFfprobeLocator
{
    /// <summary>
    /// Resolves the ffprobe executable path.
    /// </summary>
    /// <returns>The executable path, or <c>null</c> when no ffprobe binary can be found.</returns>
    string? Resolve();
}
