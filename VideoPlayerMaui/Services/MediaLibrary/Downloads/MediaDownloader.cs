using FluentFTP;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Models.Sources;

namespace VideoPlayer.Services.MediaLibrary.Downloads
{
    public class MediaDownloader: IMediaDownloader
    {

        private readonly IMediaLibrary _MediaLibrary;
        private readonly MediaLibrarySettings _Settings;

        public MediaDownloader(IMediaLibrary mediaLibrary, MediaLibrarySettings settings)
        {
            _Settings = settings;
            _MediaLibrary = mediaLibrary;
        }

        public async Task<MediaItem> CacheAsync(MediaItem item)
        {
            if (item.CopyType == MediaItemCopyType.Cache)
                return item;

            var collection = await _MediaLibrary.GetMediaItemCollectionAsync(item.ParentCollectionId);
            var source = await _MediaLibrary.GetSourceAsync(collection.MediaSourceId);
            var alternateMediaItem = (await _MediaLibrary.GetAlternateMediaItemsAsync(item.Id))
                .FirstOrDefault(foundItem =>
                                (foundItem.CopyType == MediaItemCopyType.Cache)
                    && (foundItem.OriginalMediaItemId == item.Id));
            while ((alternateMediaItem != null) && !File.Exists(alternateMediaItem.Path))
            {
                await _MediaLibrary.RemoveMediaItemAsync(alternateMediaItem);
                alternateMediaItem = (await _MediaLibrary.GetAlternateMediaItemsAsync(item.Id))
                    .FirstOrDefault(foundItem =>
                                    (foundItem.CopyType == MediaItemCopyType.Cache)
                        && (foundItem.OriginalMediaItemId == item.Id));
            }
            if (alternateMediaItem != null)
                return alternateMediaItem;
            if (source is SmbMediaSource)
                return DownloadSmbMediaItem(source as SmbMediaSource, collection, item);
            else if (source is FtpMediaSource)
                return await DownloadFtpMediaItemAsync(source as FtpMediaSource, collection, item);
            else
                return null;
        }

        private async Task<MediaItem> DownloadFtpMediaItemAsync(
            FtpMediaSource source,
            MediaItemCollection collection,
            MediaItem mediaItem)
        {
            var alternateMediaItem = mediaItem.Duplicate() as MediaItem;
            alternateMediaItem.Id = 0;
            alternateMediaItem.OriginalMediaItemId = mediaItem.Id;
            alternateMediaItem.CopyType = MediaItemCopyType.Cache;
            alternateMediaItem.Path = Path.Combine(_Settings.CacheFolderPath, collection.Id.ToString(), mediaItem.Name);
            using (FtpClient client = new FtpClient(source.ServerName, new NetworkCredential(source.Username, source.Password)))
                try
                {
                    client.Connect();
                    try
                    {
                        client.DownloadFile(alternateMediaItem.Path,
                                            mediaItem.Path,
                                            FtpLocalExists.Overwrite,
                                            FtpVerify.Throw);
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
            await _MediaLibrary.AddMediaItemAsync(alternateMediaItem);
            return alternateMediaItem;
        }

        private MediaItem DownloadSmbMediaItem(
            SmbMediaSource source,
            MediaItemCollection collection,
            MediaItem mediaItem)
        {
            var alternateMediaItem = mediaItem.Duplicate() as MediaItem;
            alternateMediaItem.Id = 0;
            alternateMediaItem.OriginalMediaItemId = mediaItem.Id;
            alternateMediaItem.CopyType = MediaItemCopyType.Cache;
            alternateMediaItem.Path = Path.Combine(_Settings.CacheFolderPath, collection.Id.ToString(), mediaItem.Name);

            // SambaShare sambaShare = new SambaShare(source.ServerName, source.Username, source.Password);
            // try
            // {
            // sambaShare.Connect();
            // try
            // {
            // sambaShare.DownloadFile(mediaItem.Path, alternateMediaItem.Path);
            // }
            // finally
            // {
            // sambaShare.Disconnect();
            // }
            // }
            // catch (Exception ex)
            // {
            // Debug.WriteLine(ex);
            // }

            // if (!File.Exists(alternateMediaItem.Path))
            // return null;

            // mediaLibrary.AddMediaItemAsync(alternateMediaItem).Wait();
            // return alternateMediaItem;
            throw new NotImplementedException();
        }

    }
}
