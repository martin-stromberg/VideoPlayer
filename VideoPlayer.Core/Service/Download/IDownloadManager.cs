using VideoPlayer.Service.BaseServices;
using VideoPlayer.Service.Library.Models.Classified;
using static VideoPlayer.Service.Download.DownloadManager;

namespace VideoPlayer.Service.Download
{
    public interface IDownloadManager: ITimerService
    {
        void ClearTempFolder();
        DownloadSession Enqueue(ClassifiedEntry entry, Library.Models.MediaItem item);
        void RemoveDownloads(ClassifiedEntry entry);
    }
}
