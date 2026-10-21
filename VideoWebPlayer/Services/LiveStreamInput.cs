using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace VideoWebPlayer.Services;

/// <summary>
/// The ffmpeg input specification for one broadcast slot: either a local file path handed to
/// ffmpeg directly (fast <c>-ss</c> seeking, also for MP4 files whose moov atom sits at the end)
/// or a stream pumped into the process's standard input (<c>pipe:0</c>) for non-local sources.
/// </summary>
/// <param name="FilePath">The local file path, or <see langword="null"/> for stream input.</param>
/// <param name="OpenStreamAsync">Factory that opens the media stream for pipe input.</param>
public sealed record LiveStreamInput(string? FilePath, Func<CancellationToken, Task<Stream?>>? OpenStreamAsync)
{
    /// <summary>Creates a file-path input.</summary>
    public static LiveStreamInput ForFile(string filePath) => new(filePath, null);

    /// <summary>Creates a standard-input stream input.</summary>
    public static LiveStreamInput ForStream(Func<CancellationToken, Task<Stream?>> openStreamAsync)
        => new(null, openStreamAsync);

    /// <summary>Gets a value indicating whether the input is piped rather than a file.</summary>
    public bool IsPipe => FilePath is null;
}
