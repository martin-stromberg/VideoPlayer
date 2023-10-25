using System;
using System.Linq;
using VideoPlayer.Models.MediaItems;

namespace VideoPlayer.Services.MediaLibrary.Downloads
{
    public  interface IMediaDownloader
    {

        Task<MediaItem> CacheAsync(MediaItem item);

    }
}
