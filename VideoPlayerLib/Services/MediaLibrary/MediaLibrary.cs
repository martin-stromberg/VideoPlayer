using VideoPlayerLib.Services.Database;
using VideoPlayerLib.Services.MediaLibrary.Models;

namespace VideoPlayerLib.Services.MediaLibrary
{
    public class MediaLibrary: IMediaLibrary
    {

        private readonly IMediaLibraryDatabase dataStore;

        public event EventHandler<BaseModelEventArgs> ModelElementAdded;

        public event EventHandler<BaseModelEventArgs> ModelElementUpdated;

        public event EventHandler<BaseModelEventArgs> ModelElementRemoved;

        protected void OnElementChanged(
            BaseModelEventArgs modelElementAdded,
            BaseModelEventArgs modelElementUpdated,
            BaseModelEventArgs modelElementRemoved)
        {
            if ((modelElementAdded != null) && (ModelElementAdded != null))
                ModelElementAdded(this, modelElementAdded);
            if ((modelElementUpdated != null) && (ModelElementUpdated != null))
                ModelElementUpdated(this, modelElementUpdated);
            if ((modelElementRemoved != null) && (ModelElementRemoved != null))
                ModelElementRemoved(this, modelElementRemoved);
        }

        public MediaLibrary(IMediaLibraryDatabase dataStore)
        {
            this.dataStore = dataStore;
        }

        #region Sources
        public async Task<IEnumerable<MediaSource>> GetSourcesAsync()
        {
            return (await (await dataStore.GetSourcesAsync())
                .OrderBy(s => s.Name)
                .ToArrayAsync())
                .Select(source => MediaSource.FromDataModel(source) as MediaSource);
        }

        public async Task<MediaSource> GetSourceAsync(long id)
        {
            return MediaSource.FromDataModel(await dataStore.GetSourceAsync(id)) as MediaSource;
        }

        public async Task AddSourceAsync(MediaSource source)
        {
            var isNew = source.Id == 0;
            var dataModel = source.ToDataModelAsync() as Database.Models.MediaSource;
            await dataStore.AddOrUpdateSourceAsync(dataModel);
            source.UpdateAutoincrements(dataModel);
            OnElementChanged(isNew ? (new BaseModelEventArgs(source)) : null,
                             (!isNew) ? (new BaseModelEventArgs(source)) : null,
                             null);
        }

        public async Task RemoveMediaSourceAsync(MediaSource mediaItem)
        {
            var dbItem = await dataStore.GetSourceAsync(mediaItem.Id);
            await ClearSourceMediaAsync(dbItem);
            await dataStore.RemoveSource(dbItem);
            OnElementChanged(null, null, new BaseModelEventArgs(mediaItem));
        }
        #endregion

        #region Media Collections
        public async Task<IEnumerable<MediaItemCollection>> GetMediaItemCollectionsAsync(long SourceId)
        {
            return (await (await dataStore.GetMediaCollectionsAsync())
                .Where(s => s.MediaSourceId == SourceId)
                .OrderBy(s => s.Name)
                .ToArrayAsync())
                .Select(source => MediaItemCollection.FromDataModel(source) as MediaItemCollection);
        }

        public async Task<MediaItemCollection> GetMediaItemCollectionAsync(long Id)
        {
            return MediaItemCollection.FromDataModel(await dataStore.GetMediaCollectionAsync(Id)) as MediaItemCollection;
        }

        public async Task<IEnumerable<MediaItemCollection>> GetChildMediaItemCollectionsAsync(long collectionId)
        {
            return (await (await dataStore.GetMediaCollectionsAsync())
                .Where(s => s.ParentCollectionId == collectionId)
                .OrderBy(s => s.Name)
                .ToArrayAsync())
                .Select(source => MediaItemCollection.FromDataModel(source) as MediaItemCollection);
        }

        public async Task<MediaItemCollection> FindMediaItemCollectionAsync(long sourceId, string path)
        {
            return MediaItemCollection.FromDataModel(await (await dataStore.GetMediaCollectionsAsync())
                .FirstOrDefaultAsync(item => (item.MediaSourceId == sourceId)
                    && (item.Path == path))) as MediaItemCollection;
        }

        public async Task AddMediaItemCollectionAsync(MediaItemCollection collection)
        {
            var isNew = collection.Id == 0;
            var dataModel = collection.ToDataModelAsync() as Database.Models.MediaCollection;
            await dataStore.AddOrUpdateMediaCollectionAsync(dataModel);
            collection.UpdateAutoincrements(dataModel);
            OnElementChanged(isNew ? (new BaseModelEventArgs(collection)) : null,
                             (!isNew) ? (new BaseModelEventArgs(collection)) : null,
                             null);
        }
        #endregion

