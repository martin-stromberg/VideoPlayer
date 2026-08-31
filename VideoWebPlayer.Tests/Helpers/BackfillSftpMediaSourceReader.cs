using System.IO;
using System.Text;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;

namespace VideoWebPlayer.Tests.Helpers;

public sealed class BackfillSftpMediaSourceReader : SftpMediaSourceReader
{
    private readonly Dictionary<(string CollectionPath, string FileName), string> _files;

    public BackfillSftpMediaSourceReader(Dictionary<(string, string), string> files)
    {
        _files = files;
    }

    public override IEnumerable<MediaEntry> ReadRootDirectory(MediaSource source)
    {
        return [];
    }

    public override IEnumerable<MediaEntry> ReadDirectoryEntries(MediaCollection collection)
    {
        return [];
    }

    public override Task<bool> FileExistsAsync(MediaCollection collection, string fileName)
    {
        return Task.FromResult(_files.ContainsKey((collection?.Path ?? string.Empty, fileName)));
    }

    public override Task<string?> ReadFileAsync(MediaCollection collection, string fileName)
    {
        _files.TryGetValue((collection?.Path ?? string.Empty, fileName), out var content);
        return Task.FromResult<string?>(content);
    }

    public override Task<Stream?> ReadFileStreamAsync(MediaCollection collection, string fileName)
    {
        if (_files.TryGetValue((collection?.Path ?? string.Empty, fileName), out var content))
        {
            return Task.FromResult<Stream?>(new MemoryStream(Encoding.UTF8.GetBytes(content)));
        }

        return Task.FromResult<Stream?>(null);
    }
}
