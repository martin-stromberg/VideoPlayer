using VideoPlayer.Service.BaseServices;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using static VideoPlayer.Service.Download.DownloadManager;

namespace VideoPlayer.Service.Download
{
    public interface IDownloadManager: ITimerService
    {
        void ClearTempFolder();
        DownloadSession Enqueue(ClassifiedEntry entry, Library.Models.MediaItem item, TimeSpan dueTime);
        DownloadSession Enqueue(ClassifiedEntry entry, MediaItemCopyType copyType, TimeSpan dueTime);
        IEnumerable<FileInfo> GetOrphanedFiles();
        void PrepareWatchedMediaItem(ClassifiedEntry entry, MediaItem item);
        void RemoveDownloads(ClassifiedEntry entry);
    }
}
