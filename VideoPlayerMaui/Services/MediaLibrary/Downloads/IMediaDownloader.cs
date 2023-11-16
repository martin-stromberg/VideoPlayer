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

    }
}
