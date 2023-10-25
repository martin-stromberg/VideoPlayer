using System;
using System.Collections.ObjectModel;
using System.Linq;
using VideoPlayer.Navigation;
using VideoPlayer.Services.MediaLibrary;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.MediaLists
{
    public class BaseMediaListViewModel: BaseViewModel
    {

        public BaseMediaListViewModel(
            IStatusPublisher statusPublisher,
            INavigationManager navigationManager,
            IMediaLibrary mediaLibrary)
            : base(statusPublisher, navigationManager)
        {
            MediaLibrary = mediaLibrary;
        }

        protected IMediaLibrary MediaLibrary { get; }

        public ObservableCollection<MediaListItemViewModel> Items { get; } = new ObservableCollection<MediaListItemViewModel>();

    }
}
