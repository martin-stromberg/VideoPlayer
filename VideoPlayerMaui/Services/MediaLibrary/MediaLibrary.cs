using VideoPlayer.Extensions;
using VideoPlayer.Models;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Models.Movies;
using VideoPlayer.Models.PlaybackHistory;
using VideoPlayer.Models.Playlists;
using VideoPlayer.Models.Sources;
using VideoPlayer.Models.TVShows;
using VideoPlayer.Services.Database;

namespace VideoPlayer.Services.MediaLibrary
{
    public class MediaLibrary: IMediaLibrary
    {

        private readonly IMediaLibraryDatabase _DataStore;
        private readonly MediaLibrarySettings _Settings;

        public MediaLibrary(
            IMediaLibraryDatabase dataStore,
            MediaLibrarySettings settings)
        {
            _Settings = settings;
            _DataStore = dataStore;
        }

        public event EventHandler<BaseModelEventArgs> ModelElementAdded;

        public event EventHandler<BaseModelEventArgs> ModelElementRemoved;

        public event EventHandler<BaseModelEventArgs> ModelElementUpdated;

        private async Task ClearMovie(Database.Models.Movie dbItem)
        {
            foreach (var assignment in await _DataStore.GetMovieMediaItems(dbItem.Id))
                await _DataStore.RemoveMovieMediaItemAsync(assignment);
        }

        private async Task ClearTVShow(Database.Models.TVShow dbItem)
        {
            var seasons = await _DataStore.GetTVShowSeasons(dbItem.Id);
            foreach (var season in seasons)
            {
                await ClearTVShowSeason(season);
            }
        }

        private async Task ClearTVShowEpisode(Database.Models.TVShowEpisode episode)
        {
            foreach (var assignment in await _DataStore.GetTVShowMediaItemsForMediaItem(episode.Id))
                await _DataStore.RemoveMovieMediaItemAsync(assignment);
        }

        private async Task ClearTVShowSeason(Database.Models.TVShowSeason season)
        {
            var episodes = await _DataStore.GetTVShowEpisodes(season.Id);
            foreach (var episode in episodes)
                await ClearTVShowEpisode(episode);
        }

        private IEnumerable<TVShowEpisode> FindTVShowEpisodes(long id)
        {
            var episodes = _DataStore
                .GetTVShowEpisodes(id)
                .Wait<IEnumerable<Database.Models.TVShowEpisode>>();
            return episodes
                .Select(e =>
                {
                    var episode = TVShowEpisode.FromDataModel(e).UpdatePicture(_Settings.CacheRootPath) as TVShowEpisode;
                    var mediaItems = _DataStore
                        .GetTVShowEpisodeMediaItems(episode.Id)
                        .Wait<IEnumerable<Database.Models.TVShowEpisodeMediaItem>>()
                        .Select(mi => mi.Id);
                    episode.MediaItems = mediaItems.ToArray();
                    return episode;
                })
                .Cast<TVShowEpisode>();
        }

        private IEnumerable<TVShowSeason> FindTVShowSeasons(long showId)
        {
            var seasons = _DataStore
                .GetTVShowSeasons(showId)
                .Wait<IEnumerable<Database.Models.TVShowSeason>>();
            return seasons
                .Select(s =>
                {
                    var season = TVShowSeason.FromDataModel(s).UpdatePicture(_Settings.CacheRootPath) as TVShowSeason;
                    return season;
                })
                .Cast<TVShowSeason>();
        }

        protected void OnElementChanged(
            BaseModelEventArgs modelElementAdded,
            BaseModelEventArgs modelElementUpdated,
            BaseModelEventArgs modelElementRemoved)
        {
            if (modelElementAdded != null)
                ModelElementAdded?.Invoke(this, modelElementAdded);
            if (modelElementUpdated != null)
                ModelElementUpdated?.Invoke(this, modelElementUpdated);
            if (modelElementRemoved != null)
                ModelElementRemoved?.Invoke(this, modelElementRemoved);
        }

