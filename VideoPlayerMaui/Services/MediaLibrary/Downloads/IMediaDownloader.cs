using System;
using System.Linq;
using VideoPlayer.Models;
using VideoPlayer.Models.MediaItems;

namespace VideoPlayer.Services.MediaLibrary.Downloads
{
    public  interface IMediaDownloader
    {

        Task<MediaItem> CacheAsync(MediaItem item);

        Task<IEnumerable<DownloadSession>> StartDownload(BaseModel item);

        Task ContinueDownloadsAsync();

        void RemoveDownload(BaseModel item);
        void RemoveAllDownloads();

        event EventHandler<BaseModelEventArgs> Downloaded;

        event EventHandler<BaseModelEventArgs> DownloadDeleted;

    }
}
