namespace VideoWebPlayer.Configuration;

/// <summary>
/// Options for the automated TV channel's live stream segmentation (section
/// <c>LiveStream</c>). Segments length, directory and retention live in the
/// <c>Setup</c> table (admin-editable); this section holds infrastructure switches.
/// </summary>
public sealed class LiveStreamOptions
{
    /// <summary>
    /// Gets or sets whether remote media items are fed to the segmenting ffmpeg process
    /// through the server's own range-capable media-stream endpoint
    /// (<c>api/internal/media-stream/{token}</c>) — ffmpeg then reads with HTTP range
    /// requests, can seek for <c>-ss</c> and handles MP4 files whose moov atom sits at
    /// the end. When <see langword="false"/>, remote sources are pumped into the
    /// process's standard input (<c>pipe:0</c>) as before.
    /// </summary>
    public bool UseHttpInput { get; set; } = true;

    /// <summary>
    /// Gets or sets whether remote media items of SFTP sources may be fed to the segmenting
    /// ffmpeg process through a direct <c>sftp://user:pass@host/path</c> URI — seekable via
    /// libssh like the HTTP input. The URI carries the source credentials in ffmpeg's
    /// command line (visible to local users via the process list), so multi-user systems
    /// may want to disable it; <see langword="false"/> leaves only <c>pipe:0</c> after HTTP.
    /// </summary>
    public bool UseSftpInput { get; set; } = true;
}
