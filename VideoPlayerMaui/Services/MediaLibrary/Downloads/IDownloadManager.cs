using VideoPlayer.Models;
using VideoPlayer.Models.MediaItems;

namespace VideoPlayer.Services.MediaLibrary.Downloads
{
    public interface IDownloadManager
    {
        void ContinueDownloads();
        void RemoveAllDownloads();
        void RemoveDownload(BaseModel item);
        Task<DownloadSession> StartDownloadAsync(MediaItem item, MediaItemCopyType cache);
        Task<IEnumerable<DownloadSession>> StartDownloadAsync(BaseModel item, MediaItemCopyType cache);
    }
}
