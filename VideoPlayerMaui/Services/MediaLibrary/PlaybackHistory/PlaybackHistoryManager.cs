using System;
using System.Linq;
using VideoPlayer.Models;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Models.Movies;
using VideoPlayer.Models.PlaybackHistory;
using VideoPlayer.Models.TVShows;

namespace VideoPlayer.Services.MediaLibrary.PlaybackHistory
{
    public class PlaybackHistoryManager: IPlaybackHistoryManager
    {

        private readonly IMediaLibrary _MediaLibrary;

        public PlaybackHistoryManager(IMediaLibrary mediaLibrary)
        {
            _MediaLibrary = mediaLibrary;
        }

        public History CurrentHistory { get; } = new History();

        public Task Add(MediaItem item, BaseModel typedItem)
        {
            if (typedItem == null)
                return Task.CompletedTask;
            var existing = CurrentHistory.Items.FirstOrDefault(i => i.TypedItem.Id == typedItem.Id);
            if (existing == null)
            {
                CurrentHistory.Items.Insert(0, new HistoryEntry() { Item = item, TypedItem = typedItem });
                FindAndRemoveOther(typedItem);
            }
            else
            {
                int offset = CurrentHistory.Items.IndexOf(existing);
                if (offset > 0)
                    CurrentHistory.Items.Move(offset, 0);
            }
            return Task.CompletedTask;
        }

        private void FindAndRemoveOther(BaseModel typedItem)
        {
            FindAndRemoveOtherFromShow(typedItem as TVShowEpisode);
        }

        private async void FindAndRemoveOtherFromShow(TVShowEpisode episode)
        {
            if (episode == null)
                return;
            var existingseasonEntry = CurrentHistory.Items
                                                    .Where(e => e.TypedItem is TVShowEpisode)
                                                    .FirstOrDefault(e =>
                                                                    (((TVShowEpisode)e.TypedItem).Id != episode.Id)
                                                        && (((TVShowEpisode)e.TypedItem).SeasonId == episode.SeasonId));
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
            if (existingseasonEntry != null)
                CurrentHistory.Items.Remove(existingseasonEntry);
        }

        public Task Finish(MediaItem item, BaseModel typedItem)
        {
            if (typedItem == null)
                return Task.CompletedTask;
            var existing = CurrentHistory.Items.FirstOrDefault(i => i.TypedItem.Id == typedItem.Id);
            if (existing == null)
                return Task.CompletedTask;
            CurrentHistory.Items.Remove(existing);
            AddNext(existing);
            return Task.CompletedTask;
        }

        private async void AddNext(HistoryEntry existing)
        {
            await AddNextEpisode(existing.TypedItem as TVShowEpisode);
            await AddNextCollectionMovieAsync(existing.TypedItem as Movie);
        }

        private async Task AddNextCollectionMovieAsync(Movie movie)
        {
            if (movie.CollectionId == 0)
                return;
            var collection = await _MediaLibrary.GetMovieCollection(movie.CollectionId);
            if (collection == null)
                return;
            var movies = await _MediaLibrary.GetMovies(collection.Id);
            var nextMovie = movies
                .OrderBy(m => m.Name)
                .SkipWhile(e => e.Id != movie.Id)
                .SkipWhile(e => e.Id == movie.Id)
                .FirstOrDefault();
            if (nextMovie != null)
                await Add(null, nextMovie);
        }

        private async Task AddNextEpisode(TVShowEpisode episode)
        {
            var episodes = await _MediaLibrary.GetTVShowEpisodes(episode.SeasonId);
            var nextEpisode = episodes
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
                if (nextEpisode != null)
                {
                    episodes = await _MediaLibrary.GetTVShowEpisodes(nextSeason.Id);
                    nextEpisode = episodes.FirstOrDefault();
                }
            }

            if (nextEpisode != null)
                await Add(null, nextEpisode);
        }

    }
}
