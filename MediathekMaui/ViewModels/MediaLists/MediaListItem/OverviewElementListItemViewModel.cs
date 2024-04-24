using Mediathek.Models.Overview;
using Mediathek.Navigation;
using Mediathek.Services.MediaLibrary;
using Mediathek.Services.MediaLibrary.Downloads;
using Mediathek.Services.Settings;
using Mediathek.StatusManagement;

namespace Mediathek.ViewModels.MediaLists.MediaListItem
{
    public class OverviewElementListItemViewModel: BaseMediaListItemViewModel
    {

        public OverviewElementListItemViewModel(
            OverviewElement mediaItem,
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            ISettingsService settingsService,
            IDownloadManager downloadManager,
            IMediaLibrary mediaLibrary)
            : base(mediaItem, statusPublisher, navigationManager, settingsService, downloadManager, mediaLibrary) { }

        protected OverviewElement Element
        {
            get
            {
                return Item as OverviewElement;
            }
        }

        public override void OpenCategory()
        {
            OpenDetails();
        }

        public override async void OpenDetails()
        {
            if (!IsStored)
                return;
            switch (Element.Type)
            {
                case nameof(TVShow):
                    var show = await MediaLibrary.GetTVShow(Element.OriginalId);
                    NavigationManager.OpenTVShow(show, null, null);
                    break;
                case nameof(TVShowCollection):
                    var collection = await MediaLibrary.GetTVShowCollection(Element.OriginalId);
                    NavigationManager.OpenTVShowCollection(collection);
                    break;
                case nameof(Movie):
                    var movie = await MediaLibrary.GetMovie(Element.OriginalId);
                    NavigationManager.OpenMovie(movie, null);
                    break;
                case nameof(MovieCollection):
                    var movieCcollection = await MediaLibrary.GetMovieCollection(Element.OriginalId);
                    NavigationManager.OpenMovieCollection(movieCcollection);
                    break;
            }
        }

        protected override bool CanStartPlayback()
        {
            throw new NotImplementedException();
        }

        protected override async void ExecuteSaveNewItem()
        {
            try
            {
                if (Item.Id != 0)
                    throw new ArgumentException(nameof(Item.Id));
                if (string.IsNullOrWhiteSpace(Title))
                    throw new ArgumentNullException(nameof(Title));

                // var existing = await MediaLibrary.FindTVShowCollectionByNameAsync(Title);
                // if ((existing is not null) && existing.Any())
                // throw new ApplicationException($"Collection already exists.");
                // Item.Name = Title;
                // await MediaLibrary.AddTVShowCollectionAsync(Collection);
                // Item = Collection;
                throw new NotImplementedException(string.Empty);
            }
            catch (Exception ex) { }
        }

        protected override void ExecuteCancelNewItem()
        {
            Item = null;
        }

        protected override void ExecuteStartPlayback()
        {
            throw new NotImplementedException();
        }

    }
}
