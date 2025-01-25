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
using VideoPlayer.Service.Processor;
using VideoPlayer.Service.Settings;
using static SQLite.SQLite3;

namespace VideoPlayer.Service.Playlists
{
    public class NextPlaybackPlaylist : BasePlaylistService
    {
        private readonly IProcessorCollection processorCollection;
        private readonly IApplicationSettings applicationSettings;

        public NextPlaybackPlaylist(
            IMediaLibrary mediaLibrary,
            IMediaCollectionSelector mediaCollectionSelector,
            IProcessorCollection processorCollection,
            IDownloadManager downloadManager,
            IApplicationSettings applicationSettings,
            ILogger logger)
            : base(mediaLibrary, mediaCollectionSelector, downloadManager, PlaylistType.NextPlayback, logger)
        {
            this.processorCollection = processorCollection;
            this.applicationSettings = applicationSettings;
        }

        #region Timer
        private TimeSpan _StartingPeriod = TimeSpan.FromSeconds(5);
        private TimeSpan _EndingPeriod = TimeSpan.FromSeconds(30);
        private TimeSpan _ProcessingInterval = TimeSpan.FromSeconds(5);
        private System.Timers.Timer _Worker = null;
        private long _ExecutionSession = 0;
        private object _ProcessingLock = new object();
        private MediaItem _ProcessingMediaItem;
        private TimeSpan _ProcessingPosition;
        private TimeSpan _ProcessingDuration;
        private bool _Executing = false;
        private TimeSpan _LastProcessingPosition = TimeSpan.Zero;
        private TimeSpan _LastPosition = TimeSpan.Zero;
        private MediaItem _LastItem = null;
        private int _BreakupCounter = 0;
        public void Start()
        {
            if (_Worker is not null) return;            
            _Worker = new System.Timers.Timer(TimeSpan.FromSeconds(5)) { AutoReset = false };            
            var currentExecutionSession = _ExecutionSession = DateTime.Now.Ticks;
            _Worker.Elapsed += (sender, args) =>
            {
                _Worker.Stop();
                if (processorCollection is not null)
                {
                    processorCollection.Enqueue(
                        "",
                        ExecuteTimer,
                        args,
                        (arg) =>
                        {
                            if (_Worker is not null && currentExecutionSession == _ExecutionSession)
                                if (_BreakupCounter < 5)
                                    _Worker.Start();
                                else Stop();
                        },
                        (arg, ex) =>
                        {
                            NotifyError(ex);
                        });
                }
                else
                {
                    ExecuteTimer(args);
                    if (_BreakupCounter < 5)
                        _Worker.Start();
                    else Stop();
                }
            };
            _Worker.Start();
        }
        private void Stop()
        {
            if (_Worker is not null)
                _Worker.Dispose();
            _Worker = null;
        }
        private void ExecuteTimer(object args)
        {
            if (_Executing)
                return;
            _Executing = true;
            try
            {
                ProcessVideoPosition();
            }
            catch (Exception ex)
            {
                NotifyError(ex);
            }
            finally
            {
                _Executing = false;
            }
        }
        private void ProcessVideoPosition()
        {
            MediaItem currentMediaItem;
            TimeSpan currentPosition;
            TimeSpan currentDuration;
            lock (_ProcessingLock)
            {
                currentMediaItem = _ProcessingMediaItem;
                currentPosition = _ProcessingPosition;
                currentDuration = _ProcessingDuration;
            }
            if (_LastPosition == currentPosition)
            {
                _BreakupCounter += 1;
                return;
            }
            _LastPosition = currentPosition;
            if (currentPosition < _StartingPeriod)
            {
                _LastProcessingPosition = currentPosition;
                return;
            }

            if (_LastProcessingPosition > currentPosition)
            {
                _BreakupCounter = 0;
                _LastProcessingPosition = currentPosition;
                return;
            }

            if (_LastItem is null || _LastItem.Id != currentMediaItem.Id)
            {
                _BreakupCounter = 0;
                ProcessVideoChanged(currentMediaItem, currentPosition);
                return;
            }

            _BreakupCounter = 0;
            if (_LastProcessingPosition.Add(_ProcessingInterval) > currentPosition)
                return;
            _LastProcessingPosition = currentPosition;

            var firstEntry = Current.First;
            if (currentPosition.Add(_EndingPeriod) > currentDuration)
            {
                ProcessVideoEnding(currentMediaItem);
                return;
            }

            if (firstEntry is null)
                firstEntry = AddMediaItem(currentMediaItem);
            if (firstEntry.Item is not null && firstEntry.Item.Id == currentMediaItem.Id)
            {
                SaveMediaItemPosition(firstEntry.Item, firstEntry.Entry, currentPosition);
                return;
            }

            _ = AddMediaItem(currentMediaItem);
        }
        private void ProcessVideoChanged(MediaItem currentMediaItem, TimeSpan position)
        {
            _LastProcessingPosition = TimeSpan.Zero;
            _LastItem = currentMediaItem;
            _ProcessingInterval = TimeSpan.FromSeconds(5);
        }
        private void ProcessVideoEnding(MediaItem currentMediaItem)
        {
            var existing = Current.Items.FirstOrDefault(i => i.Item?.Id == currentMediaItem.Id);
            if (existing is null)
                return;
            #region Zuletzt gesehen speichern
            if (existing.Entry is not null)
            {
                if (existing.Entry is TVShowSeason)
                {
                    var episode = MediaLibrary.GetTVShowEpisodeByMediaItem(currentMediaItem.Id);
                    if (episode is not null)
                        existing.Entry = episode;
                }
                existing.Entry.LastWatched = DateTime.Now;
                MediaLibrary.AddOrUpdateEntry(existing.Entry);
            }
            #endregion
            var nextMediaItem = FindNextMediaItem(currentMediaItem);
            if (nextMediaItem is not null)
                base.Current.SkipNextDownload();
            Current.Remove(existing);
            if (nextMediaItem is not null)
                AddMediaItem(nextMediaItem);
            else
                SaveChanges();
        }
        private void SaveMediaItemPosition(MediaItem item, ClassifiedEntry entry, TimeSpan position)
        {
            if (item is not null)
            {
                item.LastPosition = position;
                MediaLibrary.AddOrUpdateMediaItem(item);
            }
        }
        #endregion