        #region Media Items
        public async Task<MediaItem> GetMediaItemAsync(long id)
        {
            return MediaItem.FromDataModel(await dataStore.GetMediaItemAsync(id)) as MediaItem;
        }

        public async Task<IEnumerable<MediaItem>> GetMediaItemsAsync(long CollectionId)
        {
            return (await (await dataStore.GetMediaItemsAsync())
                .Where(s => (s.ParentCollectionId == CollectionId) && (s.OriginalMediaItemId == 0))
                .OrderBy(s => s.Name)
                .ToArrayAsync())
                .Select(source => MediaItem.FromDataModel(source) as MediaItem);
        }

        public async Task<MediaItem> FindMediaItemAsync(long SourceId, string path)
        {
            var items = await (await dataStore.GetMediaItemsAsync())
                .Where(item => item.Path == path)
                .ToArrayAsync();
            return items
                .Where(item =>
                {
                    var collection = dataStore
                        .GetMediaCollectionAsync(item.ParentCollectionId)
                        .Wait<Database.Models.MediaCollection>();
                    return collection.MediaSourceId == SourceId;
                })
                .Select(item => MediaItem.FromDataModel(item) as MediaItem)
                .FirstOrDefault();
        }

        public async Task AddMediaItemAsync(MediaItem mediaItem)
        {
            var isNew = mediaItem.Id == 0;
            var dataModel = mediaItem.ToDataModelAsync() as Database.Models.MediaItem;
            await dataStore.AddOrUpdateMediaItemAsync(dataModel);
            mediaItem.UpdateAutoincrements(dataModel);
            OnElementChanged(isNew ? (new BaseModelEventArgs(mediaItem)) : null,
                             (!isNew) ? (new BaseModelEventArgs(mediaItem)) : null,
                             null);
        }

        public async Task<IEnumerable<MediaItem>> GetAlternateMediaItemsAsync(long mediaItemId)
        {
            return (await (await dataStore.GetMediaItemsAsync())
                .Where(s => s.OriginalMediaItemId == mediaItemId)
                .OrderBy(s => s.Name)
                .ToArrayAsync())
                .Select(source => MediaItem.FromDataModel(source) as MediaItem);
        }
        #endregion 

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
            return (await (await dataStore.GetSourcesAsync()).FirstOrDefaultAsync()) == null;
        }

        #region Clear
        public async Task ClearMedia()
        {
            foreach (var movie in await dataStore.GetMovies())
                await dataStore.RemoveMovie(movie.Id);
            foreach (var show in await dataStore.GetTVShows())
                await dataStore.RemoveTVShow(show.Id);

            foreach (var source in await (await dataStore.GetSourcesAsync()).ToArrayAsync())
            {
                await ClearSourceMediaAsync(source);
                source.LastScan = DateTime.MinValue;
                await dataStore.AddOrUpdateSourceAsync(source);
            }
        }

        private async Task ClearSourceMediaAsync(Database.Models.MediaSource source)
        {
            var collStore = await dataStore.GetMediaCollectionsAsync();
            var collections = await collStore.Where(c => c.MediaSourceId == source.Id).ToArrayAsync();
            foreach (var coll in collections)
            {
                ClearCaches(coll);
                await ClearCollectionMediaAsync(coll);
                await dataStore.RemoveMediaCollection(coll);
            }
        }

        private async Task ClearCollectionMediaAsync(Database.Models.MediaCollection coll)
        {
            var mediaStore = await dataStore.GetMediaItemsAsync();
            var mediaItems = await mediaStore.Where(mi => mi.ParentCollectionId == coll.Id).ToArrayAsync();
            foreach (var mediaItem in mediaItems)
            {
                await ClearMediaItem(mediaItem);
            }
        }

        private async Task ClearMediaItem(Database.Models.MediaItem mediaItem)
        {
            ClearCaches(mediaItem);
            foreach (var assignment in await dataStore.GetMovieMediaItemsForMediaItem(mediaItem.Id))
                await dataStore.RemoveMovieMediaItemAsync(assignment);
            foreach (var assignment in await dataStore.GetTVShowMediaItemsForMediaItem(mediaItem.Id))
                await dataStore.RemoveMovieMediaItemAsync(assignment);
            await dataStore.RemoveMediaItem(mediaItem);
        }

        private void ClearCaches(Database.Models.MediaItem mediaItem)
        {
            if (File.Exists(mediaItem.PicturePath))
                File.Delete(mediaItem.PicturePath);
            if (((MediaItemCopyType)mediaItem.CopyType) == MediaItemCopyType.Cache)
                File.Delete(mediaItem.Path);
        }
        #endregion

