using System;
using System.Linq;

namespace Mediathek.Services.MediaLibrary.PlaybackHistory
{
    public class PlaybackHistoryManager: IPlaybackHistoryManager
    {

        private readonly IMediaLibrary _MediaLibrary;

        public PlaybackHistoryManager(IMediaLibrary mediaLibrary)
        {
            _MediaLibrary = mediaLibrary;
        }

        public History CurrentHistory { get; } = new History();

        public bool IsInitialized { get; set; }

        public async Task InitializeAsync()
        {
            var entries = await _MediaLibrary.GetPlayBackHistoryEntries();
            foreach (var entry in entries)
                CurrentHistory.Items.Add(entry);
            IsInitialized = true;
        }

        public async Task Add(MediaItem item, BaseModel typedItem, Playlist playlist)
        {
            if (typedItem == null)
                return;
            var existing = CurrentHistory.Items
                                         .FirstOrDefault(i =>
                                                         (i.TypedItem.Id == typedItem.Id)
                                             && ((i.Playlist?.Id ?? 0) == (playlist?.Id ?? 0)));
            if (existing == null)
            {
                CurrentHistory.Items
                              .Insert(0, new HistoryEntry() { Item = item, TypedItem = typedItem, Playlist = playlist });
                await FindAndRemoveOther(typedItem, playlist);
                await _MediaLibrary.AddPlaybackHistory(CurrentHistory);
            }
            else
            {
                int offset = CurrentHistory.Items.IndexOf(existing);
                if (offset > 0)
                {
                    CurrentHistory.Items.Move(offset, 0);
                    await _MediaLibrary.AddPlaybackHistory(CurrentHistory);
                }
            }
            return;
        }

        public async Task Remove(BaseModel item)
        {
            var existing = CurrentHistory.Items.FirstOrDefault(i => i.TypedItem.Id == item.Id);
            if (existing == null)
                return;
            CurrentHistory.Items.Remove(existing);
            await _MediaLibrary.AddPlaybackHistory(CurrentHistory);
        }

        private async Task FindAndRemoveOther(BaseModel typedItem, Playlist playlist)
        {
            await FindAndRemoveOtherFromShow(typedItem as TVShowEpisode, playlist);
            FindAndRemoveOtherFromMovieCollection(typedItem as Movie, playlist);
        }

        private void FindAndRemoveOtherFromMovieCollection(Movie movie, Playlist playlist)
        {
            if (movie == null)
                return;
            if (movie.CollectionId == 0)
                return;
            var existingCollectionEntry = CurrentHistory.Items
                                                        .Where(e => e.TypedItem is Movie)
                                                        .FirstOrDefault(e =>
                                                        {
                                                            var movie = e.TypedItem as Movie;
                                                            if (movie.Id == movie.Id)
                                                                return false;
                                                            if (movie.CollectionId != movie.CollectionId)
                                                                return false;
                                                            if (playlist == null)
                                                                return true;
                                                            if (e.Playlist == null)
                                                                return true;
                                                            if (e.Playlist.Id == playlist.Id)
                                                                return false;
                                                            return true;
                                                        });
            if (existingCollectionEntry == null)
                return;
            CurrentHistory.Items.Remove(existingCollectionEntry);
        }

        private async Task FindAndRemoveOtherFromShow(TVShowEpisode episode, Playlist playlist)
        {
            if (episode == null)
                return;
            var existingseasonEntry = CurrentHistory.Items
                                                    .Where(e => e.TypedItem is TVShowEpisode)
                                                    .FirstOrDefault(e =>
                                                    {
                                                        var listEpisode = e.TypedItem as TVShowEpisode;
                                                        if (listEpisode.Id == episode.Id)
                                                            return false;
                                                        if (listEpisode.SeasonId != episode.SeasonId)
                                                            return false;
                                                        if (playlist == null)
                                                            return true;
                                                        if (e.Playlist == null)
                                                            return true;
                                                        if (e.Playlist.Id == playlist.Id)
                                                            return false;
                                                        return true;
                                                    });
            if (existingseasonEntry == null)
            {
                var season = await _MediaLibrary.GetTVShowSeason(episode.SeasonId);
                foreach (var item in CurrentHistory.Items
                                                   .Where(e => e.TypedItem is TVShowEpisode)
                                                   .Where(e => ((TVShowEpisode)e.TypedItem).Id != episode.Id))
                {
                    var ee = (TVShowEpisode)item.TypedItem;
                    var es = await _MediaLibrary.GetTVShowSeason(ee.SeasonId);
                    if (es.ShowId != season.ShowId)
                        continue;
                    existingseasonEntry = item;
                    break;
                }
            }
            if (existingseasonEntry == null)
                return;
            CurrentHistory.Items.Remove(existingseasonEntry);
            await FindAndRemoveOtherFromShow(episode, playlist);
        }

