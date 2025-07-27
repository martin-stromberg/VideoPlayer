using System;
using System.Linq;

namespace WebPlayerApi.Service.Data
{

    public interface ISourceReader
    {

        SourceFolder GetRoot();

        IEnumerable<SourceFile> ReadFiles(SourceFolder folder);

        IEnumerable<SourceFolder> ReadFolders(SourceFolder folder);
        /*
        SourceFile ReadFile(MediaItem mediaItem);
        FileInfo Download(MediaItem nfoFile, Action<decimal> progressCallback);

        string ReadTextFile(MediaItem nfoFile);
        void Upload(string sourceFilePath, string destFilePath, Action<decimal> progressCallback);
        */
    }
}
