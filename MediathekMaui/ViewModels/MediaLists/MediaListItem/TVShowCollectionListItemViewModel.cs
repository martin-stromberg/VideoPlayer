using Mediathek.Navigation;
using Mediathek.Services.MediaLibrary;
using Mediathek.Services.MediaLibrary.Downloads;
using Mediathek.Services.Settings;
using Mediathek.StatusManagement;
using Microsoft.VisualBasic;

namespace Mediathek.ViewModels.MediaLists.MediaListItem
{

    public class TVShowCollectionListItemViewModel: BaseMediaListItemViewModel
    {

        public TVShowCollectionListItemViewModel(
            TVShowCollection collection,
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            ISettingsService settingsService,
            IDownloadManager downloadManager,
            IMediaLibrary mediaLibrary)
            : base(collection, statusPublisher, navigationManager, settingsService, downloadManager, mediaLibrary) { }

        protected TVShowCollection Collection
        {
            get
            {
                return Item as TVShowCollection;
            }
        }

        public override void OpenCategory()
        {
            OpenDetails();
        }

        public override void OpenDetails()
        {
            if (!IsStored)
                return;
            NavigationManager.OpenTVShowCollection(Item as TVShowCollection);
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
                var existing = await MediaLibrary.FindTVShowCollectionByNameAsync(Title);
                if ((existing is not null) && existing.Any())
                    throw new ApplicationException($"Collection already exists.");
                Item.Name = Title;
                await MediaLibrary.AddTVShowCollectionAsync(Collection);
                Item = Collection;
            }
            catch { }
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