        public async Task AddMovieAsync(Movie movie)
        {
            var isNew = movie.Id == 0;
            var dataModel = movie.ToDataModelAsync();
            var dataModelMediaItems = movie.MediaItems
                                           .Select(id =>
                                                   new Database.Models.MovieMediaItem()
                                           {
                                               MovieId = movie.Id,
                                               MediaItemId = id
                                           });
            if (!isNew)
                await _DataStore.RemoveMovieMediaItemsAsync(movie.Id);
            await _DataStore.AddOrUpdateMovie(dataModel as Database.Models.Movie);
            movie.UpdateAutoincrements(dataModel);
            foreach (var mediaItem in dataModelMediaItems)
            {
                mediaItem.MovieId = movie.Id;
                await _DataStore.AddMovieMediaItem(mediaItem);
            }
            OnElementChanged(isNew ? (new BaseModelEventArgs(movie)) : null,
                             (!isNew) ? (new BaseModelEventArgs(movie)) : null,
                             null);
        }

        public async Task AddMovieCollectionAsync(MovieCollection collection)
        {
            var isNew = collection.Id == 0;
            var dataModelShow = collection.ToDataModelAsync() as Database.Models.MovieCollection;
            await _DataStore.AddOrUpdateMovieCollection(dataModelShow);
            collection.UpdateAutoincrements(dataModelShow);
            OnElementChanged(isNew ? (new BaseModelEventArgs(collection)) : null,
                             (!isNew) ? (new BaseModelEventArgs(collection)) : null,
                             null);
        }

        public async Task AddTVShowAsync(TVShow show)
        {
            var isNew = show.Id == 0;
            var dataModelShow = show.ToDataModelAsync();
            await _DataStore.AddOrUpdateTVShow(dataModelShow as Database.Models.TVShow);
            show.UpdateAutoincrements(dataModelShow);
            OnElementChanged(isNew ? (new BaseModelEventArgs(show)) : null,
                             (!isNew) ? (new BaseModelEventArgs(show)) : null,
                             null);
        }

        public async Task AddTVShowEpisodeAsync(TVShow show, TVShowSeason season, TVShowEpisode episode)
        {
            var isNew = episode.Id == 0;
            var dataModelEpisode = episode.ToDataModelAsync() as Database.Models.TVShowEpisode;
            var dataModelMediaItems = episode.MediaItems
                                             .Select(id =>
                                                     new Database.Models.TVShowEpisodeMediaItem()
                                             {
                                                 EpisodeId = episode.Id,
                                                 MediaItemId = id
                                             });
            dataModelEpisode.SeasonId = season.Id;
            if (!isNew)
                await _DataStore.RemoveTVShowEpisodeMediaItemsAsync(episode.Id);
            await _DataStore.AddOrUpdateTVShowEpisode(dataModelEpisode);
            episode.UpdateAutoincrements(dataModelEpisode);
            foreach (var mediaItem in dataModelMediaItems)
            {
                mediaItem.EpisodeId = episode.Id;
                await _DataStore.AddTVShowEpisodeMediaItem(mediaItem);
            }
            OnElementChanged(isNew ? (new BaseModelEventArgs(episode)) : null,
                             (!isNew) ? (new BaseModelEventArgs(episode)) : null,
                             null);
        }

        public async Task AddTVShowSeasonAsync(TVShow show, TVShowSeason season)
        {
            var isNew = season.Id == 0;
            season.ShowId = show.Id;
            var dataModelSeason = season.ToDataModelAsync() as Database.Models.TVShowSeason;
            dataModelSeason.ShowId = show.Id;
            await _DataStore.AddOrUpdateTVShowSeason(dataModelSeason);
            season.UpdateAutoincrements(dataModelSeason);
            OnElementChanged(isNew ? (new BaseModelEventArgs(season)) : null,
                             (!isNew) ? (new BaseModelEventArgs(season)) : null,
                             null);
        }

