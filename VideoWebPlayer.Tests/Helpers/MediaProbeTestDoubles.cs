using VideoWebPlayer.Data;
using VideoWebPlayer.Services;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Shared test doubles for <see cref="MediaDurationProbeService"/> tests: an ffprobe locator
/// that always resolves, a media-source reader stub with configurable stream behavior and a
/// probe that always reports a fixed duration. Centralized so an interface change is followed
/// up in one place instead of several private copies.
/// </summary>
internal sealed class PresentLocator : IFfprobeLocator
{
    public string? Resolve() => "ffprobe";
}

/// <summary>Probe stub that always reports a successful fixed duration.</summary>
internal sealed class FixedDurationProbe : IMediaDurationProbe
{
    private readonly TimeSpan _duration;

    public FixedDurationProbe(TimeSpan duration) => _duration = duration;

    public Task<MediaDurationProbeResult> ProbeFileAsync(string filePath, CancellationToken cancellationToken)
        => Task.FromResult(MediaDurationProbeResult.Success(_duration));

    public Task<MediaDurationProbeResult> ProbeUriAsync(string uri, CancellationToken cancellationToken)
        => Task.FromResult(MediaDurationProbeResult.Success(_duration));

    public Task<MediaDurationProbeResult> ProbeStreamAsync(Stream input, CancellationToken cancellationToken)
        => Task.FromResult(MediaDurationProbeResult.Success(_duration));
}

/// <summary>
/// Reader stub that serves no directory entries. <c>openFileStream</c> optionally
/// supplies a stream factory for remote items — a factory rather than a stream instance so
/// every call gets a fresh, unconsumed stream.
/// </summary>
internal sealed class StubMediaSourceReader : IMediaSourceReader
{
    private readonly Func<Stream?>? _openFileStream;

    public StubMediaSourceReader(Func<Stream?>? openFileStream = null) => _openFileStream = openFileStream;

    public IEnumerable<MediaEntry> ReadRootDirectory(MediaSource source) => Enumerable.Empty<MediaEntry>();
    public IEnumerable<MediaEntry> ReadDirectoryEntries(MediaCollection collection) => Enumerable.Empty<MediaEntry>();
    public Task<bool> FileExistsAsync(MediaCollection collection, string fileName) => Task.FromResult(false);
    public Task<string?> ReadFileAsync(MediaCollection collection, string fileName) => Task.FromResult<string?>(null);
    public Task<Stream?> ReadFileStreamAsync(MediaCollection collection, string fileName) => Task.FromResult<Stream?>(null);
    public Stream? OpenFileStream(MediaCollection collection, string fileName) => _openFileStream?.Invoke();
}
