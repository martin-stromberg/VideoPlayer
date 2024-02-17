using System;
using System.Linq;

namespace Mediathek.Services.MediaLibrary.PlaybackHistory
{
    public interface IPlaybackHistoryManager
    {

        bool IsInitialized { get; }

        History CurrentHistory { get; }

        Task Add(MediaItem item, BaseModel typedItem, Playlist playlist);

        Task Remove(BaseModel item);

        Task Finish(MediaItem item, BaseModel typedItem);

        Task InitializeAsync();

    }
}
