namespace VideoWebPlayer.Services;

/// <summary>
/// Resolves the ffmpeg executable used for the live stream's HLS segmentation.
/// </summary>
public interface IFfmpegLocator
{
    /// <summary>
    /// Resolves the ffmpeg executable path.
    /// </summary>
    /// <returns>The executable path, or <see langword="null"/> when ffmpeg cannot be found.</returns>
    string? Resolve();
}
