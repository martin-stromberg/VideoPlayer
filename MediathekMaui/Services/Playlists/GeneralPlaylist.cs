using Mediathek.Services.MediaLibrary;
using System;
using System.Linq;

namespace Mediathek.Services.Playlists
{
    public class GeneralPlaylist: BaseManager
    {

        public GeneralPlaylist(string name, IMediaLibrary mediaLibrary)
            : base(mediaLibrary)
        {
            Name = name;
        }

        internal async Task InitializeAsync()
        {
            var playlist = (await MediaLibrary.GetPlaylists(PlaylistType.General)).FirstOrDefault();
            if (playlist == null)
            {
                playlist = new Playlist() { Type = PlaylistType.General, Name = Name };
                await MediaLibrary.AddPlaylistAsync(playlist);
            }
            Playlist = playlist;
        }

        public string Name { get; private set; }

        public Playlist Playlist { get; private set; }

        private async Task SavePlaylistAsync(Playlist playlist)
        {
            await MediaLibrary.AddPlaylistAsync(playlist);
        }

        private async Task<MediaItem> GetFirstMediaItem(long[] mediaItems)
        {
            MediaItem item = null;
            foreach (var mediaItemId in mediaItems)
            {
                item = await MediaLibrary.GetMediaItemAsync(mediaItemId);
                if (item == null)
                    continue;
                if (item.CopyType != MediaItemCopyType.None)
                    continue;
                break;
            }
            return item;
        }

        private int currentPlaylistCompletionSessionId = 0;

        public async Task StartPlaylist(TVShowEpisode episode, Func<IEnumerable<BaseModel>> collectionElements)
        {
            currentPlaylistCompletionSessionId = Random.Shared.Next(int.MaxValue);
            Playlist.Items.Clear();
            await AddTVShowEpisodes(episode,
                                    true,
                                    currentPlaylistCompletionSessionId,
                                    async () => { await SavePlaylistAsync(Playlist); });
        }

        public async Task StartPlaylist(TVShow show)
        {
            currentPlaylistCompletionSessionId = Random.Shared.Next(int.MaxValue);
            Playlist.Items.Clear();
            Task SaveTask = null;
            bool AsyncSave = false;
            await AddTVShowEpisodes(show,
                                    true,
                                    currentPlaylistCompletionSessionId,
                                    async () =>
                                    {
                                        SaveTask = SavePlaylistAsync(Playlist);
                                        if (AsyncSave)
                                            await SaveTask;
                                    });
            if (SaveTask != null)
                await SaveTask;
            AsyncSave = true;
        }

        public async Task StartPlaylist(TVShowSeason season)
        {
            currentPlaylistCompletionSessionId = Random.Shared.Next(int.MaxValue);
            Playlist.Items.Clear();
            Task SaveTask = null;
            bool AsyncSave = false;
            await AddTVShowSeasonEpisodes(season,
                                          true,
                                          currentPlaylistCompletionSessionId,
                                          async () =>
                                          {
                                              SaveTask = SavePlaylistAsync(Playlist);
                                              if (AsyncSave)
                                                  await SaveTask;
                                          });
            if (SaveTask != null)
                await SaveTask;
            AsyncSave = true;
        }

        private async Task AddTVShowEpisodes(TVShow show, bool startPlayback, int session, Action Finished)
        {
            bool started = startPlayback;
            bool isFirst = true;
            var seasons = (await MediaLibrary.GetTVShowSeasons(show.Id))
                .OrderBy(s => s.Name)
                .ToArray();
            var count = seasons.Count();
            Task previousTask = null;
            Task nextTask = null;
            foreach (var season in seasons)
            {
                if (session != currentPlaylistCompletionSessionId)
                    return;
                while (nextTask != null)
                    await Task.Delay(100);
                nextTask = AddTVShowSeasonEpisodes(season,
                                                   startPlayback && isFirst,
                                                   session,
                                                   async () =>
                                                   {
                                                       count -= 1;
                                                       if (count == 0)
                                                           Finished();
                                                       else if (nextTask != null)
                                                       {
                                                           var currentTask = previousTask;
                                                           previousTask = nextTask;
                                                           if (currentTask != null)
                                                               await currentTask;
                                                           nextTask = null;
                                                       }
                                                   });
                if (isFirst || !started)
                {
                    await nextTask;
                    nextTask = null;
                }
                isFirst = false;
            }
            if (isFirst)
                Finished();
        }

        private async Task AddTVShowEpisodes(TVShowEpisode episode, bool startPlayback, int session, Action Finished)
        {
            bool started = startPlayback;
            bool isFirst = true;
            var season = await MediaLibrary.GetTVShowSeason(episode.SeasonId);

            var episodes = (await MediaLibrary.GetTVShowEpisodes(season.Id))
                .OrderBy(e => e.EpisodeNo)
                .SkipWhile(e => e.EpisodeNo != episode.EpisodeNo)
                .ToArray();
            var count = episodes.Count();

            Task previousTask = null;
            Task nextTask = null;
            foreach (var currEpisode in episodes)
            {
                if (session != currentPlaylistCompletionSessionId)
                    return;
                while (nextTask != null)
                    await Task.Delay(100);
                nextTask = AddTVShowEpisode(currEpisode,
                                            session,
                                            async () =>
                                            {
                                                count -= 1;
                                                if (count == 0)
                                                    Finished();
                                                else if (nextTask != null)
                                                {
                                                    var currentTask = previousTask;
                                                    previousTask = nextTask;
                                                    if (currentTask != null)
                                                        await currentTask;
                                                    nextTask = null;
                                                }
                                            });
                if (isFirst || !started)
                {
                    await nextTask;
                    nextTask = null;
                }
                isFirst = false;
            }
            if (isFirst)
                Finished();
        }

