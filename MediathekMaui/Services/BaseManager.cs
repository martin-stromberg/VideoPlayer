using Mediathek.Services.MediaLibrary;
using System;
using System.Linq;

namespace Mediathek.Services
{
    public class BaseManager
    {

        public BaseManager(IMediaLibrary mediaLibrary)
        {
            MediaLibrary = mediaLibrary;
            MediaLibrary.ModelElementAdded += MediaLibrary_ModelElementAdded;
            MediaLibrary.ModelElementRemoved += MediaLibrary_ModelElementRemoved;
            MediaLibrary.ModelElementUpdated += MediaLibrary_ModelElementUpdated;
        }

        private void MediaLibrary_ModelElementUpdated(object sender, BaseModelEventArgs e)
        {
            if (e.Element is TVShow)
                ProcessTVShowUpdated(e.Element as TVShow);
        }

        private void MediaLibrary_ModelElementRemoved(object sender, BaseModelEventArgs e)
        {
            if (e.Element is TVShow)
                ProcessTVShowRemoved(e.Element as TVShow);
        }

        private void MediaLibrary_ModelElementAdded(object sender, BaseModelEventArgs e)
        {
            if (e.Element is TVShow)
                ProcessTVShowAdded(e.Element as TVShow);
        }

        protected virtual void ProcessTVShowAdded(TVShow show) { }

        protected virtual void ProcessTVShowUpdated(TVShow show) { }

        protected virtual void ProcessTVShowRemoved(TVShow show) { }

        protected IMediaLibrary MediaLibrary { get; }

    }
}
