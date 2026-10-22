using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace VideoWebPlayer.Services;

/// <summary>
/// The ffmpeg input specification for one broadcast slot: a local file path handed to ffmpeg
/// directly (fast <c>-ss</c> seeking, also for MP4 files whose moov atom sits at the end), an
/// <c>http(s)</c> URL of the server's own range-capable media-stream endpoint (seekable via
/// HTTP range requests for remote sources) or a stream pumped into the process's standard
/// input (<c>pipe:0</c>) as the fallback for non-local sources.
/// </summary>
/// <param name="FilePath">The local file path, or <see langword="null"/> for other inputs.</param>
/// <param name="OpenStreamAsync">Factory that opens the media stream for pipe input.</param>
/// <param name="HttpUrl">The range-capable stream URL, or <see langword="null"/>.</param>
public sealed record LiveStreamInput(
    string? FilePath,
    Func<CancellationToken, Task<Stream?>>? OpenStreamAsync,
    string? HttpUrl = null)
{
    /// <summary>Creates a file-path input.</summary>
    public static LiveStreamInput ForFile(string filePath) => new(filePath, null);

    /// <summary>Creates a standard-input stream input.</summary>
    public static LiveStreamInput ForStream(Func<CancellationToken, Task<Stream?>> openStreamAsync)
        => new(null, openStreamAsync);

    /// <summary>Creates a range-capable HTTP URL input (internal media-stream endpoint).</summary>
    /// <param name="httpUrl">The URL handed to ffmpeg's <c>-i</c> argument.</param>
    public static LiveStreamInput ForHttp(string httpUrl) => new(null, null, httpUrl);

    /// <summary>Gets a value indicating whether the input is piped rather than a file or URL.</summary>
    public bool IsPipe => OpenStreamAsync is not null;
}