        public async Task<Movie> FindMovieAsync(long mediaItemId)
        {
            var movie = await _DataStore.GetMovieByMediaItem(mediaItemId);
            if (movie == null)
                return null;
            var mediaItems = await _DataStore.GetMovieMediaItems(movie.Id);
            return (Movie
                .FromDataModel(movie)
                .UpdatePicture(_Settings.CacheRootPath) as Movie)
                .SetMediaItems(mediaItems);
        }

        public async Task<IEnumerable<MovieCollection>> FindMovieCollectionByNameAsync(string name)
        {
            return (await _DataStore.GetMovieCollectionsByName(name))
                .Select(coll =>
                        MovieCollection.FromDataModel(coll).UpdatePicture(_Settings.CacheRootPath) as MovieCollection);
        }

        public async Task<MovieCollection> GetMovieCollection(Movie movie)
        {
            var dbItem = await _DataStore.GetMovieCollection(movie.CollectionId);
            return MovieCollection.FromDataModel(dbItem).UpdatePicture(_Settings.CacheRootPath) as MovieCollection;
        }

        public async Task<TVShow> FindTVShowAsync(long id)
        {
            var show = await _DataStore
                .GetTVShow(id);
            if (show == null)
                return null;
            return TVShow.FromDataModel(show).UpdatePicture(_Settings.CacheRootPath) as TVShow;
        }

        public async Task<IEnumerable<TVShow>> FindTVShowByNameAsync(string name)
        {
            var show = await _DataStore.GetTVShowsByName(name);
            return show
                .Select(show =>
                {
                    var model = TVShow.FromDataModel(show).UpdatePicture(_Settings.CacheRootPath) as TVShow;
                    return model;
                })
                .Cast<TVShow>();
        }

        public async Task<Movie> GetMovie(long id)
        {
            var movie = await _DataStore.GetMovie(id);
            var mediaItems = await _DataStore.GetMovieMediaItems(movie.Id);
            return (Movie.FromDataModel(movie).UpdatePicture(_Settings.CacheRootPath) as Movie)
                .SetMediaItems(mediaItems);
        }

        public async Task<MovieCollection> GetMovieCollection(long id)
        {
            return MovieCollection.FromDataModel(await _DataStore.GetMovieCollection(id))
                                  .UpdatePicture(_Settings.CacheRootPath) as MovieCollection;
        }

        public async Task<IEnumerable<MovieCollection>> GetMovieCollections()
        {
            var collections = await _DataStore.GetMovieCollections();
            return collections
                .Select(collection =>
                {
                    var model = MovieCollection.FromDataModel(collection).UpdatePicture(_Settings.CacheRootPath) as MovieCollection;
                    return model;
                })
                .Cast<MovieCollection>();
        }

        public async Task<IEnumerable<Movie>> GetMovies()
        {
            var movies = (await _DataStore.GetMovies())
                .Select(movie => Movie.FromDataModel(movie).UpdatePicture(_Settings.CacheRootPath) as Movie)
                .ToArray();
            foreach (var movie in movies)
            {
                var mediaItems = await _DataStore.GetMovieMediaItems(movie.Id);
                movie.SetMediaItems(mediaItems);
            }
            return movies;
        }

        public async Task<IEnumerable<Movie>> GetMovies(long collectionId)
        {
            var movies = (await _DataStore.GetMovies())
                .Where(movie => movie.CollectionId == collectionId)
                .Select(movie => Movie.FromDataModel(movie).UpdatePicture(_Settings.CacheRootPath) as Movie)
                .ToArray();
            foreach (var movie in movies)
            {
                var mediaItems = await _DataStore.GetMovieMediaItems(movie.Id);
                movie.SetMediaItems(mediaItems);
            }
            return movies;
        }

