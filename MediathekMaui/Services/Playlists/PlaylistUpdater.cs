using Mediathek.Services.MediaLibrary;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace Mediathek.Services.Playlists
{
    public class PlaylistUpdater: BaseManager
    {

        public PlaylistUpdater(IMediaLibrary mediaLibrary)
            : base(mediaLibrary) { }

        private BackgroundWorker _worker = null;
        private bool _working = false;
        private TimeSpan _workerInterval = TimeSpan.FromSeconds(5);
        private ConcurrentQueue<BaseModel> _workerQueue = new ConcurrentQueue<BaseModel>();

        protected void StartWorker(BaseModel item)
        {
            _workerQueue.Enqueue(item);
            if (_worker != null)
                return;
            _worker = new BackgroundWorker();
            _worker.DoWork += _worker_DoWork;
            _worker.RunWorkerCompleted += _worker_RunWorkerCompleted;
            _worker.RunWorkerAsync();
        }

        private async void _worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            await Task.Delay(_workerInterval);
            _worker.RunWorkerAsync();
        }

        private async void _worker_DoWork(object sender, DoWorkEventArgs e)
        {
            if (_working)
                return;
            _working = true;
            try
            {
                if (_workerQueue.TryDequeue(out BaseModel item))
                    try
                    {
                        await ProcessAsync(item);
                        await CleanOrphanedPlaylists();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                    }
            }
            finally
            {
                _working = false;
            }
        }

        public void UpdateAsync(TVShow show)
        {
            StartWorker(show);
        }

        private async Task ProcessAsync(BaseModel item)
        {
            await ProcessTVShowAsync(item as TVShow);
        }

        private async Task CleanOrphanedPlaylists()
        {
            var playlists = (await MediaLibrary.GetPlaylists())
                .Where(playlists => playlists.Type == PlaylistType.TVShowCollection);
            foreach (var playlist in playlists.ToArray())
            {
                var tvshowfound = (await MediaLibrary.GetTVShowCollections()).Any(collection =>
                                                                                  collection.PlaylistId == playlist.Id);
                if (tvshowfound)
                    continue;
                await MediaLibrary.RemovePlaylistAsync(playlist);
            }
        }

        private async Task ProcessTVShowAsync(TVShow show)
        {
            if (show is null)
                return;
            if (show.CollectionId != 0)
            {
                var collection = await MediaLibrary.GetTVShowCollection(show.CollectionId);
                await UpdateTVShowPlaylist(collection, show);
            }
            else
                await ProcessOrphanedTVShowAsync(show);
        }

        private async Task ProcessOrphanedTVShowAsync(TVShow show)
        {
            var playlists = (await MediaLibrary.GetPlaylists())
                .Where(playlists => playlists.Type == PlaylistType.TVShowCollection)
                .Where(pl => !pl.Items.Any());
            foreach (var playlist in playlists.Where(playlist => playlist.Items.Any()))
            {
                var mediaItem = playlist.Items.FirstOrDefault();
                var typedItem = await MediaLibrary.GetTypedItem(mediaItem.Id) as TVShowEpisode;
                var season = await MediaLibrary.GetTVShowSeason(typedItem.SeasonId);
                if (season.ShowId != show.Id)
                    continue;
                await RemoveTVShowFromPlaylist(playlist, show);
            }
        }

        private Task RemoveTVShowFromPlaylist(Playlist playlist, TVShow show)
        {
            throw new NotImplementedException();
        }

        private async Task UpdateTVShowPlaylist(TVShowCollection collection, TVShow show)
        {
            var playlist = await MediaLibrary.GetPlaylist(collection.PlaylistId);
            if (playlist is null)
                playlist = await CreateTVShowCollectionPlaylist(collection);
            else
            {
                await UpdateTVShowOnPlaylist(collection, playlist, show);
                await MediaLibrary.AddPlaylistAsync(playlist);
            }
        }

        private async Task<Playlist> CreateTVShowCollectionPlaylist(TVShowCollection collection)
        {
            var playlist = new Playlist() { Name = collection.Name, Type = PlaylistType.TVShowCollection };
            await CollectAllTVShows(collection, playlist);

            await MediaLibrary.AddPlaylistAsync(playlist);
            collection.PlaylistId = playlist.Id;
            await MediaLibrary.AddTVShowCollectionAsync(collection);
            return playlist;
        }

        private async Task CollectAllTVShows(TVShowCollection collection, Playlist playlist)
        {
            var shows = await MediaLibrary.GetTVShows(collection.Id);
            foreach (var show in shows)
                await UpdateTVShowOnPlaylist(collection, playlist, show);
        }

        private async Task UpdateTVShowOnPlaylist(TVShowCollection collection, Playlist playlist, TVShow show)
        {
            var seasons = await MediaLibrary.GetTVShowSeasons(show.Id);
            foreach (var season in seasons)
                await UpdateTVShowOnPlaylist(collection, playlist, season);
        }

        private async Task UpdateTVShowOnPlaylist(TVShowCollection collection, Playlist playlist, TVShowSeason season)
        {
            var episodes = await MediaLibrary.GetTVShowEpisodes(season.Id);
            foreach (var episode in episodes)
                UpdateTVShowOnPlaylist(collection, playlist, episode);
        }

        private void UpdateTVShowOnPlaylist(TVShowCollection collection, Playlist playlist, TVShowEpisode episode)
        {
            var existing = playlist.Items.FirstOrDefault(i => episode.MediaItems.Contains(i.MediaItemId));
            if (existing is not null)
                CheckTVShowEpisodeOrder(playlist, existing);
            else
                InsertTVShowEpisode(collection, playlist, episode);
        }

        private void InsertTVShowEpisode(TVShowCollection collection, Playlist playlist, TVShowEpisode episode)
        {
            if (episode.PrimaryMediaItem is null)
                return;
            var newEntry = new PlaylistEntry()
            {
                Item = episode.PrimaryMediaItem,
                MediaItemId = episode.PrimaryMediaItem.Id,
                Name = episode.Name,
                PlaylistId = playlist.Id,
            };
            if (playlist.Items.Count == 0)
                playlist.Items.Add(newEntry);
            else
            {
                var nextElem = playlist.Items.SkipWhile(elem => PlaylistEntryCompare(elem, newEntry)).FirstOrDefault();
                if (nextElem is null)
                    playlist.Items.Add(newEntry);
                else
                    playlist.Items.Insert(playlist.Items.IndexOf(nextElem), newEntry);
            }
        }

        private Func<PlaylistEntry, PlaylistEntry, bool> PlaylistEntryCompare = new Func<PlaylistEntry, PlaylistEntry, bool>((entry1, entry2) =>
        {
            var info1 = entry1.Item.MetaInfo as EpisodeInformation;
            var info2 = entry2.Item.MetaInfo as EpisodeInformation;
            if (info1.AiredAt < info2.AiredAt)
                return true;
            if ((info1.AiredAt == info2.AiredAt) && (info1.Episode.CompareTo(info2.Episode) < 0))
                return true;
            return false;
        });

        private void CheckTVShowEpisodeOrder(Playlist playlist, PlaylistEntry entry)
        {
            int offset = playlist.Items.IndexOf(entry);
            while ((offset > 1) && !PlaylistEntryCompare(playlist.Items[offset - 1], entry))
            {
                playlist.Items.Remove(entry);
                playlist.Items.Insert(offset - 1, entry);
                offset = playlist.Items.IndexOf(entry);
            }

            offset = playlist.Items.IndexOf(entry);
            while ((offset < playlist.Items.Count - 1) && PlaylistEntryCompare(playlist.Items[offset + 1], entry))
            {
                playlist.Items.Remove(entry);
                playlist.Items.Insert(offset + 1, entry);
                offset = playlist.Items.IndexOf(entry);
            }
        }

    }
}
