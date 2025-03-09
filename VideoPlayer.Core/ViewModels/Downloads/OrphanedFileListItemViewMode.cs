using Microsoft.Extensions.Logging;

namespace VideoPlayer.ViewModels.Downloads
{
    public class OrphanedFileListItemViewMode : FileListItemViewModel, IDownloadListItem
    {
        public OrphanedFileListItemViewMode(FileInfo file, ILogger logger) 
            : base(file, logger)
        {
            
        }
    }
}
