using Mediathek.Navigation;
using Mediathek.Services.MediaLibrary;
using Mediathek.Services.MediaLibrary.Downloads;
using Mediathek.Services.MediaLibrary.Scanner;
using Mediathek.Services.Settings;
using Mediathek.StatusManagement;
using System;
using System.Linq;

namespace Mediathek.ViewModels.MediaLists.MediaListItem
{
    public class MediaListItemViewModel: BaseMediaListItemViewModel
    {

        private readonly ILibraryScanner _LibraryScanner;

        public MediaListItemViewModel(
            BaseModel mediaItem,
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            ISettingsService settingsService,
            ILibraryScanner libraryScanner,
            IDownloadManager downloadManager,
            IMediaLibrary mediaLibrary)
            : base(mediaItem, statusPublisher, navigationManager, settingsService, downloadManager, mediaLibrary)
        {
            _LibraryScanner = libraryScanner;
            ProcessItem = new Command(() => ExecuteProcessItem(), () => CanProcessItem());
        }

        public Command ProcessItem { get; set; }

        protected override void UpdateProperties()
        {
            base.UpdateProperties();
            ProcessItem?.ChangeCanExecute();
        }

        public override async void OpenDetails()
        {
            await NavigationManager.OpenMediaItemDetailsAsync(Item as MediaItem);
        }

        public override async void OpenCategory()
        {
            await NavigationManager.OpenMediaItemDetailsAsync(Item as MediaItem);
        }

        protected override bool CanStartPlayback()
        {
            return false;
        }

        protected override void ExecuteStartPlayback()
        {
            OpenDetails();
        }

        private bool CanProcessItem()
        {
            return (Item != null) && (Item is MediaItem);
        }

        private void ExecuteProcessItem()
        {
            _LibraryScanner.Rescan(Item as MediaItem);
        }

    }
}
