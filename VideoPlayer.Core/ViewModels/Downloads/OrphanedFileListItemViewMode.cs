namespace VideoPlayer.ViewModels.Downloads
{
    public class OrphanedFileListItemViewMode : FileListItemViewModel, IDownloadListItem
    {
        public OrphanedFileListItemViewMode(FileInfo file) 
            : base(file)
        {
            
        }
    }
}