        private async Task<Playlist> CompletePlaylistAsync(Playlist playlist)
        {
            var entries = await _DataStore.GetPlaylistEntries(playlist.Id);
            foreach (var entry in entries)
            {
                var playlistEntry = PlaylistEntry.FromDataModel(entry) as PlaylistEntry;
                playlistEntry.Item = await GetMediaItemAsync(entry.MediaItemId);
                playlist.Add(playlistEntry);
            }
            return playlist;
        }

        public async Task<IEnumerable<Playlist>> GetPlaylists(PlaylistType type)
        {
            var playlists = (await _DataStore.GetPlaylists())
                .Where(playlist => playlist.Type == ((int)type))
                .Select(playlist => Playlist.FromDataModel(playlist) as Playlist)
                .ToArray();
            foreach (var playlist in playlists)
            {
                await CompletePlaylistAsync(playlist);
            }
            return playlists;
        }

        public async Task<Playlist> GetPlaylist(long id)
        {
            var playlist = Playlist.FromDataModel(await _DataStore.GetPlaylist(id)) as Playlist;
            return await CompletePlaylistAsync(playlist);
        }

        public async Task<IEnumerable<Playlist>> GetPlaylists()
        {
            var playlists = (await _DataStore.GetPlaylists())
                .Select(playlist => Playlist.FromDataModel(playlist) as Playlist)
                .ToArray();
            foreach (var playlist in playlists)
            {
                await CompletePlaylistAsync(playlist);
            }
            return playlists;
        }

        public async Task<TVShow> GetTVShow(long id)
        {
            var show = await _DataStore.GetTVShow(id);
            return TVShow.FromDataModel(show).UpdatePicture(_Settings.CacheRootPath) as TVShow;
        }

        public async Task<TVShowEpisode> GetTVShowEpisode(long id)
        {
            var episode = await _DataStore.GetTVShowEpisode(id);
            var mediaItems = await _DataStore.GetTVShowEpisodeMediaItems(episode.Id);
            return (TVShowEpisode.FromDataModel(episode).UpdatePicture(_Settings.CacheRootPath) as TVShowEpisode)
                .SetMediaItems(mediaItems);
        }

        public async Task<IEnumerable<TVShowEpisode>> GetTVShowEpisodes(long seasonId)
        {
            var dbEpisodes = await _DataStore.GetTVShowEpisodes(seasonId);
            var episodes = dbEpisodes
                .Select(episode =>
                {
                    var model = TVShowEpisode.FromDataModel(episode).UpdatePicture(_Settings.CacheRootPath) as TVShowEpisode;
                    return model;
                })
                .Cast<TVShowEpisode>()
                .ToArray();
            foreach (var episode in episodes)
            {
                var mediaItems = await _DataStore.GetTVShowEpisodeMediaItems(episode.Id);
                episode.SetMediaItems(mediaItems);
            }
            return episodes;
        }

        public async Task<IEnumerable<TVShow>> GetTVShows()
        {
            var shows = await _DataStore.GetTVShows();
            return shows
                .Select(show =>
                {
                    var model = TVShow.FromDataModel(show).UpdatePicture(_Settings.CacheRootPath) as TVShow;
                    return model;
                })
                .Cast<TVShow>();
        }

        public async Task<TVShowSeason> GetTVShowSeason(long id)
        {
            var season = await _DataStore.GetTVShowSeason(id);
            return TVShowSeason.FromDataModel(season).UpdatePicture(_Settings.CacheRootPath) as TVShowSeason;
        }

        public async Task<IEnumerable<TVShowSeason>> GetTVShowSeasons(long showId)
        {
            var seasons = await _DataStore.GetTVShowSeasons(showId);
            return seasons
                .Select(season =>
                {
                    var model = TVShowSeason.FromDataModel(season).UpdatePicture(_Settings.CacheRootPath) as TVShowSeason;
                    return model;
                })
                .Cast<TVShowSeason>();
        }

