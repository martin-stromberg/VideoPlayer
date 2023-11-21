using System;
using System.Linq;
using VideoPlayer.Models;
using VideoPlayer.Models.MediaItems;

namespace VideoPlayer.Services.MediaLibrary.Downloads
{
    public  interface IMediaDownloader
    {

        Task<MediaItem> CacheAsync(MediaItem item);

        void StartDownload(BaseModel item);

        Task ContinueDownloadsAsync();

        void RemoveDownload(BaseModel item);

        event EventHandler<BaseModelEventArgs> Downloaded;

        event EventHandler<BaseModelEventArgs> DownloadDeleted;

    }
}
