using MyVideoPlayer.ViewModels.Logs;
using MyVideoPlayer.ViewModels.Navigation;
using MyVideoPlayer.ViewModels.Navigation.Library;
using MyVideoPlayer.ViewModels.Navigation.MediaCollection;
using MyVideoPlayer.ViewModels.Navigation.Sources;
using VideoPlayerLib.Services.MediaLibrary;
using VideoPlayerLib.Services.MediaLibrary.Models;

namespace MyVideoPlayer.Helper.Navigation
{
    public class RessourceLocation: IRessourceLocation
    {
        public string Path { get; set; }
    }

    public interface IRessourceLocation
    {
        string Path { get; }
    }

    public class NavigationManager: INavigationManager
    {
        private readonly IMediaLibrary mediaLibrary;
        private readonly IServiceProvider serviceProvider;
        private readonly IRessourceLocation ressourceLocation;

        public NavigationManager(IServiceProvider serviceProvider)
        {
            this.mediaLibrary = serviceProvider.GetService<IMediaLibrary>();
            this.serviceProvider = serviceProvider;
            this.ressourceLocation = serviceProvider.GetService<IRessourceLocation>();
        }
        private NavigationContentViewModel currentView = null;
        private Stack<NavigationContentViewModel> viewStack = new Stack<NavigationContentViewModel> { };
        private void NavigateTo(NavigationContentViewModel viewModel)
        {
            viewModel.ItemTapped += ViewModel_ItemTapped;

            viewStack.Push(viewModel);
            currentView = viewModel;
            OnNavigationCompleted(viewModel);
        }