        public async Task ImportAsync(IMediaLibrary library)
        {
            foreach (var fromSource in await library.GetSourcesAsync())
            {
                fromSource.Id = 0;
                await AddSourceAsync(fromSource);
            }

            // ToDo: Auch die MediaItems und MediaCollections
        }

        public async Task<bool> IsEmptyAsync()
        {
            return (await (await _DataStore.GetSourcesAsync()).FirstOrDefaultAsync()) == null;
        }

        public async Task RemoveMediaItemAsync(MediaItem mediaItem)
        {
            var mediaStore = await _DataStore.GetMediaItemAsync(mediaItem.Id);
            await ClearMediaItem(mediaStore);
            await _DataStore.RemoveMediaItem(mediaStore);
        }

        public async Task RemoveMovieAsync(Movie movie)
        {
            var dbItem = await _DataStore.GetMovie(movie.Id);
            await ClearMovie(dbItem);
            await _DataStore.RemoveMovie(dbItem.Id);
        }

        public async Task RemoveTVShowAsync(TVShow show)
        {
            var dbItem = await _DataStore.GetTVShow(show.Id);
            await ClearTVShow(dbItem);
            await _DataStore.RemoveTVShow(dbItem.Id);
        }

        #region Sources
        public async Task<IEnumerable<MediaSource>> GetSourcesAsync()
        {
            return (await (await _DataStore.GetSourcesAsync())
                .OrderBy(s => s.Name)
                .ToArrayAsync())
                .Select(source => MediaSource.FromDataModel(source) as MediaSource);
        }

        public async Task<MediaSource> GetSourceAsync(long id)
        {
            return MediaSource.FromDataModel(await _DataStore.GetSourceAsync(id)) as MediaSource;
        }

        public async Task AddSourceAsync(MediaSource source)
        {
            var isNew = source.Id == 0;
            var dataModel = source.ToDataModelAsync();
            await _DataStore.AddOrUpdateSourceAsync(dataModel as Services.Database.Models.MediaSource);
            source.UpdateAutoincrements(dataModel);
            OnElementChanged(isNew ? (new BaseModelEventArgs(source)) : null,
                             (!isNew) ? (new BaseModelEventArgs(source)) : null,
                             null);
        }

        public async Task RemoveMediaSourceAsync(MediaSource mediaItem)
        {
            var dbItem = await _DataStore.GetSourceAsync(mediaItem.Id);
            await ClearSourceMediaAsync(dbItem);
            await _DataStore.RemoveSource(dbItem);
            OnElementChanged(null, null, new BaseModelEventArgs(mediaItem));
        }
        #endregion

        #region Media Collections
        public async Task<IEnumerable<MediaItemCollection>> GetMediaItemCollectionsAsync(long SourceId)
        {
            return (await (await _DataStore.GetMediaCollectionsAsync())
                .Where(s => s.MediaSourceId == SourceId)
                .OrderBy(s => s.Name)
                .ToArrayAsync())
                .Select(source => MediaItemCollection.FromDataModel(source) as MediaItemCollection);
        }

        public async Task<IEnumerable<MediaItemCollection>> GetAllMediaItemCollectionsAsync()
        {
            return (await (await _DataStore.GetMediaCollectionsAsync())
                .OrderBy(s => s.Name)
                .ToArrayAsync())
                .Select(source => MediaItemCollection.FromDataModel(source) as MediaItemCollection);
        }

        public async Task RemoveMediaItemCollection(MediaItemCollection collection)
        {
            var dbItem = await _DataStore.GetMediaCollectionAsync(collection.Id);
            await ClearCollectionMediaAsync(dbItem);
            await _DataStore.RemoveMediaCollection(dbItem);
            OnElementChanged(null, null, new BaseModelEventArgs(collection));
        }

        public async Task<MediaItemCollection> GetMediaItemCollectionAsync(long Id)
        {
            return MediaItemCollection.FromDataModel(await _DataStore.GetMediaCollectionAsync(Id)) as MediaItemCollection;
        }

