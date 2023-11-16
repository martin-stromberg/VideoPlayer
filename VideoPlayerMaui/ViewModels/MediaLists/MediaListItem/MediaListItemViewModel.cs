using System;
using System.Linq;
using VideoPlayer.Models;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Navigation;
using VideoPlayer.Services.MediaLibrary.Downloads;
using VideoPlayer.Services.MediaLibrary.Scanner;
using VideoPlayer.Services.Settings;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.MediaLists.MediaListItem
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
            IMediaDownloader mediaDownloader)
            : base(mediaItem, statusPublisher, navigationManager, settingsService, mediaDownloader)
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