        public async Task<Movie> GetMovie(long id)
        {
            var movie = await dataStore.GetMovie(id);
            var mediaItems = await dataStore.GetMovieMediaItems(movie.Id);
            return (Movie.FromDataModel(movie) as Movie)
                .SetMediaItems(mediaItems);
        }

        public async Task<IEnumerable<Movie>> GetMovies()
        {
            return (await dataStore.GetMovies())
                .Select(movie => Movie.FromDataModel(movie) as Movie);
        }

        public async Task<Movie> FindMovieAsync(long mediaItemId)
        {
            var movie = await dataStore.GetMovieByMediaItem(mediaItemId);
            if (movie == null)
                return null;
            var mediaItems = await dataStore.GetMovieMediaItems(movie.Id);
            return (Movie
                .FromDataModel(movie) as Movie)
                .SetMediaItems(mediaItems);
        }

        public async Task AddMovieAsync(Movie movie)
        {
            var isNew = movie.Id == 0;
            var dataModel = movie.ToDataModelAsync() as Database.Models.Movie;
            var dataModelMediaItems = movie.MediaItems
                                           .Select(id =>
                                                   new Database.Models.MovieMediaItem()
                                           {
                                               MovieId = movie.Id,
                                               MediaItemId = id
                                           });
            if (!isNew)
                await dataStore.RemoveMovieMediaItemsAsync(movie.Id);
            await dataStore.AddOrUpdateMovie(dataModel);
            movie.UpdateAutoincrements(dataModel);
            foreach (var mediaItem in dataModelMediaItems)
            {
                mediaItem.MovieId = movie.Id;
                await dataStore.AddMovieMediaItem(mediaItem);
            }
            OnElementChanged(isNew ? (new BaseModelEventArgs(movie)) : null,
                             (!isNew) ? (new BaseModelEventArgs(movie)) : null,
                             null);
        }

        public async Task<IEnumerable<TVShow>> GetTVShows()
        {
            var shows = await dataStore.GetTVShows();
            return shows
                .Select(show =>
                {
                    var model = TVShow.FromDataModel(show) as TVShow;

                    // var seasons = FindTVShowSeasons(show.Id);
                    // model.Seasons = seasons.ToArray();
                    return model;
                })
                .Cast<TVShow>();
        }

        public async Task<TVShow> GetTVShow(long id)
        {
            var show = await dataStore.GetTVShow(id);
            return TVShow.FromDataModel(show) as TVShow;
        }

        public async Task<IEnumerable<TVShowSeason>> GetTVShowSeasons(long showId)
        {
            var seasons = await dataStore.GetTVShowSeasons(showId);
            return seasons
                .Select(season =>
                {
                    var model = TVShowSeason.FromDataModel(season) as TVShowSeason;
                    return model;
                })
                .Cast<TVShowSeason>();
        }

        public async Task<TVShowSeason> GetTVShowSeason(long id)
        {
            var season = await dataStore.GetTVShowSeason(id);
            return TVShowSeason.FromDataModel(season) as TVShowSeason;
        }

        public async Task<TVShowEpisode> GetTVShowEpisode(long id)
        {
            var episode = await dataStore.GetTVShowEpisode(id);
            var mediaItems = await dataStore.GetTVShowEpisodeMediaItems(episode.Id);
            return (TVShowEpisode.FromDataModel(episode) as TVShowEpisode)
                .SetMediaItems(mediaItems);
        }

        public async Task<IEnumerable<TVShowEpisode>> GetTVShowEpisodes(long seasonId)
        {
            var episodes = await dataStore.GetTVShowEpisodes(seasonId);
            return episodes
                .Select(episode =>
                {
                    var model = TVShowEpisode.FromDataModel(episode) as TVShowEpisode;
                    return model;
                })
                .Cast<TVShowEpisode>();
        }

        public async Task<IEnumerable<TVShow>> FindTVShowByNameAsync(string name)
        {
            var show = await dataStore.GetTVShowsByName(name);
            return show
                .Select(show =>
                {
                    var model = TVShow.FromDataModel(show) as TVShow;

                    // var seasons = FindTVShowSeasons(show.Id);
                    // model.Seasons = seasons.ToArray();
                    return model;
                })
                .Cast<TVShow>();
        }

        private IEnumerable<TVShowSeason> FindTVShowSeasons(long showId)
        {
            var seasons = dataStore
                .GetTVShowSeasons(showId)
                .Wait<IEnumerable<Database.Models.TVShowSeason>>();
            return seasons
                .Select(s =>
                {
                    var season = TVShowSeason.FromDataModel(s) as TVShowSeason;

                    // var episodes = FindTVShowEpisodes(season.Id);
                    // season.Episodes = episodes.ToArray();
                    return season;
                })
                .Cast<TVShowSeason>();
        }