        public void NavigateToRoot()
        {
            while (viewStack.Count() > 1)
                NavigateBack();
        }
        public void NavigateBack()
        {
            var currentViewModel = viewStack.Pop();
            if (viewStack.Count == 0)     
                viewStack.Push(currentViewModel);
            else
            {                
                currentViewModel.ItemTapped -= ViewModel_ItemTapped;
                currentViewModel.OnDisappeared();
                currentView = viewStack.Peek();
                OnNavigationCompleted(currentView);
            }
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

        private void ViewModel_ItemTapped(object sender, MediaElementBoxViewModelEventArgs e)
        {
            if (e.ViewModel is SourceBoxViewModel)
                NavigateToSource(e.ViewModel as SourceBoxViewModel);
            else if (e.ViewModel is MediaCollectionBoxViewModel)
                NavigateToMediaItem(e.ViewModel as MediaCollectionBoxViewModel);
            else if (e.ViewModel is MediaItemBoxViewModel)
                NavigateToMediaItem(e.ViewModel as MediaItemBoxViewModel);
            else if (e.ViewModel is CategoryBoxViewModel)
                NavigateToCategory(e.ViewModel as CategoryBoxViewModel);
            else if (e.ViewModel is MovieBoxViewModel)
                NavigateToMediaItem(e.ViewModel as MovieBoxViewModel);
            else if (e.ViewModel is TVShowBoxViewModel)
                NavigateToMediaItem(e.ViewModel as TVShowBoxViewModel);
            else if (e.ViewModel is TVShowSeasonBoxViewModel)
                NavigateToMediaItem(e.ViewModel as TVShowSeasonBoxViewModel);
            else if (e.ViewModel is TVShowEpisodeBoxViewModel)
                NavigateToMediaItem(e.ViewModel as TVShowEpisodeBoxViewModel);
        }

        private async void NavigateToMediaItem(MediaItemBoxViewModel viewModel)
        {
            var mediaItem = viewModel.Item;
            if (viewModel.Item.CopyType == MediaItemCopyType.None)
            {
                var cachedItem = (await mediaLibrary.GetAlternateMediaItemsAsync(viewModel.Item.Id))
                    .FirstOrDefault(item => item.CopyType == MediaItemCopyType.Cache);
                if (cachedItem != null)
                    mediaItem = cachedItem;
            }

            if (mediaItem.CopyType == MediaItemCopyType.None && viewModel.Source.MustCache(mediaItem))
            {
                NavigateToCachedItem(viewModel);
                return;
            }

            var path = viewModel.Source.GetItemPath(mediaItem);
            //var mediaSource = CommunityToolkit.Maui.Views.MediaSource.FromUri("https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4");
            var mediaSource = CommunityToolkit.Maui.Views.MediaSource.FromFile(path);
            OnMediaSourceToPlay(mediaSource);
        }

        private async void NavigateToMediaItem(TVShowBoxViewModel viewModel)
        {
            var movie = await mediaLibrary.GetTVShow(viewModel.Item.Id);

            var vm = serviceProvider.GetService<LibraryMediaCollectionViewModel>();
            vm.Title = viewModel.Title;
            vm.Parent = movie;
            vm.CategoryType = movie.GetType();
            NavigateTo(vm);
        }

        private async void NavigateToMediaItem(TVShowSeasonBoxViewModel viewModel)
        {
            var season = await mediaLibrary.GetTVShowSeason(viewModel.Item.Id);

            var vm = serviceProvider.GetService<LibraryMediaCollectionViewModel>();
            vm.Title = viewModel.Title;
            vm.Parent = season;
            vm.CategoryType = season.GetType();
            NavigateTo(vm);
        }

        private async void NavigateToMediaItem(TVShowEpisodeBoxViewModel viewModel)
        {
            var episode = await mediaLibrary.GetTVShowEpisode(viewModel.Item.Id);
            if (viewModel.MediaItem != null && viewModel.MediaItem.CopyType == MediaItemCopyType.Cache && !File.Exists(viewModel.MediaItem.Path))
            {
                viewModel.MediaItem = null;
                viewModel.Collection = null;
                viewModel.Source = null;
            }
            if (viewModel.MediaItem == null)
                viewModel.MediaItem = await mediaLibrary.GetMediaItemAsync(episode.MediaItems.FirstOrDefault());
            if (viewModel.Collection == null)
                viewModel.Collection = await mediaLibrary.GetMediaItemCollectionAsync(viewModel.MediaItem.ParentCollectionId);
            if (viewModel.Source == null)
                viewModel.Source = await mediaLibrary.GetSourceAsync(viewModel.Collection.MediaSourceId);

            if (viewModel.MediaItem.CopyType == MediaItemCopyType.None)
            {
                var cachedItem = (await mediaLibrary.GetAlternateMediaItemsAsync(viewModel.Item.Id))
                    .FirstOrDefault(item => item.CopyType == MediaItemCopyType.Cache);
                if (cachedItem != null)
                    viewModel.MediaItem = cachedItem;
            }

            if (viewModel.MediaItem.CopyType == MediaItemCopyType.None && viewModel.Source.MustCache(viewModel.MediaItem))
            {
                NavigateToCachedItem(viewModel);
                return;
            }

            var path = viewModel.Source.GetItemPath(viewModel.MediaItem);
            var mediaSource = CommunityToolkit.Maui.Views.MediaSource.FromFile(path);
            OnMediaSourceToPlay(mediaSource);
        }
        private void NavigateToCachedItem(TVShowEpisodeBoxViewModel viewModel)
        {
            StartPlayLoadingVideo();
            var e = new CallbackBaseModelEventArgs(viewModel.MediaItem);
            e.Callback += (sender, e) => {
                viewModel.MediaItem = e.Element as MediaItem;
                playingMediaItem = viewModel.MediaItem;
                var mediaSource = CommunityToolkit.Maui.Views.MediaSource.FromFile(viewModel.MediaItem.Path);
                OnMediaSourceToPlay(mediaSource);
            };
            DownloadRequested.Invoke(this, e);
        }

        private async void NavigateToMediaItem(MovieBoxViewModel viewModel)
        {
            var movie = await mediaLibrary.GetMovie(viewModel.Item.Id);

            if (viewModel.MediaItem != null && viewModel.MediaItem.CopyType == MediaItemCopyType.Cache && !File.Exists(viewModel.MediaItem.Path))
            {
                viewModel.MediaItem = null;
                viewModel.Collection = null;
                viewModel.Source = null;
            }

            if (viewModel.MediaItem == null)
                viewModel.MediaItem = await mediaLibrary.GetMediaItemAsync(movie.MediaItems.FirstOrDefault());
            if (viewModel.Collection == null)
                viewModel.Collection = await mediaLibrary.GetMediaItemCollectionAsync(viewModel.MediaItem.ParentCollectionId);
            if (viewModel.Source == null)
                viewModel.Source = await mediaLibrary.GetSourceAsync(viewModel.Collection.MediaSourceId);


            if (viewModel.MediaItem.CopyType == MediaItemCopyType.None)
            {
                var cachedItem = (await mediaLibrary.GetAlternateMediaItemsAsync(viewModel.Item.Id))
                    .FirstOrDefault(item => item.CopyType == MediaItemCopyType.Cache);
                if (cachedItem != null)
                    viewModel.MediaItem = cachedItem;
            }

            if (viewModel.MediaItem.CopyType == MediaItemCopyType.None && viewModel.Source.MustCache(viewModel.MediaItem))
            {
                NavigateToCachedItem(viewModel);
                return;
            }

            var path = viewModel.Source.GetItemPath(viewModel.MediaItem);
            var mediaSource = CommunityToolkit.Maui.Views.MediaSource.FromFile(path);
            OnMediaSourceToPlay(mediaSource);
        }
        private void NavigateToCachedItem(MovieBoxViewModel viewModel)
        {
            StartPlayLoadingVideo();
            var e = new CallbackBaseModelEventArgs(viewModel.MediaItem);
            e.Callback += (sender, e) => {
                viewModel.MediaItem = e.Element as MediaItem;
                playingMediaItem = viewModel.MediaItem;
                var mediaSource = CommunityToolkit.Maui.Views.MediaSource.FromFile(viewModel.MediaItem.Path);
                OnMediaSourceToPlay(mediaSource);
            };
            DownloadRequested.Invoke(this, e);
        }

        private void StartPlayLoadingVideo()
        {
            var path = findLocalFile("loading.mp4");
            if (string.IsNullOrWhiteSpace(path))
            {
                path = $"embed://loading.mp4";
                var mediaSource = CommunityToolkit.Maui.Views.MediaSource.FromFile(path);
                OnMediaSourceToPlay(mediaSource);
            }
            else
            {
                var mediaSource = CommunityToolkit.Maui.Views.MediaSource.FromFile(path);
                OnMediaSourceToPlay(mediaSource);
            }

        }

        private void NavigateToCachedItem(MediaItemBoxViewModel viewModel)
        {
            StartPlayLoadingVideo();

            var e = new CallbackBaseModelEventArgs(viewModel.Item);
            e.Callback += (sender, e) => {
                playingMediaItem = e.Element as MediaItem;
                var mediaSource = CommunityToolkit.Maui.Views.MediaSource.FromFile(playingMediaItem.Path);
                OnMediaSourceToPlay(mediaSource);
            };
            DownloadRequested.Invoke(this, e);
        }

        public event EventHandler<CallbackBaseModelEventArgs> DownloadRequested;

        private string findLocalFile(string fileName, DirectoryInfo folder = null)
        {
            DirectoryInfo tempFolder = new DirectoryInfo(FileSystem.Current.CacheDirectory);
            FileInfo tempFile = new FileInfo(Path.Combine(tempFolder.FullName, fileName));
            if (tempFile.Exists)
                tempFile.Delete();

            if (folder == null)
                folder = new DirectoryInfo(ressourceLocation.Path);
            try
            {
                FileInfo file = new FileInfo(Path.Combine(folder.FullName, fileName));
                if (file.Exists)
                    return file.FullName;

                try
                {
                    file = folder.GetFiles($"*{fileName}").FirstOrDefault();
                    if (file != null && file.Exists)
                        return file.FullName;
                }
                catch { }

                foreach (var subDir in folder.GetDirectories())
                {
                    var path = findLocalFile(fileName, subDir);
                    if (!string.IsNullOrWhiteSpace(path))
                        return path;
                }
            }
            catch { }
            return string.Empty;
        }

        public event EventHandler<MediaSourceEventArgs> MediaSourceToPlay;
        protected virtual void OnMediaSourceToPlay(CommunityToolkit.Maui.Views.MediaSource mediaSource)
        {
            MediaSourceToPlay?.Invoke(this, new MediaSourceEventArgs(mediaSource));
        }
        private MediaItem playingMediaItem = null;
        public void VideoClosed(CommunityToolkit.Maui.Views.MediaSource e)
        {            
            if (playingMediaItem == null)
                return;
            var currentViewModel = viewStack.Peek();
            if (playingMediaItem.CopyType == MediaItemCopyType.Cache)
                mediaLibrary.RemoveMediaItemAsync(playingMediaItem);
            playingMediaItem = null;
        }


        private void NavigateToMediaItem(MediaCollectionBoxViewModel viewModel)
        {
            var vm = serviceProvider.GetService<MediaCollectionViewModel>();
            vm.Title = viewModel.Title;
            vm.Source = viewModel.Source;
            vm.Collection = viewModel.Collection;
            NavigateTo(vm);
        }        
        public void NavigateToSourceOverview()
        {
            var newView = serviceProvider.GetService<SourcesViewModel>();
            newView.Title = "Quellen";
            NavigateTo(newView);
        }
        private void NavigateToSource(SourceBoxViewModel viewModel)
        {
            var vm = serviceProvider.GetService<MediaCollectionViewModel>();
            vm.Title = viewModel.Title;
            vm.Source = viewModel.Source;
            NavigateTo(vm);
        }
        private void NavigateToCategory(CategoryBoxViewModel viewModel)
        {
            var vm = serviceProvider.GetService<LibraryMediaCollectionViewModel>();
            vm.Title = viewModel.Title;
            vm.CategoryType = viewModel.Type;
            NavigateTo(vm);
        }
        public void NavigateToOverview()
        {
            var newView = serviceProvider.GetService<LibraryOverviewViewModel>();
            newView.Title = "Überblick";
            NavigateTo(newView);
        }

        public void NavigateToLog()
        {
            var vm = serviceProvider.GetService<LogListViewModel>();
            vm.Title = "Protokoll";
            NavigateTo(vm);
        }
    }
}
