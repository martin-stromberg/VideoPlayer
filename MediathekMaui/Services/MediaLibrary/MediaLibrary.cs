using Mediathek.Extensions;
using Mediathek.Services.Database;
using Mediathek.Services.Database.Models;

namespace Mediathek.Services.MediaLibrary
{
    public class MediaLibrary: IMediaLibrary
    {

        private readonly IMediaLibraryDatabase _DataStore;
        private readonly MediaLibraryEnvironment _Settings;

        public MediaLibrary(
            IMediaLibraryDatabase dataStore,
            MediaLibraryEnvironment settings)
        {
            _Settings = settings;
            _DataStore = dataStore;
        }

        public event EventHandler<BaseModelEventArgs> ModelElementAdded;

        public event EventHandler<BaseModelEventArgs> ModelElementRemoved;

        public event EventHandler<BaseModelEventArgs> ModelElementUpdated;

        private async Task ClearMovie(Services.Database.Models.Movie dbItem)
        {
            foreach (var assignment in await _DataStore.GetMovieMediaItems(dbItem.Id))
                await _DataStore.RemoveMovieMediaItemAsync(assignment);
        }

        private async Task ClearTVShow(Services.Database.Models.TVShow show)
        {
            var seasons = await _DataStore.GetTVShowSeasons(show.Id);
            foreach (var season in seasons)
            {
                await ClearTVShowSeason(season);
            }
        }

        private async Task ClearTVShowEpisode(Services.Database.Models.TVShowEpisode episode)
        {
            var mediaItems = (await _DataStore.GetTVShowEpisodeMediaItems(episode.Id)).ToArray();
            await _DataStore.RemoveTVShowEpisodeMediaItemsAsync(episode.Id);
            foreach (var assignment in await _DataStore.GetTVShowMediaItemsForMediaItem(episode.Id))
                await _DataStore.RemoveMovieMediaItemAsync(assignment);

            foreach (var mediaItemAssignment in mediaItems)
            {
                var mediaItem = await _DataStore.GetMediaItemAsync(mediaItemAssignment.MediaItemId);
                var collection = await _DataStore.GetMediaCollectionAsync(mediaItem.ParentCollectionId);
                if (mediaItem != null)
                {
                    await ClearMediaItem(mediaItem);
                    await _DataStore.RemoveMediaItem(mediaItem);
                }
                while ((collection != null) && (collection.ParentCollectionId != 0))
                {
                    var remaining = (await GetMediaItemsAsync(collection.Id)).Any()
                        || (await GetChildMediaItemCollectionsAsync(collection.Id)).Any();
                    if (remaining)
                        break;
                    await ClearCollectionMediaAsync(collection);
                    await _DataStore.RemoveMediaCollection(collection);
                    collection = await _DataStore.GetMediaCollectionAsync(collection.ParentCollectionId);
                }
            }
        }