        private IEnumerable<TVShowEpisode> FindTVShowEpisodes(long id)
        {
            var episodes = dataStore
                .GetTVShowEpisodes(id)
                .Wait<IEnumerable<Database.Models.TVShowEpisode>>();
            return episodes
                .Select(e =>
                {
                    var episode = TVShowEpisode.FromDataModel(e) as TVShowEpisode;
                    var mediaItems = dataStore
                        .GetTVShowEpisodeMediaItems(episode.Id)
                        .Wait<IEnumerable<Database.Models.TVShowEpisodeMediaItem>>()
                        .Select(mi => mi.Id);
                    episode.MediaItems = mediaItems.ToArray();
                    return episode;
                })
                .Cast<TVShowEpisode>();
        }

        public async Task<TVShow> FindTVShowAsync(long id)
        {
            var show = await dataStore
                .GetTVShow(id);
            if (show == null)
                return null;
            return TVShow.FromDataModel(show) as TVShow;
        }

        public async Task AddTVShowAsync(TVShow show)
        {
            var isNew = show.Id == 0;
            var dataModelShow = show.ToDataModelAsync() as Database.Models.TVShow;
            await dataStore.AddOrUpdateTVShow(dataModelShow);
            show.UpdateAutoincrements(dataModelShow);
            OnElementChanged(isNew ? (new BaseModelEventArgs(show)) : null,
                             (!isNew) ? (new BaseModelEventArgs(show)) : null,
                             null);

            // foreach (var season in show.Seasons)
            // await AddTVShowSeasonAsync(show, season);
        }

        public async Task AddTVShowSeasonAsync(TVShow show, TVShowSeason season)
        {
            var isNew = season.Id == 0;
            season.ShowId = show.Id;
            var dataModelSeason = season.ToDataModelAsync() as Database.Models.TVShowSeason;
            dataModelSeason.ShowId = show.Id;
            await dataStore.AddOrUpdateTVShowSeason(dataModelSeason);
            season.UpdateAutoincrements(dataModelSeason);
            OnElementChanged(isNew ? (new BaseModelEventArgs(season)) : null,
                             (!isNew) ? (new BaseModelEventArgs(season)) : null,
                             null);

            // foreach (var episode in season.Episodes)
            // await AddTVShowEpisodeAsync(show, season, episode);
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
                await dataStore.RemoveTVShowEpisodeMediaItemsAsync(episode.Id);
            await dataStore.AddOrUpdateTVShowEpisode(dataModelEpisode);
            episode.UpdateAutoincrements(dataModelEpisode);
            foreach (var mediaItem in dataModelMediaItems)
            {
                mediaItem.EpisodeId = episode.Id;
                await dataStore.AddTVShowEpisodeMediaItem(mediaItem);
            }
            OnElementChanged(isNew ? (new BaseModelEventArgs(episode)) : null,
                             (!isNew) ? (new BaseModelEventArgs(episode)) : null,
                             null);
        }

        public async Task RemoveMediaItemAsync(MediaItem mediaItem)
        {
            if (mediaItem.CopyType == MediaItemCopyType.Cache)
                File.Delete(mediaItem.Path);
            var mediaStore = await dataStore.GetMediaItemAsync(mediaItem.Id);
            await dataStore.RemoveMediaItem(mediaStore);
        }

        public async Task<IEnumerable<MovieCollection>> FindMovieCollectionByNameAsync(string name)
        {
            return (await dataStore.GetMovieCollectionsByName(name))
                .Select(coll => MovieCollection.FromDataModel(coll) as MovieCollection);
        }

        public async Task AddMovieCollectionAsync(MovieCollection collection)
        {
            var isNew = collection.Id == 0;
            var dataModelShow = collection.ToDataModelAsync() as Database.Models.MovieCollection;
            await dataStore.AddOrUpdateMovieCollection(dataModelShow);
            collection.UpdateAutoincrements(dataModelShow);
            OnElementChanged(isNew ? (new BaseModelEventArgs(collection)) : null,
                             (!isNew) ? (new BaseModelEventArgs(collection)) : null,
                             null);
        }

        public async Task<IEnumerable<MovieCollection>> GetMovieCollections()
        {
            var collections = await dataStore.GetMovieCollections();
            return collections
                .Select(collection =>
                {
                    var model = MovieCollection.FromDataModel(collection) as MovieCollection;
                    return model;
                })
                .Cast<MovieCollection>();
        }

        public async Task<MovieCollection> GetMovieCollection(long id)
        {
            return MovieCollection.FromDataModel(await dataStore.GetMovieCollection(id)) as MovieCollection;
        }

    }
}