        public async Task Finish(MediaItem item, BaseModel typedItem)
        {
            if (typedItem == null)
                return;
            var existing = CurrentHistory.Items.FirstOrDefault(i => i.TypedItem.Id == typedItem.Id);
            if (existing == null)
                return;
            CurrentHistory.Items.Remove(existing);
            AddNext(existing);
        }

        private async void AddNext(HistoryEntry existing)
        {
            if (await AddNextPlaylistEntry(existing.Playlist))
                return;
            if (await AddNextEpisode(existing.TypedItem as TVShowEpisode))
                return;
            if (await AddNextCollectionMovieAsync(existing.TypedItem as Movie))
                return;
            await _MediaLibrary.AddPlaybackHistory(CurrentHistory);
        }

        private async Task<bool> AddNextPlaylistEntry(Playlist playlist)
        {
            if (playlist is null)
                return false;
            playlist.CurrentPosition += 1;
            if (playlist.CurrentPosition >= playlist.Items.Count)
                return true;
            var mediaItem = playlist.Items[playlist.CurrentPosition].Item;
            var typedItem = await _MediaLibrary.GetTypedItem(mediaItem.Id);
            await Add(mediaItem, typedItem, playlist);
            return true;
        }

        private async Task<bool> AddNextCollectionMovieAsync(Movie movie)
        {
            if (movie == null)
                return false;
            if (movie.CollectionId == 0)
                return false;
            var collection = await _MediaLibrary.GetMovieCollection(movie.CollectionId);
            if (collection == null)
                return false;
            var movies = await _MediaLibrary.GetMovies(collection.Id);
            var nextMovie = movies
                .OrderBy(m => m.Name)
                .SkipWhile(e => e.Id != movie.Id)
                .SkipWhile(e => e.Id == movie.Id)
                .FirstOrDefault();
            if (nextMovie == null)
                return false;
            await Add(null, nextMovie, null);
            return true;
        }

        private async Task<bool> AddNextEpisode(TVShowEpisode episode)
        {
            if (episode == null)
                return false;
            var episodes = await _MediaLibrary.GetTVShowEpisodes(episode.SeasonId);
            var nextEpisode = episodes
                .OrderBy(m => m.EpisodeNo)
                .SkipWhile(e => e.EpisodeNo != episode.EpisodeNo)
                .SkipWhile(e => e.EpisodeNo == episode.EpisodeNo)
                .FirstOrDefault();
            if (nextEpisode == null)
            {
                var season = await _MediaLibrary.GetTVShowSeason(episode.SeasonId);
                var seasons = await _MediaLibrary.GetTVShowSeasons(season.ShowId);
                var nextSeason = seasons
                    .OrderBy(e => e.Name)
                    .SkipWhile(e => e.Name != season.Name)
                    .SkipWhile(e => e.Name == season.Name)
                    .FirstOrDefault();
                if (nextSeason != null)
                {
                    episodes = await _MediaLibrary.GetTVShowEpisodes(nextSeason.Id);
                    nextEpisode = episodes
                        .OrderBy(m => m.EpisodeNo)
                        .FirstOrDefault();
                }
            }

            if (nextEpisode == null)
                return false;
            await Add(null, nextEpisode, null);
            return true;
        }

    }
}