        private async Task ClearTVShowSeason(Services.Database.Models.TVShowSeason season)
        {
            var episodes = await _DataStore.GetTVShowEpisodes(season.Id);
            foreach (var episode in episodes)
                await ClearTVShowEpisode(episode);
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

        public async Task AddMovieAsync(Models.Movies.Movie movie)
        {
            var isNew = movie.Id == 0;
            var dataModel = movie.ToDataModelAsync();
            var dataModelMediaItems = movie.MediaItems
                                           .Select(id => new MovieMediaItem() { MovieId = movie.Id, MediaItemId = id });
            if (!isNew)
                await _DataStore.RemoveMovieMediaItemsAsync(movie.Id);
            await _DataStore.AddOrUpdateMovie(dataModel as Services.Database.Models.Movie);
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

        public async Task AddMovieCollectionAsync(Models.Movies.MovieCollection collection)
        {
            var isNew = collection.Id == 0;
            var dataModelShow = collection.ToDataModelAsync() as Services.Database.Models.MovieCollection;
            await _DataStore.AddOrUpdateMovieCollection(dataModelShow);
            collection.UpdateAutoincrements(dataModelShow);
            OnElementChanged(isNew ? (new BaseModelEventArgs(collection)) : null,
                             (!isNew) ? (new BaseModelEventArgs(collection)) : null,
                             null);
        }

        public async Task AddTVShowCollectionAsync(Models.TVShows.TVShowCollection collection)
        {
            var isNew = collection.Id == 0;
            var dataModelShow = collection.ToDataModelAsync();
            await _DataStore.AddOrUpdateTVShowCollection(dataModelShow as Services.Database.Models.TVShowCollection);
            collection.UpdateAutoincrements(dataModelShow);
            OnElementChanged(isNew ? (new BaseModelEventArgs(collection)) : null,
                             (!isNew) ? (new BaseModelEventArgs(collection)) : null,
                             null);
        }

        public async Task AddTVShowAsync(Models.TVShows.TVShow show)
        {
            var isNew = show.Id == 0;
            var dataModelShow = show.ToDataModelAsync();
            await _DataStore.AddOrUpdateTVShow(dataModelShow as Services.Database.Models.TVShow);
            show.UpdateAutoincrements(dataModelShow);
            OnElementChanged(isNew ? (new BaseModelEventArgs(show)) : null,
                             (!isNew) ? (new BaseModelEventArgs(show)) : null,
                             null);
        }

        public async Task AddTVShowEpisodeAsync(
            Models.TVShows.TVShow show,
            Models.TVShows.TVShowSeason season,
            Models.TVShows.TVShowEpisode episode)
        {
            var isNew = episode.Id == 0;
            var dataModelEpisode = episode.ToDataModelAsync() as Services.Database.Models.TVShowEpisode;
            var dataModelMediaItems = episode.MediaItems
                                             .Select(id =>
                                                     new TVShowEpisodeMediaItem()
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

        public async Task AddTVShowSeasonAsync(Models.TVShows.TVShow show, Models.TVShows.TVShowSeason season)
        {
            var isNew = season.Id == 0;
            season.ShowId = show.Id;
            var dataModelSeason = season.ToDataModelAsync() as Services.Database.Models.TVShowSeason;
            dataModelSeason.ShowId = show.Id;
            await _DataStore.AddOrUpdateTVShowSeason(dataModelSeason);
            season.UpdateAutoincrements(dataModelSeason);
            OnElementChanged(isNew ? (new BaseModelEventArgs(season)) : null,
                             (!isNew) ? (new BaseModelEventArgs(season)) : null,
                             null);
        }

        public async Task<Models.Movies.Movie> FindMovieAsync(long mediaItemId)
        {
            var movie = await _DataStore.GetMovieByMediaItem(mediaItemId);
            if (movie == null)
                return null;
            var mediaItems = await _DataStore.GetMovieMediaItems(movie.Id);
            return (Models.Movies.Movie.FromDataModel(movie).UpdatePicture(_Settings.CacheRootPath) as Models.Movies.Movie)
                .SetMediaItems(mediaItems);
        }

        public async Task<IEnumerable<Models.Movies.MovieCollection>> FindMovieCollectionByNameAsync(string name)
        {
            return (await _DataStore.GetMovieCollectionsByName(name))
                .Select(coll =>
                        Models.Movies.MovieCollection.FromDataModel(coll).UpdatePicture(_Settings.CacheRootPath) as Models.Movies.MovieCollection);
        }

        public async Task<Models.Movies.MovieCollection> GetMovieCollection(Models.Movies.Movie movie)
        {
            var dbItem = await _DataStore.GetMovieCollection(movie.CollectionId);
            return Models.Movies.MovieCollection.FromDataModel(dbItem).UpdatePicture(_Settings.CacheRootPath) as Models.Movies.MovieCollection;
        }

        public async Task<Models.TVShows.TVShow> FindTVShowAsync(long id)
        {
            var show = await _DataStore
                .GetTVShow(id);
            if (show == null)
                return null;
            return Models.TVShows.TVShow.FromDataModel(show).UpdatePicture(_Settings.CacheRootPath) as Models.TVShows.TVShow;
        }

        public async Task<IEnumerable<Models.TVShows.TVShow>> FindTVShowByNameAsync(string name)
        {
            var show = await _DataStore.GetTVShowsByName(name);
            return show
                .Select(show =>
                {
                    var model = Models.TVShows.TVShow.FromDataModel(show).UpdatePicture(_Settings.CacheRootPath) as Models.TVShows.TVShow;
                    return model;
                })
                .Cast<Models.TVShows.TVShow>();
        }

        public async Task<Models.Movies.Movie> GetMovie(long id)
        {
            var movie = await _DataStore.GetMovie(id);
            if (movie is null)
                return null;
            var mediaItems = await _DataStore.GetMovieMediaItems(movie.Id);
            return (Models.Movies.Movie.FromDataModel(movie).UpdatePicture(_Settings.CacheRootPath) as Models.Movies.Movie)
                .SetMediaItems(mediaItems);
        }

        public async Task<Models.Movies.MovieCollection> GetMovieCollection(long id)
        {
            return Models.Movies.MovieCollection
                                .FromDataModel(await _DataStore.GetMovieCollection(id))
                                .UpdatePicture(_Settings.CacheRootPath) as Models.Movies.MovieCollection;
        }

        public async Task<IEnumerable<Models.Movies.MovieCollection>> GetMovieCollections()
        {
            var collections = await _DataStore.GetMovieCollections();
            return collections
                .Where(collection => !collection.IsSingleMovie)
                .Select(collection =>
                {
                    var model = Models.Movies.MovieCollection
                                             .FromDataModel(collection)
                                             .UpdatePicture(_Settings.CacheRootPath) as Models.Movies.MovieCollection;
                    return model;
                })
                .Cast<Models.Movies.MovieCollection>();
        }

        public async Task<IEnumerable<Models.Movies.MovieCollection>> GetMovieCollections(int offset, int count)
        {
            var collections = await _DataStore.GetMovieCollections();
            return collections
                .Where(collection => !collection.IsSingleMovie)
                .OrderBy(show => show.Name)
                .Skip(offset)
                .Take(count)
                .Select(collection =>
                {
                    var model = Models.Movies.MovieCollection
                                             .FromDataModel(collection)
                                             .UpdatePicture(_Settings.CacheRootPath) as Models.Movies.MovieCollection;
                    return model;
                })
                .Cast<Models.Movies.MovieCollection>();
        }

        public async Task<IEnumerable<Models.Movies.Movie>> GetMovies()
        {
            var movies = (await _DataStore.GetMovies())
                .Select(movie =>
                        Models.Movies.Movie.FromDataModel(movie).UpdatePicture(_Settings.CacheRootPath) as Models.Movies.Movie)
                .ToArray();
            foreach (var movie in movies)
            {
                var mediaItems = await _DataStore.GetMovieMediaItems(movie.Id);
                movie.SetMediaItems(mediaItems);
            }
            return movies;
        }

        public async Task<IEnumerable<Models.Movies.Movie>> GetMovies(long collectionId, int offset, int count)
        {
            var movies = (await _DataStore.GetMovies())
                .Where(movie =>
                       (movie.CollectionId == collectionId) || (movie.IsSingleCollectionMovie && (collectionId == 0)))
                .OrderBy(show => show.Name)
                .Skip(offset)
                .Take(count)
                .Select(movie =>
                        Models.Movies.Movie.FromDataModel(movie).UpdatePicture(_Settings.CacheRootPath) as Models.Movies.Movie)
                .ToArray();
            foreach (var movie in movies)
            {
                var mediaItems = await _DataStore.GetMovieMediaItems(movie.Id);
                movie.SetMediaItems(mediaItems);
            }
            return movies;
        }

        public async Task<IEnumerable<Models.Movies.Movie>> GetMovies(long collectionId)
        {
            var movies = (await _DataStore.GetMovies())
                .Where(movie =>
                       (movie.CollectionId == collectionId) || (movie.IsSingleCollectionMovie && (collectionId == 0)))
                .Select(movie =>
                        Models.Movies.Movie.FromDataModel(movie).UpdatePicture(_Settings.CacheRootPath) as Models.Movies.Movie)
                .ToArray();
            foreach (var movie in movies)
            {
                var mediaItems = await _DataStore.GetMovieMediaItems(movie.Id);
                movie.SetMediaItems(mediaItems);
            }
            return movies;
        }

        private async Task<Models.Playlists.Playlist> CompletePlaylistAsync(Models.Playlists.Playlist playlist)
        {
            if (playlist is null)
                return playlist;
            var entries = await _DataStore.GetPlaylistEntries(playlist.Id);
            foreach (var entry in entries)
            {
                var playlistEntry = Models.Playlists.PlaylistEntry.FromDataModel(entry) as Models.Playlists.PlaylistEntry;
                playlistEntry.Item = await GetMediaItemAsync(entry.MediaItemId);
                playlist.Add(playlistEntry);
            }
            return playlist;
        }

        public async Task<IEnumerable<Models.Playlists.Playlist>> GetPlaylists(PlaylistType type)
        {
            var playlists = (await _DataStore.GetPlaylists())
                .Where(playlist => playlist.Type == ((int)type))
                .Select(playlist => Models.Playlists.Playlist.FromDataModel(playlist) as Models.Playlists.Playlist)
                .ToArray();
            foreach (var playlist in playlists)
            {
                await CompletePlaylistAsync(playlist);
            }
            return playlists;
        }

        public async Task<Models.Playlists.Playlist> GetPlaylist(long id)
        {
            var playlist = Models.Playlists.Playlist.FromDataModel(await _DataStore.GetPlaylist(id)) as Models.Playlists.Playlist;
            return await CompletePlaylistAsync(playlist);
        }

        public async Task<IEnumerable<Models.Playlists.Playlist>> GetPlaylists()
        {
            var playlists = (await _DataStore.GetPlaylists())
                .Select(playlist => Models.Playlists.Playlist.FromDataModel(playlist) as Models.Playlists.Playlist)
                .ToArray();
            foreach (var playlist in playlists)
            {
                await CompletePlaylistAsync(playlist);
            }
            return playlists;
        }

        public async Task<Models.TVShows.TVShow> GetTVShow(long id)
        {
            var show = await _DataStore.GetTVShow(id);
            return Models.TVShows.TVShow.FromDataModel(show).UpdatePicture(_Settings.CacheRootPath) as Models.TVShows.TVShow;
        }

        private async Task<Services.Database.Models.TVShowEpisode> CompleteEpisodeAsync(Services.Database.Models.TVShowEpisode episode)
        {
            var changed = false;
            Services.Database.Models.TVShowSeason season = null;
            if (string.IsNullOrWhiteSpace(episode.SeasonName))
            {
                season = await _DataStore.GetTVShowSeason(episode.SeasonId);
                episode.SeasonName = season.Name;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(episode.ShowName))
            {
                if (season == null)
                    season = await _DataStore.GetTVShowSeason(episode.SeasonId);
                var show = await _DataStore.GetTVShow(season.ShowId);
                episode.ShowName = show.Name;
                changed = true;
            }
            if (changed)
                await _DataStore.AddOrUpdateTVShowEpisode(episode);
            return episode;
        }

        private async Task SetEpisodeMediaItems(
            Models.TVShows.TVShowEpisode episode,
            Models.MediaItems.MediaItem primMediaItem,
            Models.MediaItems.MediaItem downloadMediaItem,
            IEnumerable<TVShowEpisodeMediaItem> mediaItems)
        {
            mediaItems = mediaItems.ToArray();
            episode.PrimaryMediaItem = primMediaItem;
            episode.DownloadMediaItem = downloadMediaItem;
            episode = episode.SetMediaItems(mediaItems);
            await Task.CompletedTask;
        }

        public async Task<Models.TVShows.TVShowEpisode> GetTVShowEpisode(long id)
        {
            var episode = await _DataStore.GetTVShowEpisode(id);
            if (episode is null)
                return null;
            episode = await CompleteEpisodeAsync(episode);
            var mediaItems = await _DataStore.GetTVShowEpisodeMediaItems(episode.Id);
            var modelEpisode = Models.TVShows.TVShowEpisode
                                             .FromDataModel(episode)
                                             .UpdatePicture(_Settings.CacheRootPath) as Models.TVShows.TVShowEpisode;
            var primMediaItem = await GetMediaItemAsync(episode.PrimaryMediaItemId);
            var downloadMediaItem = await GetMediaItemAsync(episode.DownloadMediaItemId);
            await SetEpisodeMediaItems(modelEpisode, primMediaItem, downloadMediaItem, mediaItems);
            return modelEpisode;
        }

        public async Task<IEnumerable<Models.TVShows.TVShowEpisode>> GetTVShowEpisodes(long seasonId)
        {
            var dbEpisodes = (await _DataStore.GetTVShowEpisodes(seasonId)).ToArray();
            foreach (var episode in dbEpisodes)
                await CompleteEpisodeAsync(episode);
            var episodes = dbEpisodes
                .Select(episode =>
                {
                    var model = Models.TVShows.TVShowEpisode
                                              .FromDataModel(episode)
                                              .UpdatePicture(_Settings.CacheRootPath) as Models.TVShows.TVShowEpisode;
                    return model;
                })
                .Cast<Models.TVShows.TVShowEpisode>()
                .ToArray();
            foreach (var episode in episodes)
            {
                var mediaItems = await _DataStore.GetTVShowEpisodeMediaItems(episode.Id);
                var primMediaItem = await GetMediaItemAsync(episode.PrimaryMediaItem?.Id ?? 0);
                var downloadMediaItem = await GetMediaItemAsync(episode.DownloadMediaItem?.Id ?? 0);
                await SetEpisodeMediaItems(episode, primMediaItem, downloadMediaItem, mediaItems);
            }
            return episodes;
        }

        public async Task<Models.TVShows.TVShowEpisode> FindTVShowEpisodeByMediaItem(long originalMediaItemId)
        {
            var episode = await _DataStore.FindTVShowEpisodeByMediaItem(originalMediaItemId);
            if (episode is null)
                return null;
            episode = await CompleteEpisodeAsync(episode);
            var mediaItems = await _DataStore.GetTVShowEpisodeMediaItems(episode.Id);
            var modelEpisode = Models.TVShows.TVShowEpisode
                                             .FromDataModel(episode)
                                             .UpdatePicture(_Settings.CacheRootPath) as Models.TVShows.TVShowEpisode;
            var primMediaItem = await GetMediaItemAsync(episode.PrimaryMediaItemId);
            var downloadMediaItem = await GetMediaItemAsync(episode.DownloadMediaItemId);
            await SetEpisodeMediaItems(modelEpisode, primMediaItem, downloadMediaItem, mediaItems);
            return modelEpisode;
        }

        public async Task<IEnumerable<TVShowName>> GetTVShowNames()
        {
            var shows = await _DataStore.GetTVShows();
            return shows
                .Select(show =>
                {
                    var model = TVShowName.FromDataModel(show);
                    return model;
                })
                .Cast<TVShowName>();
        }

        public async Task<IEnumerable<Models.TVShows.TVShow>> GetTVShows()
        {
            var shows = await _DataStore.GetTVShows();
            return shows
                .Select(show =>
                {
                    var model = Models.TVShows.TVShow.FromDataModel(show).UpdatePicture(_Settings.CacheRootPath) as Models.TVShows.TVShow;
                    return model;
                })
                .Cast<Models.TVShows.TVShow>();
        }

        public async Task<IEnumerable<Models.TVShows.TVShow>> GetTVShows(long collectionId)
        {
            var shows = await _DataStore.GetTVShows();
            return shows
                .Where(show => show.CollectionId == collectionId)
                .Select(show =>
                {
                    var model = Models.TVShows.TVShow.FromDataModel(show).UpdatePicture(_Settings.CacheRootPath) as Models.TVShows.TVShow;
                    return model;
                })
                .Cast<Models.TVShows.TVShow>();
        }

        public async Task<IEnumerable<Models.TVShows.TVShow>> GetTVShows(int offset, int value)
        {
            var shows = await _DataStore.GetTVShows();
            return shows
                .OrderBy(show => show.Name)
                .Skip(offset)
                .Take(value)
                .Select(show =>
                {
                    var model = Models.TVShows.TVShow.FromDataModel(show).UpdatePicture(_Settings.CacheRootPath) as Models.TVShows.TVShow;
                    return model;
                })
                .Cast<Models.TVShows.TVShow>();
        }

        public async Task<Models.TVShows.TVShowSeason> GetTVShowSeason(long id)
        {
            var season = await _DataStore.GetTVShowSeason(id);
            return Models.TVShows.TVShowSeason.FromDataModel(season).UpdatePicture(_Settings.CacheRootPath) as Models.TVShows.TVShowSeason;
        }

        public async Task<IEnumerable<Models.TVShows.TVShowSeason>> GetTVShowSeasons(long showId)
        {
            var seasons = await _DataStore.GetTVShowSeasons(showId);
            return seasons
                .Select(season =>
                {
                    var model = Models.TVShows.TVShowSeason.FromDataModel(season).UpdatePicture(_Settings.CacheRootPath) as Models.TVShows.TVShowSeason;
                    return model;
                })
                .Cast<Models.TVShows.TVShowSeason>();
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

        public async Task RemoveMediaItemAsync(Models.MediaItems.MediaItem mediaItem)
        {
            var mediaStore = await _DataStore.GetMediaItemAsync(mediaItem.Id);
            await ClearMediaItem(mediaStore);
            await _DataStore.RemoveMediaItem(mediaStore);
            OnElementChanged(null, null, new BaseModelEventArgs(mediaItem));
        }

        public async Task RemoveMovieAsync(Models.Movies.Movie movie)
        {
            var dbItem = await _DataStore.GetMovie(movie.Id);
            await ClearMovie(dbItem);
            await _DataStore.RemoveMovie(dbItem.Id);
        }

        public async Task RemoveMovieCollectionAsync(Models.Movies.MovieCollection collection)
        {
            var dbItem = await _DataStore.GetMovieCollection(collection.Id);
            await ClearMovieCollection(dbItem);
            await _DataStore.RemoveMovieCollection(dbItem.Id);
        }

        private async Task ClearMovieCollection(Services.Database.Models.MovieCollection dbItem)
        {
            await Task.CompletedTask;
        }

        public async Task RemoveTVShowAsync(Models.TVShows.TVShow show)
        {
            var dbItem = await _DataStore.GetTVShow(show.Id);
            await ClearTVShow(dbItem);
            await _DataStore.RemoveTVShow(dbItem.Id);
            OnElementChanged(null, null, new BaseModelEventArgs(show));
        }

        public async Task RemoveTVShowCollectionAsync(Models.TVShows.TVShowCollection collection)
        {
            var dbItem = await _DataStore.GetTVShowCollection(collection.Id);
            await ClearTVShowCollection(dbItem);
            await _DataStore.RemoveTVShowCollection(dbItem.Id);
            OnElementChanged(null, null, new BaseModelEventArgs(collection));
        }

        private async Task ClearTVShowCollection(Database.Models.TVShowCollection showCollection)
        {
            var shows = await GetTVShows(showCollection.Id);
            foreach (var show in shows)
            {
                show.CollectionId = 0;
                await AddTVShowAsync(show);
            }
        }

        public async Task RemoveTVShowSeasonAsync(Models.TVShows.TVShowSeason season)
        {
            var dbItem = await _DataStore.GetTVShowSeason(season.Id);
            await ClearTVShowSeason(dbItem);
            await _DataStore.RemoveTVShowSeason(dbItem.Id);
        }

        public async Task RemoveTVShowEpisodeAsync(Models.TVShows.TVShowEpisode episode)
        {
            var dbItem = await _DataStore.GetTVShowEpisode(episode.Id);
            await ClearTVShowEpisode(dbItem);
            await _DataStore.RemoveTVShowEpisode(dbItem.Id);
        }

        #region Sources
        public async Task<IEnumerable<MediaElementSource>> GetSourcesAsync()
        {
            return (await (await _DataStore.GetSourcesAsync())
                .OrderBy(s => s.Name)
                .ToArrayAsync())
                .Select(source => BaseModel.FromDataModel(source) as MediaElementSource);
        }

        public async Task<MediaElementSource> GetSourceAsync(long id)
        {
            return BaseModel.FromDataModel(await _DataStore.GetSourceAsync(id)) as MediaElementSource;
        }

        public async Task AddSourceAsync(MediaElementSource source)
        {
            var isNew = source.Id == 0;
            var dataModel = source.ToDataModelAsync();
            await _DataStore.AddOrUpdateSourceAsync(dataModel as MediaSource);
            source.UpdateAutoincrements(dataModel);
            OnElementChanged(isNew ? (new BaseModelEventArgs(source)) : null,
                             (!isNew) ? (new BaseModelEventArgs(source)) : null,
                             null);
        }

        public async Task RemoveMediaSourceAsync(MediaElementSource mediaItem)
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
            var collection = await _DataStore.GetMediaCollectionsAsync();
            var collectionArr = await collection.Where(s => s.MediaSourceId == SourceId)
                                                .OrderBy(s => s.Name)
                                                .ToArrayAsync();
            var result = collectionArr.Select(source => MediaItemCollection.FromDataModel(source) as MediaItemCollection);
            return result ?? new MediaItemCollection[0];
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
            if (dbItem == null)
                return;
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
            await _DataStore.AddOrUpdateMediaCollectionAsync(dataModel as MediaCollection);
            collection.UpdateAutoincrements(dataModel);
            OnElementChanged(isNew ? (new BaseModelEventArgs(collection)) : null,
                             (!isNew) ? (new BaseModelEventArgs(collection)) : null,
                             null);
        }
        #endregion

        #region Media Items
        public async Task<Models.MediaItems.MediaItem> GetMediaItemAsync(long id)
        {
            var item = await _DataStore.GetMediaItemAsync(id);
            return (Models.MediaItems.MediaItem.FromDataModel(item)?
                .UpdatePicture(_Settings.CacheRootPath) as Models.MediaItems.MediaItem)?
                .UpdatePath(_Settings.GetPath((MediaItemCopyType)item.CopyType));
        }

        public async Task<IEnumerable<Models.MediaItems.MediaItem>> GetAllMediaItems()
        {
            return (await (await _DataStore.GetMediaItemsAsync())
                .OrderBy(s => s.Name)
                .ToArrayAsync())
                .Select(item =>
                        (Models.MediaItems.MediaItem.FromDataModel(item).UpdatePicture(_Settings.CacheRootPath) as Models.MediaItems.MediaItem)
                                           .UpdatePath(_Settings.GetPath((MediaItemCopyType)item.CopyType)));
        }

        public async Task<IEnumerable<Models.MediaItems.MediaItem>> GetMediaItemsAsync(long CollectionId)
        {
            return (await (await _DataStore.GetMediaItemsAsync())
                .Where(s => (s.ParentCollectionId == CollectionId) && (s.OriginalMediaItemId == 0))
                .OrderBy(s => s.Name)
                .ToArrayAsync())
                .Select(item =>
                        (Models.MediaItems.MediaItem.FromDataModel(item).UpdatePicture(_Settings.CacheRootPath) as Models.MediaItems.MediaItem)
                            .UpdatePath(_Settings.GetPath((MediaItemCopyType)item.CopyType)));
        }

        public async Task<Models.MediaItems.MediaItem> FindMediaItemAsync(long SourceId, string path)
        {
            var items = await (await _DataStore.GetMediaItemsAsync())
                .Where(item => item.Path == path)
                .ToArrayAsync();
            return items
                .Where(item =>
                {
                    var collection = _DataStore
                        .GetMediaCollectionAsync(item.ParentCollectionId)
                        .Wait<MediaCollection>();
                    return collection.MediaSourceId == SourceId;
                })
                .Select(item =>
                        (Models.MediaItems.MediaItem.FromDataModel(item).UpdatePicture(_Settings.CacheRootPath) as Models.MediaItems.MediaItem)
                    .UpdatePath(_Settings.GetPath((MediaItemCopyType)item.CopyType)))
                .FirstOrDefault();
        }

        public async Task AddMediaItemAsync(Models.MediaItems.MediaItem mediaItem)
        {
            var isNew = mediaItem.Id == 0;
            var dataModel = mediaItem.ToDataModelAsync() as Services.Database.Models.MediaItem;
            var rootPath = _Settings.GetPath((MediaItemCopyType)dataModel.CopyType);
            if (dataModel.Path.StartsWith(rootPath))
                dataModel.Path = dataModel.Path.Remove(0, rootPath.Length);
            await _DataStore.AddOrUpdateMediaItemAsync(dataModel);
            mediaItem.UpdateAutoincrements(dataModel);
            OnElementChanged(isNew ? (new BaseModelEventArgs(mediaItem)) : null,
                             (!isNew) ? (new BaseModelEventArgs(mediaItem)) : null,
                             null);
        }

        public async Task UpdateMediaItemAsync(Models.MediaItems.MediaItem mediaItem, bool notify)
        {
            var isNew = mediaItem.Id == 0;
            var dataModel = mediaItem.ToDataModelAsync() as Services.Database.Models.MediaItem;
            var rootPath = _Settings.GetPath((MediaItemCopyType)dataModel.CopyType);
            if (dataModel.Path.StartsWith(rootPath))
                dataModel.Path = dataModel.Path.Remove(0, rootPath.Length);
            await _DataStore.AddOrUpdateMediaItemAsync(dataModel);
            mediaItem.UpdateAutoincrements(dataModel);
            OnElementChanged(isNew ? (new BaseModelEventArgs(mediaItem)) : null,
                             (!isNew && notify) ? (new BaseModelEventArgs(mediaItem)) : null,
                             null);
        }

        public async Task<IEnumerable<Models.MediaItems.MediaItem>> GetAlternateMediaItemsAsync(long mediaItemId)
        {
            return (await (await _DataStore.GetMediaItemsAsync())
                .Where(s => s.OriginalMediaItemId == mediaItemId)
                .OrderBy(s => s.Name)
                .ToArrayAsync())
                .Select(item =>
                        (Models.MediaItems.MediaItem.FromDataModel(item).UpdatePicture(_Settings.CacheRootPath) as Models.MediaItems.MediaItem)
                                           .UpdatePath(_Settings.GetPath((MediaItemCopyType)item.CopyType)));
        }

        public async Task<Models.MediaItems.MediaItem> GetOriginalMediaItemsAsync(Models.MediaItems.MediaItem item)
        {
            return await GetMediaItemAsync(item.OriginalMediaItemId);
        }

        public async Task<IEnumerable<Models.MediaItems.MediaItem>> GetUncategorizedMediaItems(int offset, int count)
        {
            return (await (await _DataStore.GetMediaItemsAsync())
                .Where(mi =>
                       (mi.MetaInfoJson == null) || (mi.MetaInfoJson == string.Empty) || (mi.MetaInfoJson == "null"))
                .Skip(offset)
                .Take(count)
                .ToArrayAsync())
                .Select(mi =>
                        (Models.MediaItems.MediaItem.FromDataModel(mi).UpdatePicture(_Settings.CacheRootPath) as Models.MediaItems.MediaItem)
                                       .UpdatePath(_Settings.GetPath((MediaItemCopyType)mi.CopyType)));
        }

        public async Task<IEnumerable<Models.MediaItems.MediaItem>> GetDownloadedMediaItems(int offset, int count)
        {
            return (await(await _DataStore.GetMediaItemsAsync())
                   .Where(mi => mi.CopyType == 2)
                   .Skip(offset)
                   .Take(count)
                   .ToArrayAsync())
                   .Select(mi =>
                           (Models.MediaItems.MediaItem.FromDataModel(mi).UpdatePicture(_Settings.CacheRootPath) as Models.MediaItems.MediaItem)
                                          .UpdatePath(_Settings.GetPath((MediaItemCopyType)mi.CopyType)));
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

        private async Task ClearSourceMediaAsync(MediaSource source)
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

        private async Task ClearCollectionMediaAsync(MediaCollection coll)
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
            {
                var path = $"{_Settings.CacheFolderPath}{mediaItem.Path}";
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
        #endregion

        #region Playlist
        public async Task AddPlaylistAsync(Models.Playlists.Playlist playlist)
        {
            var isNew = playlist.Id == 0;
            var dataModel = playlist.ToDataModelAsync();
            await _DataStore.AddOrUpdatePlaylistAsync(dataModel as Services.Database.Models.Playlist);
            playlist.UpdateAutoincrements(dataModel);

            var existingMediaItems = (await _DataStore.GetPlaylistEntries(playlist.Id)).ToArray();
            var mediaItemsToAdd = playlist.Items.ToArray();
            var mediaItemsToDelete = existingMediaItems.Skip(mediaItemsToAdd.Length).ToArray();
            var existingCollectionOffset = 0;
            foreach (var mediaItemToAdd in mediaItemsToAdd)
            {
                Services.Database.Models.PlaylistEntry dataModel2 = mediaItemToAdd.ToDataModelAsync() as Services.Database.Models.PlaylistEntry;
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

        public async Task RemovePlaylistAsync(Models.Playlists.Playlist playlist)
        {
            var dbItem = await _DataStore.GetPlaylist(playlist.Id);
            if (dbItem == null)
                return;
            await ClearPlaylistEntriesAsync(dbItem);
            await _DataStore.RemovePlaylist(dbItem.Id);
            OnElementChanged(null, null, new BaseModelEventArgs(playlist));
        }

        private async Task ClearPlaylistEntriesAsync(Database.Models.Playlist dbItem)
        {
            var entries = await _DataStore.GetPlaylistEntries(dbItem.Id);
            foreach (var entry in entries)
                await _DataStore.RemovePlaylistEntryAsync(entry);
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
                PlaybackHistoryEntry existingEntry = null;
                if (idx < existingEntries.Count)
                    existingEntry = existingEntries[idx];
                else
                {
                    existingEntry = new PlaybackHistoryEntry();
                    existingEntries.Add(existingEntry);
                }
                existingEntry.Deleted = false;
                existingEntry.MediaItemId = (currentEntry.Item == null) ? 0 : currentEntry.Item.Id;
                existingEntry.TypedItemId = currentEntry.TypedItem.Id;
                existingEntry.PlaylistId = (currentEntry.Playlist is null) ? 0 : currentEntry.Playlist.Id;
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
                    case nameof(Models.Movies.Movie):
                        item.TypedItem = await GetMovie(item.TypedItemId);
                        break;
                    case nameof(Models.TVShows.TVShowEpisode):
                        item.TypedItem = await GetTVShowEpisode(item.TypedItemId);
                        break;
                }
                if (item.PlaylistId != 0)
                    item.Playlist = await GetPlaylist(item.PlaylistId);
            }
            return dbItems.Where(item => (item.Item is not null) || (item.TypedItem is not null));
        }

        public async Task<IEnumerable<Models.TVShows.TVShowCollection>> GetTVShowCollections()
        {
            var seasons = await _DataStore.GetTVShowCollections();
            return seasons
                .Select(season =>
                {
                    var model = Models.TVShows.TVShowCollection
                                              .FromDataModel(season)
                                              .UpdatePicture(_Settings.CacheRootPath) as Models.TVShows.TVShowCollection;
                    return model;
                })
                .Cast<Models.TVShows.TVShowCollection>();
        }

        public async Task<IEnumerable<Models.TVShows.TVShowCollection>> FindTVShowCollectionByNameAsync(string name)
        {
            var seasons = await _DataStore.GetTVShowCollectionsByName(name);
            return seasons
                .Select(season =>
                {
                    var model = Models.TVShows.TVShowCollection
                                              .FromDataModel(season)
                                              .UpdatePicture(_Settings.CacheRootPath) as Models.TVShows.TVShowCollection;
                    return model;
                })
                .Cast<Models.TVShows.TVShowCollection>();
        }

        public async Task<Models.TVShows.TVShowCollection> GetTVShowCollection(long collectionId)
        {
            return Models.TVShows.TVShowCollection
                                 .FromDataModel(await _DataStore.GetTVShowCollection(collectionId))
                                 .UpdatePicture(_Settings.CacheRootPath) as Models.TVShows.TVShowCollection;
        }

    }
}
