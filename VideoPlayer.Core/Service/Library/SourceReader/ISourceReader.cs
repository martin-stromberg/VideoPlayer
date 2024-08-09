using System;
using System.Linq;

namespace VideoPlayer.Service.Library.SourceReader
{
    public interface ISourceReader
    {

        SourceFolder GetRoot();

        Task<IEnumerable<SourceFile>> ReadFilesAsync(SourceFolder folder);

        Task<IEnumerable<SourceFolder>> ReadFoldersAsync(SourceFolder folder);

    }
}
