using System.IO;
using Renci.SshNet.Common;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Reader that simulates a remote directory which no longer exists for a configured path.
/// </summary>
public sealed class ThrowingSftpMediaSourceReader : SftpMediaSourceReader
{
    private readonly string _missingPath;

    public ThrowingSftpMediaSourceReader(string missingPath)
    {
        _missingPath = missingPath;
    }

    public override IEnumerable<MediaEntry> ReadRootDirectory(MediaSource source)
    {
        yield break;
    }

    public override IEnumerable<MediaEntry> ReadDirectoryEntries(MediaCollection collection)
    {
        if (collection.Path == _missingPath)
            throw new SftpPathNotFoundException($"No such file. Path: '{collection.Path}'.");

        yield break;
    }

    public override Task<bool> FileExistsAsync(MediaCollection collection, string fileName)
    {
        return Task.FromResult(false);
    }

    public override Task<string?> ReadFileAsync(MediaCollection collection, string fileName)
    {
        return Task.FromResult<string?>(null);
    }

    public override Task<Stream?> ReadFileStreamAsync(MediaCollection collection, string fileName)
    {
        return Task.FromResult<Stream?>(null);
    }
}
