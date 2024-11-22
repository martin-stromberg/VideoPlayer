using System;
using System.Linq;
using VideoPlayer.Service.Library.Models;

namespace VideoPlayer.Service.Library.SourceReader
{

    public interface ISourceReader
    {

        SourceFolder GetRoot();

        Task<IEnumerable<SourceFile>> ReadFilesAsync(SourceFolder folder);

        Task<IEnumerable<SourceFolder>> ReadFoldersAsync(SourceFolder folder);

        Task<SourceFile> ReadFileAsync(MediaItem mediaItem);
        FileInfo Download(MediaItem nfoFile, Action<decimal> progressCallback);

        string ReadTextFile(MediaItem nfoFile);

    }
}