        public async Task<IEnumerable<MediaItemCollection>> GetChildMediaItemCollectionsAsync(long collectionId)
        {
            return (await (await _DataStore.GetMediaCollectionsAsync())
                .Where(s => s.ParentCollectionId == collectionId)
                .OrderBy(s => s.Name)
                .ToArrayAsync())
                .Select(source => MediaItemCollection.FromDataModel(source) as MediaItemCollection);
        }

        public async Task<MediaItemCollection> FindMediaItemCollectionAsync(long sourceId, string path)
        {
            return MediaItemCollection.FromDataModel(await (await _DataStore.GetMediaCollectionsAsync())
                .FirstOrDefaultAsync(item => (item.MediaSourceId == sourceId)
                    && (item.Path == path))) as MediaItemCollection;
        }

        public async Task AddMediaItemCollectionAsync(MediaItemCollection collection)
        {
            var isNew = collection.Id == 0;
            var dataModel = collection.ToDataModelAsync();
            await _DataStore.AddOrUpdateMediaCollectionAsync(dataModel as Services.Database.Models.MediaCollection);
            collection.UpdateAutoincrements(dataModel);
            OnElementChanged(isNew ? (new BaseModelEventArgs(collection)) : null,
                             (!isNew) ? (new BaseModelEventArgs(collection)) : null,
                             null);
        }
        #endregion

        #region Media Items
        public async Task<MediaItem> GetMediaItemAsync(long id)
        {
            return MediaItem.FromDataModel(await _DataStore.GetMediaItemAsync(id))?.UpdatePicture(_Settings.CacheRootPath) as MediaItem;
        }

        public async Task<IEnumerable<MediaItem>> GetAllMediaItems()
        {
            return (await (await _DataStore.GetMediaItemsAsync())
                .OrderBy(s => s.Name)
                .ToArrayAsync())
                .Select(source => MediaItem.FromDataModel(source).UpdatePicture(_Settings.CacheRootPath) as MediaItem);
        }

        public async Task<IEnumerable<MediaItem>> GetMediaItemsAsync(long CollectionId)
        {
            return (await (await _DataStore.GetMediaItemsAsync())
                .Where(s => (s.ParentCollectionId == CollectionId) && (s.OriginalMediaItemId == 0))
                .OrderBy(s => s.Name)
                .ToArrayAsync())
                .Select(source => MediaItem.FromDataModel(source).UpdatePicture(_Settings.CacheRootPath) as MediaItem);
        }

        public async Task<MediaItem> FindMediaItemAsync(long SourceId, string path)
        {
            var items = await (await _DataStore.GetMediaItemsAsync())
                .Where(item => item.Path == path)
                .ToArrayAsync();
            return items
                .Where(item =>
                {
                    var collection = _DataStore
                        .GetMediaCollectionAsync(item.ParentCollectionId)
                        .Wait<Services.Database.Models.MediaCollection>();
                    return collection.MediaSourceId == SourceId;
                })
                .Select(item => MediaItem.FromDataModel(item).UpdatePicture(_Settings.CacheRootPath) as MediaItem)
                .FirstOrDefault();
        }

        public async Task AddMediaItemAsync(MediaItem mediaItem)
        {
            var isNew = mediaItem.Id == 0;
            var dataModel = mediaItem.ToDataModelAsync();
            await _DataStore.AddOrUpdateMediaItemAsync(dataModel as Database.Models.MediaItem);
            mediaItem.UpdateAutoincrements(dataModel);
            OnElementChanged(isNew ? (new BaseModelEventArgs(mediaItem)) : null,
                             (!isNew) ? (new BaseModelEventArgs(mediaItem)) : null,
                             null);
        }

