using VideoWebPlayer.Data;
using VideoWebPlayer.Services;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Media source reader whose file cannot be opened: <see cref="OpenFileStream"/> fails the way the file
/// system does when the service account may not read the file. The other members are not used by the
/// tests that need it.
/// </summary>
internal sealed class UnreadableFileReader : IMediaSourceReader
{
    /// <inheritdoc />
    public Stream? OpenFileStream(MediaCollection collection, string fileName)
        => throw new UnauthorizedAccessException($"Kein Lesezugriff auf '{fileName}'.");

    /// <inheritdoc />
    public IEnumerable<MediaEntry> ReadRootDirectory(MediaSource source) => [];

    /// <inheritdoc />
    public IEnumerable<MediaEntry> ReadDirectoryEntries(MediaCollection collection) => [];

    /// <inheritdoc />
    public Task<bool> FileExistsAsync(MediaCollection collection, string fileName) => Task.FromResult(false);

    /// <inheritdoc />
    public Task<string?> ReadFileAsync(MediaCollection collection, string fileName) => Task.FromResult<string?>(null);

    /// <inheritdoc />
    public Task<Stream?> ReadFileStreamAsync(MediaCollection collection, string fileName) => Task.FromResult<Stream?>(null);
}
