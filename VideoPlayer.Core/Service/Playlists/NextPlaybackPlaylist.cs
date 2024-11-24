using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.BaseServices;
using VideoPlayer.Service.Download;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Library.Models.Playlists;

namespace VideoPlayer.Service.Playlists
{
    public class NextPlaybackPlaylist : BasePlaylistService
    {
        public NextPlaybackPlaylist(
            IMediaLibrary mediaLibrary,
            IMediaCollectionSelector mediaCollectionSelector,
            ILogger logger)
            : base(mediaLibrary, mediaCollectionSelector, PlaylistType.NextPlayback, logger)
        {
        }

        private TimeSpan StartingPeriod = TimeSpan.FromSeconds(5);
        private TimeSpan EndingPeriod = TimeSpan.FromSeconds(30);
        private TimeSpan ProcessingInterval = TimeSpan.FromSeconds(5);
        private MediaItem lastItem = null;
        private TimeSpan lastPosition = TimeSpan.Zero;
        private bool processing = false;
        internal void ProcessVideoPosition(MediaItem mediaItem, TimeSpan position, TimeSpan duration)
        {
            if (processing) return;
            processing = true;
            try
            {
                if (position < StartingPeriod)
                {
                    lastPosition = position;
                    return;
                }

                if (lastItem is null || lastItem.Id != mediaItem.Id)
                {
                    ProcessVideoChanged(mediaItem, position);
                    return;
                }
                if (lastPosition.Add(ProcessingInterval) > position)
                    return;
                lastPosition = position;

                var firstEntry = Current.First;
                if (position.Add(EndingPeriod) > duration)
                {
                    ProcessVideoEnding(mediaItem);
                    return;
                }

                if (firstEntry is null)
                    firstEntry = AddMediaItem(mediaItem);
                if (firstEntry.Item is not null && firstEntry.Item.Id == mediaItem.Id)
                {
                    SaveMediaItemPosition(firstEntry.Item, firstEntry.Entry, position);
                    return;
                }

                _ = AddMediaItem(mediaItem);
            }
            finally
            {
                processing = false;
            }            
        }
        private void SaveMediaItemPosition(MediaItem item, ClassifiedEntry entry, TimeSpan position)
        {
            if (item is not null)
            {
                item.LastPosition = position;
                MediaLibrary.AddOrUpdateMediaItem(item);
            }
        }

        private PlaylistEntry AddMediaItem(MediaItem mediaItem)
        {
            if (mediaItem is null)
                return null;
            var existing = Current.Items.FirstOrDefault(i => i.Item is not null && i.Item.Id == mediaItem.Id);
            if (existing is not null)
            {
                Current.MoveTo(existing, 0);
                return Current.First;
            }
            var nextEntry = GetClassifiedEntry(mediaItem);
            existing = Current.Items.FirstOrDefault(i => i.Item is null && i.Entry.Id == nextEntry.Id);
            if (existing is not null)
            {
                existing.Item = mediaItem;
                Current.MoveTo(existing, 0);
                return Current.First;
            }
            RemoveBelongingEntries(nextEntry as TVShowEpisode);
            RemoveBelongingEntries(nextEntry as Movie);
            Current.Add(mediaItem, nextEntry);
            SaveChanges();
            return Current.First;
        }

        private void RemoveBelongingEntries(TVShowEpisode entry)
        {
            if (entry is null) return;
            var season = MediaLibrary.GetTVShowSeason(entry.SeasonId);
            if (!Current.Items.Any()) return;
            foreach (var existing in Current.Items.ToArray())
            {
                var existingEntry = existing.Entry as TVShowEpisode;
                if (existingEntry is null) continue;
                var existingSeason = MediaLibrary.GetTVShowSeason(existingEntry.SeasonId);
                if (existingSeason.ShowId != season.ShowId)
                    continue;
                Current.Remove(existing);
            }
        }
        private void RemoveBelongingEntries(Movie entry)
        {
            if (entry is null) return;
            var collection = MediaLibrary.GetMovieCollection(entry.CollectionId);
            if (collection is null) return;
            foreach (var existing in Current.Items.ToArray())
            {
                var existingEntry = existing.Entry as Movie;
                if (existingEntry is null) continue;
                if (existingEntry.CollectionId != collection.Id)
                    continue;
                Current.Remove(existing);
            }
        }

        private void ProcessVideoChanged(MediaItem currentMediaItem, TimeSpan position)
        {
            lastPosition = TimeSpan.Zero;
            lastItem = currentMediaItem;
            ProcessingInterval = TimeSpan.FromSeconds(5);
        }
        private void ProcessVideoEnding(MediaItem currentMediaItem)
        {
            var existing = Current.Items.FirstOrDefault(i => i.Item?.Id == currentMediaItem.Id);
            if (existing is null)
                return;
            #region Zuletzt gesehen speichern
            if (existing.Entry is not null) 
            {
                existing.Entry.LastWatched = DateTime.Now;
                MediaLibrary.AddOrUpdateEntry(existing.Entry);
            }
            #endregion
            var nextMediaItem = FindNextMediaItem(currentMediaItem);
            Current.Remove(existing);
            AddMediaItem(nextMediaItem);
        }

        private ClassifiedEntry GetClassifiedEntry(MediaItem mediaItem)
        {
            if (mediaItem is null) return null;
            return MediaLibrary.GetTVShowEpisodeByMediaItem(mediaItem.Id) as ClassifiedEntry
                ?? MediaLibrary.GetMovieByMediaItem(mediaItem.Id);
        }

        
    }
}
