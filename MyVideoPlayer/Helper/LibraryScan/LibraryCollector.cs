using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayerLib.Services.MediaLibrary;
using VideoPlayerLib.Services.MediaLibrary.Models;
using VideoPlayerLib.Services.MediaLibrary.Models.Meta;

namespace MyVideoPlayer.Helper.LibraryScan
{
    public interface ILibraryCollector
    {

    }
    public class LibraryCollector : ILibraryCollector
    {
        private readonly IMediaLibrary mediaLibrary;

        public LibraryCollector(IMediaLibrary mediaLibrary)
            :base()
        {
            this.mediaLibrary = mediaLibrary;
            this.mediaLibrary.ModelElementAdded += MediaLibrary_ModelElementAdded;
            this.mediaLibrary.ModelElementUpdated += MediaLibrary_ModelElementUpdated;
        }

        private void MediaLibrary_ModelElementUpdated(object sender, VideoPlayerLib.Services.MediaLibrary.Models.BaseModelEventArgs e)
        {
            CollectMediaItem(e.Element as MediaItem);
        }
        private void MediaLibrary_ModelElementAdded(object sender, VideoPlayerLib.Services.MediaLibrary.Models.BaseModelEventArgs e)
        {
            CollectMediaItem(e.Element as MediaItem);
        }
        private void CollectMediaItem(MediaItem mediaItem)
        {
            if (mediaItem == null)
                return;
            if (mediaItem.MetaInfo is MovieInformation)
                CollectMovie(mediaItem, mediaItem.MetaInfo as MovieInformation);
        }

        private void CollectMovie(MediaItem mediaItem, MovieInformation movieInformation)
        {
            throw new NotImplementedException();
        }
    }
}
