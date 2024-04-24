using Mediathek.Models.Overview;
using System;
using System.Linq;

namespace Mediathek.Services.MediaLibrary.OverviewPreparation
{
    public class OverviewManager: IOverviewManager
    {

        private readonly IMediaLibrary _MediaLibrary;
        private static string[] ExpectedTypeNames =
        {
            typeof(Movie).Name,
            typeof(MovieCollection).Name,
            typeof(TVShow).Name,
            typeof(TVShowCollection).Name
        };

        public OverviewManager(IMediaLibrary mediaLibrary)
        {
            _MediaLibrary = mediaLibrary;
            mediaLibrary.ModelElementAdded += MediaLibrary_ModelElementAdded;
            mediaLibrary.ModelElementRemoved += MediaLibrary_ModelElementRemoved;
            mediaLibrary.ModelElementUpdated += MediaLibrary_ModelElementUpdated;
        }

        #region Media library events
        private async void MediaLibrary_ModelElementUpdated(object sender, BaseModelEventArgs e)
        {
            await UpdateElement(e.Element);
        }

        private async void MediaLibrary_ModelElementRemoved(object sender, BaseModelEventArgs e)
        {
            await UpdateElement(e.Element);
        }

        private async void MediaLibrary_ModelElementAdded(object sender, BaseModelEventArgs e)
        {
            await UpdateElement(e.Element);
        }
        #endregion

        private async Task UpdateElement(BaseModel elem, bool forceUpdate = false)
        {
            var typeName = elem.GetType().Name;
            if (!ExpectedTypeNames.Contains(typeName))
                return;
            var element = await _MediaLibrary.GetOverviewElementByOriginalId(typeName, elem.Id);
            if (element is null)
                element = new OverviewElement() { Id = 0, Name = elem.Name, Type = typeName, OriginalId = elem.Id };
            var changed = false;
            if (elem is TVShow)
            {
                changed = element.Update(elem as TVShow);
                element.Delete = IsInCollection(elem as TVShow);
            }
            if (elem is Movie)
            {
                changed = element.Update(elem as Movie);
                element.Delete = IsInCollection(elem as Movie);
            }
            if (elem is TVShowCollection)
                changed = element.Update(elem as TVShowCollection, (await _MediaLibrary.GetTVShows(elem.Id)).ToArray());
            if (elem is MovieCollection)
                changed = element.Update(elem as MovieCollection, (await _MediaLibrary.GetMovies(elem.Id)).ToArray());

            changed = changed || forceUpdate;
            if (element.Delete)
            {
                if (element.Id != 0)
                    await _MediaLibrary.RemoveOverviewElement(element);
            }
            else if (changed || (element.Id == 0))
            {
                element.LastUpdate = DateTime.Now;
                await _MediaLibrary.AddOverviewElement(element);
            }
        }

        private bool IsInCollection(TVShow show)
        {
            return show.CollectionId != 0;
        }

        private bool IsInCollection(Movie movie)
        {
            return movie.CollectionId != 0;
        }

        public async Task RecreateData()
        {
            DateTime updateStartTime = DateTime.Now;
            foreach (var show in await _MediaLibrary.GetTVShows())
                await UpdateElement(show, true);
            foreach (var showCollection in await _MediaLibrary.GetTVShowCollections())
                await UpdateElement(showCollection, true);
            foreach (var movie in await _MediaLibrary.GetMovies())
                await UpdateElement(movie, true);
            foreach (var movieCollection in await _MediaLibrary.GetMovieCollections())
                await UpdateElement(movieCollection, true);

            foreach (var elem in (await _MediaLibrary.GetAllOverviewElements())
                .Where(elem => elem.LastUpdate < updateStartTime))
                await _MediaLibrary.RemoveOverviewElement(elem);
        }

    }
}