        private async Task AddTVShowSeasonEpisodes(
            TVShowSeason season,
            bool startPlayback,
            int session,
            Action Finished)
        {
            bool started = startPlayback;
            bool isFirst = true;
            var episodes = (await MediaLibrary.GetTVShowEpisodes(season.Id))
                .OrderBy(e => e.EpisodeNo)
                .ToArray();
            var count = episodes.Count();
            Task previousTask = null;
            Task nextTask = null;
            foreach (var episode in episodes)
            {
                if (session != currentPlaylistCompletionSessionId)
                    return;
                while (nextTask != null)
                    await Task.Delay(100);
                nextTask = AddTVShowEpisode(episode,
                                            session,
                                            async () =>
                                            {
                                                count -= 1;
                                                if (count == 0)
                                                    Finished();
                                                else if (nextTask != null)
                                                {
                                                    var currentTask = previousTask;
                                                    previousTask = nextTask;
                                                    if (currentTask != null)
                                                        await currentTask;
                                                    nextTask = null;
                                                }
                                            });
                if (isFirst || !started)
                {
                    await nextTask;
                    nextTask = null;
                }
                isFirst = false;
            }
            if (isFirst)
                Finished();
        }

        private async Task AddTVShowEpisode(TVShowEpisode episode, int session, Action Finished)
        {
            if (session != currentPlaylistCompletionSessionId)
                return;
            var mediaItem = await GetFirstMediaItem(episode.MediaItems);
            Playlist.Add(mediaItem);
            Finished();
        }

        public async Task StartPlaylist(Movie movie)
        {
            currentPlaylistCompletionSessionId = Random.Shared.Next(int.MaxValue);
            Playlist.Items.Clear();
            await AddMovie(movie, currentPlaylistCompletionSessionId, () => { });
            await SavePlaylistAsync(Playlist);
        }

        public async Task StartPlaylist(MovieCollection movieCollection)
        {
            currentPlaylistCompletionSessionId = Random.Shared.Next(int.MaxValue);
            Playlist.Items.Clear();
            Task SaveTask = null;
            bool AsyncSave = false;
            await AddMovieCollection(movieCollection,
                                     true,
                                     currentPlaylistCompletionSessionId,
                                     async () =>
                                     {
                                         SaveTask = SavePlaylistAsync(Playlist);
                                         if (AsyncSave)
                                             await SaveTask;
                                     });
            if (SaveTask != null)
                await SaveTask;
            AsyncSave = true;
        }

        private async Task AddMovieCollection(
            MovieCollection movieCollection,
            bool startPlayback,
            int session,
            Action Finished)
        {
            var started = startPlayback;
            var isFirst = true;
            var movies = (await MediaLibrary.GetMovies(movieCollection.Id))
                .OrderBy(m => m.Date)
                .ThenBy(m => m.Name)
                .ToArray();
            var count = movies.Count();
            Task previousTask = null;
            Task nextTask = null;
            foreach (var movie in movies)
            {
                if (session != currentPlaylistCompletionSessionId)
                    return;
                while (nextTask != null)
                    await Task.Delay(100);
                nextTask = AddMovie(movie,
                                    session,
                                    async () =>
                                    {
                                        count -= 1;
                                        if (count == 0)
                                            Finished();
                                        else if (nextTask != null)
                                        {
                                            var currentTask = previousTask;
                                            previousTask = nextTask;
                                            if (currentTask != null)
                                                await currentTask;
                                            nextTask = null;
                                        }
                                    });
                if (isFirst || !started)
                {
                    await nextTask;
                    nextTask = null;
                }
                isFirst = false;
            }
            if (isFirst)
                Finished();
        }

        private async Task AddMovie(Movie movie, int session, Action Finished)
        {
            if (session != currentPlaylistCompletionSessionId)
                return;
            var mediaItem = await GetFirstMediaItem(movie.MediaItems);
            Playlist.Add(mediaItem);
            Finished();
        }

        public async Task StartPlaylist(MediaItem mediaItem)
        {
            currentPlaylistCompletionSessionId = Random.Shared.Next(int.MaxValue);
            Playlist.Items.Clear();
            Playlist.Add(mediaItem);
            await SavePlaylistAsync(Playlist);
        }

        public async Task ProcessMediaEndedAsync(MediaItem item)
        {
            Playlist.RemoveUpTo(item);
            if (item.CopyType == MediaItemCopyType.Cache)
                await MediaLibrary.RemoveMediaItemAsync(item);
            await SavePlaylistAsync(Playlist);
        }

    }
}