        public async Task UpdateMediaItemAsync(MediaItem mediaItem, bool notify)
        {
            var isNew = mediaItem.Id == 0;
            var dataModel = mediaItem.ToDataModelAsync();
            await _DataStore.AddOrUpdateMediaItemAsync(dataModel as Database.Models.MediaItem);
            mediaItem.UpdateAutoincrements(dataModel);
            OnElementChanged(isNew ? (new BaseModelEventArgs(mediaItem)) : null,
                             (!isNew && notify) ? (new BaseModelEventArgs(mediaItem)) : null,
                             null);
        }

        public async Task<IEnumerable<MediaItem>> GetAlternateMediaItemsAsync(long mediaItemId)
        {
            return (await (await _DataStore.GetMediaItemsAsync())
                .Where(s => s.OriginalMediaItemId == mediaItemId)
                .OrderBy(s => s.Name)
                .ToArrayAsync())
                .Select(source => MediaItem.FromDataModel(source).UpdatePicture(_Settings.CacheRootPath) as MediaItem);
        }

        public async Task<MediaItem> GetOriginalMediaItemsAsync(MediaItem item)
        {
            return await GetMediaItemAsync(item.OriginalMediaItemId);
        }
        #endregion 


        #region Clear
        public async Task ClearMedia()
        {
            foreach (var movie in await _DataStore.GetMovies())
                await _DataStore.RemoveMovie(movie.Id);
            foreach (var show in await _DataStore.GetTVShows())
                await _DataStore.RemoveTVShow(show.Id);

            foreach (var source in await (await _DataStore.GetSourcesAsync()).ToArrayAsync())
            {
                await ClearSourceMediaAsync(source);
                var mediaSource = await GetSourceAsync(source.Id);
                mediaSource.ResetScan();
                await AddSourceAsync(mediaSource);
            }
        }

        private async Task ClearSourceMediaAsync(Services.Database.Models.MediaSource source)
        {
            var collStore = await _DataStore.GetMediaCollectionsAsync();
            var collections = await collStore.Where(c => c.MediaSourceId == source.Id).ToArrayAsync();
            foreach (var coll in collections)
            {
                ClearCaches(coll);
                await ClearCollectionMediaAsync(coll);
                await _DataStore.RemoveMediaCollection(coll);
            }
        }

        private async Task ClearCollectionMediaAsync(Services.Database.Models.MediaCollection coll)
        {
            var mediaStore = await _DataStore.GetMediaItemsAsync();
            var mediaItems = await mediaStore.Where(mi => mi.ParentCollectionId == coll.Id).ToArrayAsync();
            foreach (var mediaItem in mediaItems)
            {
                await ClearMediaItem(mediaItem);
            }
        }

        private async Task ClearMediaItem(Services.Database.Models.MediaItem mediaItem)
        {
            ClearCaches(mediaItem);
            foreach (var assignment in await _DataStore.GetMovieMediaItemsForMediaItem(mediaItem.Id))
                await _DataStore.RemoveMovieMediaItemAsync(assignment);
            foreach (var assignment in await _DataStore.GetTVShowMediaItemsForMediaItem(mediaItem.Id))
                await _DataStore.RemoveMovieMediaItemAsync(assignment);
            await _DataStore.RemoveMediaItem(mediaItem);
        }

        private void ClearCaches(Services.Database.Models.MediaItem mediaItem)
        {
            if (File.Exists(mediaItem.PicturePath))
                File.Delete(mediaItem.PicturePath);
            if (((MediaItemCopyType)mediaItem.CopyType) == MediaItemCopyType.Cache)
                if (File.Exists(mediaItem.Path))
                    File.Delete(mediaItem.Path);
        }
        #endregion

