using FluentFTP;
using MyVideoPlayer.Helper.Navigation;
using MyVideoPlayer.ViewModels.Navigation;
using MyVideoPlayer.ViewModels.Navigation.MediaCollection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using VideoPlayerLib.Services.MediaLibrary;
using VideoPlayerLib.Services.MediaLibrary.Models;
using VideoPlayerLib.Services.Samba;

namespace MyVideoPlayer.Helper.Download
{
    public interface ILibraryDownloader
    {

    }
    public class LibraryDownloader: ILibraryDownloader
    {
        private readonly LibraryDownloaderSettings settings;
        private readonly INavigationManager navigationManager;
        private readonly IMediaLibrary mediaLibrary;
        private NavigationContentViewModel currentViewModel;

        public LibraryDownloader(   
            LibraryDownloaderSettings settings,
            INavigationManager navigationManager,            
            IMediaLibrary mediaLibrary) 
        {
            this.settings = settings;
            this.navigationManager = navigationManager;
            this.mediaLibrary = mediaLibrary;
            this.navigationManager.NavigationCompleted += NavigationManager_NavigationCompleted;
            this.navigationManager.DownloadRequested += NavigationManager_DownloadRequested;
        }

        private async void NavigationManager_DownloadRequested(object sender, CallbackBaseModelEventArgs e)
        {            
            var mediaItem = e.Element as MediaItem;
            mediaItem = await DownloadMediaItem(null, null, mediaItem);
            e.SendCallback(mediaItem);
        }

        private void NavigationManager_NavigationCompleted(object sender, Navigation.NavigationEventArgs e)
        {
            if (this.currentViewModel != null)
                this.currentViewModel.ItemDownloadRequested -= CurrentViewModel_ItemDownloadRequested;
            this.currentViewModel = e.ContentViewModel;
            if (this.currentViewModel != null)
                this.currentViewModel.ItemDownloadRequested += CurrentViewModel_ItemDownloadRequested;
        }

        private void CurrentViewModel_ItemDownloadRequested(object sender, MediaElementBoxViewModelEventArgs e)
        {
            if (e.ViewModel is MediaCollectionBoxViewModel)
                DownloadMediaItemCollectionAsync(e.ViewModel as MediaCollectionBoxViewModel);
            else if (e.ViewModel is MediaItemBoxViewModel)
                DownloadMediaItem(e.ViewModel as MediaItemBoxViewModel);
        }

        private async void DownloadMediaItem(MediaItemBoxViewModel viewModel)
        {
            await DownloadMediaItem(null, null, viewModel.Item);
        }
        private async void DownloadMediaItemCollectionAsync(MediaCollectionBoxViewModel viewModel)
        {            
            await DownloadMediaItemCollection(viewModel.Collection);
        }
        private async Task DownloadMediaItemCollection(MediaItemCollection collection)
        {
            var source = await mediaLibrary.GetSourceAsync(collection.MediaSourceId);
            foreach (var mediaItem in await mediaLibrary.GetMediaItemsAsync(collection.Id))
                await DownloadMediaItem(source, collection, mediaItem);
            foreach (var subCollection in await mediaLibrary.GetChildMediaItemCollectionsAsync(collection.Id))
                await DownloadMediaItemCollection(subCollection);
        }
        private async Task<MediaItem> DownloadMediaItem(MediaSource source, MediaItemCollection collection, MediaItem mediaItem)
        {
            if (collection == null)
                collection = await mediaLibrary.GetMediaItemCollectionAsync(mediaItem.ParentCollectionId);
            if (source == null)
                source = await mediaLibrary.GetSourceAsync(collection.MediaSourceId);
            var alternateMediaItem = (await mediaLibrary.GetAlternateMediaItemsAsync(mediaItem.Id))
                .FirstOrDefault(item => item.CopyType == MediaItemCopyType.Cache
                                     && item.OriginalMediaItemId == mediaItem.Id);
            if (alternateMediaItem != null)
                return alternateMediaItem;
            if (source is SmbMediaSource)
                return DownloadSmbMediaItem(source as SmbMediaSource, collection, mediaItem);
            else if (source is FtpMediaSource)
                return await DownloadFtpMediaItemAsync(source as FtpMediaSource, collection, mediaItem);
            else
                return null;
        }

        private async Task<MediaItem> DownloadFtpMediaItemAsync(FtpMediaSource source, MediaItemCollection collection, MediaItem mediaItem)
        {
            var alternateMediaItem = mediaItem.Duplicate() as MediaItem;
            alternateMediaItem.Id = 0;
            alternateMediaItem.OriginalMediaItemId = mediaItem.Id;
            alternateMediaItem.CopyType = MediaItemCopyType.Cache;
            alternateMediaItem.Path = Path.Combine(settings.CacheFolderPath, collection.Id.ToString(), mediaItem.Name);
            using (FtpClient client = new FtpClient(source.ServerName, new NetworkCredential(source.Username, source.Password)))
                try
                {
                    client.Connect();
                    try
                    {
                        client.DownloadFile(alternateMediaItem.Path, mediaItem.Path, FtpLocalExists.Overwrite, FtpVerify.Throw);
                    }
                    finally
                    {
                        client.Disconnect();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            if (!File.Exists(alternateMediaItem.Path))
                return null;
            await mediaLibrary.AddMediaItemAsync(alternateMediaItem);
            return alternateMediaItem;
        }

        private MediaItem DownloadSmbMediaItem(SmbMediaSource source, MediaItemCollection collection, MediaItem mediaItem)
        {
            var alternateMediaItem = mediaItem.Duplicate() as MediaItem;
            alternateMediaItem.Id = 0;
            alternateMediaItem.OriginalMediaItemId = mediaItem.Id;
            alternateMediaItem.CopyType = MediaItemCopyType.Cache;
            alternateMediaItem.Path = Path.Combine(settings.CacheFolderPath, collection.Id.ToString(), mediaItem.Name);

            SambaShare sambaShare = new SambaShare(source.ServerName, source.Username, source.Password);
            try
            {
                sambaShare.Connect();
                try
                {
                    sambaShare.DownloadFile(mediaItem.Path, alternateMediaItem.Path);
                }
                finally
                {
                    sambaShare.Disconnect();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            if (!File.Exists(alternateMediaItem.Path))
                return null;

            mediaLibrary.AddMediaItemAsync(alternateMediaItem).Wait();
            return alternateMediaItem;
        }
    }
}
