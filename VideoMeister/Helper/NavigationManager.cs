using CommunityToolkit.Maui.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoMeister.Services.VideoSources;
using VideoMeister.ViewModels.Navigation;

namespace VideoMeister.Helper
{
    public class NavigationManager
    {
        private readonly VideoSourceManager videoSourceManager;

        public NavigationManager(VideoSourceManager videoSourceManager) 
        {
            this.videoSourceManager = videoSourceManager;
            this.videoSourceManager.Sources.CollectionChanged += Sources_CollectionChanged;
        }

        private void Sources_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (var source in e.OldItems)
                    RemoveSourceToSourceView(source);
            if (e.NewItems != null)
                foreach (var source in e.NewItems)
                    AddSourceToSourceView(source);
        }
        private void RemoveSourceToSourceView(object source)
        {
            if (currentView as SourcesViewModel == null)
                return;
            (currentView as SourcesViewModel).RemoveSource(source as VideoSource);
        }
        private void AddSourceToSourceView(object source)
        {
            if (currentView == null)
                NavigateToSourceOverview();
            (currentView as SourcesViewModel).AddSource(source as VideoSource);
        }

        

        public event EventHandler<NavigationEventArgs> NavigationCompleted;
        protected void OnNavigationCompleted(NavigationEventArgs e)
        {
            NavigationCompleted?.Invoke(this, e);
        }
        protected void OnNavigationCompleted(NavigationContentViewModel contentViewModel)
        {
            OnNavigationCompleted(new NavigationEventArgs(contentViewModel));
        }


        private NavigationContentViewModel currentView = null;
        private Stack<NavigationContentViewModel> viewStack = new Stack<NavigationContentViewModel> { };
        private void NavigateToSourceOverview()
        {
            var newView = new SourcesViewModel()
            {
                Title = "Quellen"
            };
            NavigateTo(newView);
        }
        private void NavigateTo(NavigationContentViewModel viewModel)
        {
            viewModel.ItemTapped += ViewModel_ItemTapped;

            viewStack.Push(viewModel);
            currentView = viewModel;
            OnNavigationCompleted(viewModel);
        }

        private void StartPayback(MediaSource videoSource)
        {
            OnPlaybackRequest(videoSource);
        }
        private void OnPlaybackRequest(MediaSource videoSource)
        {
            OnPlaybackRequest(new MediaSourceEventArgs(videoSource));
        }
        private void OnPlaybackRequest(MediaSourceEventArgs mediaSourceEventArgs)
        {
            PlaybackRequest?.Invoke(this, mediaSourceEventArgs);
        }
        public event EventHandler<MediaSourceEventArgs> PlaybackRequest;

        private void ViewModel_ItemTapped(object sender, MediaElementBoxViewModelEventArgs e)
        {
            if (e.ViewModel is SourceBoxViewModel)
                NavigateToSource(e.ViewModel as SourceBoxViewModel);
            else if (e.ViewModel is MediaItemViewModel)
                NavigateToMediaItem(e.ViewModel as MediaItemViewModel);
        }

        private void NavigateToMediaItem(MediaItemViewModel viewModel)
        {
            var videoSource = videoSourceManager.CreateVideoSource(viewModel.Item);
            StartPayback(videoSource);
        }

        

        private async void NavigateToSource(SourceBoxViewModel viewModel)
        {
            var items = await videoSourceManager.GetMediaItemsAsync(viewModel.Source);
            var vm = new MediaCollectionViewModel()
            {
                Title = viewModel.Name
            };            
            NavigateTo(vm);
            await Task.Run(() => { vm.AddItems(items); });
        }
    }
}