        protected override Playlist InitCurrentPlaylist()
        {
            var playlist = base.InitCurrentPlaylist();
            playlist.AutoDownload = true;
            return playlist;
        }
        internal void ProcessVideoPosition(MediaItem mediaItem, TimeSpan position, TimeSpan duration)
        {
            lock (_ProcessingLock)
            {
                _ProcessingMediaItem = mediaItem;
                _ProcessingPosition = position;
                _ProcessingDuration = duration;
            }
            Start();
        }

        protected override void ExecuteDownloadFinished(ClassifiedEntry entry, MediaItem item)
        {
            base.ExecuteDownloadFinished(entry, item);
            StartDownloadSecondMediaItem(entry, item);
        }

        private void StartDownloadSecondMediaItem(ClassifiedEntry entry, MediaItem mediaItem)
        {
            var existing = Current.Items.FirstOrDefault(i => i.Item is not null && i.Item.Id == mediaItem.Id);
            if (existing is null)
            {
                if (entry is null)
                    entry = GetClassifiedEntry(mediaItem);
                if (entry is not null)
                    existing = Current.Items.FirstOrDefault(i => i.Item is null && i.Entry is not null && i.Entry.Id == entry.Id);
                if (existing is null)
                {
                    TVShowEpisode episode = entry as TVShowEpisode;
                    if (episode is not null)
                        if (episode.Episode == 1)
                        {
                            var season = MediaLibrary.GetTVShowSeason(episode.SeasonId);
                            if (season is not null)
                                entry = season;
                            existing = Current.Items.FirstOrDefault(i => i.Item is null && i.Entry.Id == entry.Id);
                        }

                }
            }
            if (existing is null)
                return;
            var nextMediaItem = FindNextMediaItem(mediaItem);
            ExecuteDownloadRequest(nextMediaItem, applicationSettings.DownloadDueTimeNextPlaylistCache);
        }

        private PlaylistEntry AddMediaItem(MediaItem mediaItem)
        {
            if (mediaItem is null)
                return null;
            try
            {                
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

                #region Erste Episode => Staffel
                TVShowEpisode episode = nextEntry as TVShowEpisode;
                if (episode is not null)
                    if (episode.Episode == 1)
                    {
                        var season = MediaLibrary.GetTVShowSeason(episode.SeasonId);
                        if (season is not null)
                        {
                            MediaLibrary.Release(nextEntry);
                            nextEntry = season;
                        }
                    }
                #endregion
                

                var entriesToRemove = RemoveBelongingEntries(nextEntry as TVShowEpisode)
                    .Concat(RemoveBelongingEntries(nextEntry as Movie))
                    .ToList();
                Current.Add(mediaItem, nextEntry);
                foreach (var entry in entriesToRemove)
                {
                    Current.SkipNextDownload();
                    Current.Remove(entry);
                }
                return Current.First;
            }
            finally
            {
                SaveChanges();
            }
        }

        private IEnumerable<PlaylistEntry> RemoveBelongingEntries(TVShowEpisode entry)
        {
            if (entry is null) yield break;
            var season = MediaLibrary.GetTVShowSeason(entry.SeasonId);
            if (!Current.Items.Any()) yield break;
            foreach (var existing in Current.Items.ToArray())
            {
                var existingEntry = existing.Entry as TVShowEpisode;
                var existingSeason = (existingEntry is null) 
                                   ? existing.Entry as TVShowSeason
                                   : MediaLibrary.GetTVShowSeason(existingEntry.SeasonId);
                if (existingSeason is null) continue;
                if (existingSeason.ShowId != season.ShowId)
                    continue;
                yield return existing;                
            }
        }
        private IEnumerable<PlaylistEntry> RemoveBelongingEntries(Movie entry)
        {
            if (entry is null) yield break;
            var collection = MediaLibrary.GetMovieCollection(entry.CollectionId);
            if (collection is null) yield break;
            foreach (var existing in Current.Items.ToArray())
            {
                var existingEntry = existing.Entry as Movie;
                if (existingEntry is null) continue;
                if (existingEntry.CollectionId != collection.Id)
                    continue;
                yield return existing;
            }
        }        

        private ClassifiedEntry GetClassifiedEntry(MediaItem mediaItem)
        {
            if (mediaItem is null) return null;
            return MediaLibrary.GetTVShowEpisodeByMediaItem(mediaItem.Id) as ClassifiedEntry
                ?? MediaLibrary.GetMovieByMediaItem(mediaItem.Id);
        }

        
    }
}