        #region Playlist
        public async Task AddPlaylistAsync(Playlist playlist)
        {
            var isNew = playlist.Id == 0;
            var dataModel = playlist.ToDataModelAsync();
            await _DataStore.AddOrUpdatePlaylistAsync(dataModel as Database.Models.Playlist);
            playlist.UpdateAutoincrements(dataModel);

            var existingMediaItems = (await _DataStore.GetPlaylistEntries(playlist.Id)).ToArray();
            var mediaItemsToAdd = playlist.Items.ToArray();
            var mediaItemsToDelete = existingMediaItems.Skip(mediaItemsToAdd.Length).ToArray();
            var existingCollectionOffset = 0;
            foreach (var mediaItemToAdd in mediaItemsToAdd)
            {
                Database.Models.PlaylistEntry dataModel2 = mediaItemToAdd.ToDataModelAsync() as Database.Models.PlaylistEntry;
                if (existingCollectionOffset < existingMediaItems.Length)
                {
                    existingMediaItems[existingCollectionOffset].MediaItemId = dataModel2.MediaItemId;
                    dataModel2 = existingMediaItems[existingCollectionOffset];
                    existingCollectionOffset++;
                }
                await _DataStore.AddOrUpdatePlaylistEntryAsync(dataModel2);
            }
            foreach (var mediaItemToDelete in mediaItemsToDelete)
                await _DataStore.RemovePlaylistEntryAsync(mediaItemToDelete);

            OnElementChanged(isNew ? (new BaseModelEventArgs(playlist)) : null,
                             (!isNew) ? (new BaseModelEventArgs(playlist)) : null,
                             null);
        }
        #endregion

        public async Task<BaseModel> GetTypedItem(long id)
        {
            var mmi = (await _DataStore.GetMovieMediaItemsForMediaItem(id)).FirstOrDefault();
            if (mmi != null)
                return await GetMovie(mmi.MovieId);

            var tvsmi = (await _DataStore.GetTVShowMediaItemsForMediaItem(id)).FirstOrDefault();
            if (tvsmi != null)
                return await GetTVShowEpisode(tvsmi.EpisodeId);
            return null;
        }

        public async Task AddPlaybackHistory(History currentHistory)
        {
            var existingEntries = (await _DataStore.GetPlaybackHistoryEntriesAsync()).OrderBy(e => e.Id).ToList();
            for (int idx = 0; idx < currentHistory.Items.Count(); idx++)
            {
                var currentEntry = currentHistory.Items[idx];
                Database.Models.PlaybackHistoryEntry existingEntry = null;
                if (idx < existingEntries.Count)
                    existingEntry = existingEntries[idx];
                else
                {
                    existingEntry = new Database.Models.PlaybackHistoryEntry();
                    existingEntries.Add(existingEntry);
                }
                existingEntry.Deleted = false;
                existingEntry.MediaItemId = (currentEntry.Item == null) ? 0 : currentEntry.Item.Id;
                existingEntry.TypedItemId = currentEntry.TypedItem.Id;
                existingEntry.Type = currentEntry.TypedItem.GetType().Name;
            }
            for (int idx = currentHistory.Items.Count(); idx < existingEntries.Count; idx++)
                existingEntries[idx].Deleted = true;
            foreach (var entry in existingEntries)
                await _DataStore.AddOrUpdatePlaybackHistoryEntry(entry);
        }

        public async Task<IEnumerable<HistoryEntry>> GetPlayBackHistoryEntries()
        {
            var existingEntries = (await _DataStore.GetPlaybackHistoryEntriesAsync())
                .Where(e => !e.Deleted)
                .OrderBy(e => e.Id)
                .ToArray();
            var dbItems = existingEntries.Select(entry => HistoryEntry.FromDataModel(entry) as HistoryEntry).ToArray();
            foreach (var item in dbItems)
            {
                if (item.MediaItemId != 0)
                    item.Item = await GetMediaItemAsync(item.MediaItemId);
                switch (item.Type)
                {
                    case nameof(Movie):
                        item.TypedItem = await GetMovie(item.TypedItemId);
                        break;
                    case nameof(TVShowEpisode):
                        item.TypedItem = await GetTVShowEpisode(item.TypedItemId);
                        break;
                }
            }
            return dbItems;
        }

    }
}
