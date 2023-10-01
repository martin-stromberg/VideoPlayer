using BackgroundAssets;
using MyVideoPlayer.Helper.Download;
using MyVideoPlayer.ViewModels.Navigation;
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

        public void NavigateBack()
        {
            var currentViewModel = viewStack.Pop();
            if (viewStack.Count == 0)     
                viewStack.Push(currentViewModel);
            else
            {                
                currentViewModel.ItemTapped -= ViewModel_ItemTapped;
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


        private void NavigateToCachedItem(MediaItemBoxViewModel viewModel)
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

            var e = new CallbackBaseModelEventArgs(viewModel.Item);
            e.Callback += (sender, e) => {
                var mediaItem = e.Element as MediaItem;
                var mediaSource = CommunityToolkit.Maui.Views.MediaSource.FromFile(mediaItem.Path);
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

            

            //var assembly = typeof(App).GetTypeInfo().Assembly;
            //var assemblyName = assembly.GetName().Name;
            //var names = assembly.GetManifestResourceNames();
            //var stream = assembly.GetManifestResourceStream($"{assemblyName}.{fileName}");
            
            //using (StreamReader reader = new StreamReader(stream)) 
            //using (StreamWriter writer = new StreamWriter(file.FullName))
            //    writer.Write(reader.ReadToEnd());
            //file.Refresh();
            //return file.FullName;
        }

        public event EventHandler<MediaSourceEventArgs> MediaSourceToPlay;
        protected virtual void OnMediaSourceToPlay(CommunityToolkit.Maui.Views.MediaSource mediaSource)
        {
            MediaSourceToPlay?.Invoke(this, new MediaSourceEventArgs(mediaSource));
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

        
    }
}
