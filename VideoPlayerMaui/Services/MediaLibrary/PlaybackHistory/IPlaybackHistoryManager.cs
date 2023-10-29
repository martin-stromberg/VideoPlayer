using System;
using System.Linq;
using VideoPlayer.Models;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Models.PlaybackHistory;

namespace VideoPlayer.Services.MediaLibrary.PlaybackHistory
{
    public interface IPlaybackHistoryManager
    {

        History CurrentHistory { get; }

        Task Add(MediaItem item, BaseModel typedItem);
        Task Finish(MediaItem item, BaseModel typedItem);
    }
}
