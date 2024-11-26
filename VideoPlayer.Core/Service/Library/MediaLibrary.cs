using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using VideoPlayer.Service.BaseServices;
using VideoPlayer.Service.Database;
using VideoPlayer.Service.Database.Models;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Library.Models.Playlists;
using VideoPlayer.Service.Log;

namespace VideoPlayer.Service.Library
{

    public class MediaLibrary : TimerService, IMediaLibrary
    {

        private readonly IMediaLibraryDatabase _Database;
        private ConcurrentDictionary<Type, ModelCache<BaseDataModel>> _ModelCaches = new ConcurrentDictionary<Type, ModelCache<BaseDataModel>>();

        public MediaLibrary(
            IMediaLibraryDatabase database,
            ILogger<MediaLibrary> logger)
            : base(logger)
        {
            _Database = database; 
            Start();
        }
        private Setup _Setup = null;
        public Setup Setup
        {
            get
            {
                if (_Setup is null)
                {
                    var setupId = _Database
                        .GetAll<DataSetup>()
                        .Select(c => c.Id)
                        .FirstOrDefault();

                    var cache = GetModelCache(typeof(DataSetup));
                    _Setup = cache.GetServiceModel<Setup>(setupId);
                    if (_Setup is null)
                    {
                        _Setup = Setup.Default();
                        AddOrUpdate(_Setup);
                    }
                }
                return _Setup;
            }
        }

        private ModelCache<BaseDataModel> GetModelCache(Type type)
        {
            lock (_Database)
            {
                if (!_ModelCaches.ContainsKey(type))
                {
                    _ModelCaches[type] = new ModelCache<BaseDataModel>(_Database, this, type, Logger);
                    _ModelCaches[type].ElementUpdated += MediaLibrary_ElementUpdated;
                }
            }
            return _ModelCaches[type];
        }

        private void MediaLibrary_ElementUpdated(object sender, EventArgs e)
        {
            var args = e as CacheElementEventArgs;
            UpdateMediaItems(args.Element);
        }

        private void UpdateMediaItems(CacheElement element)
        {
            var classifiedEntry = element.Item as DataClassifiedEntry;
            var mediaItemIds = new long[0];
            if (classifiedEntry is not null)
                switch (classifiedEntry.Type)
                {
                    case DataEntryType.Movie:
                        mediaItemIds = GetMovieMediaItemIds(classifiedEntry.Id);
                        break;
                    case DataEntryType.TVShowEpisode:
                        mediaItemIds = GetTVShowEpisodeMediaItemIds(classifiedEntry.Id);
                        break;
                }
            if (mediaItemIds.Any() || element.Data.ContainsKey(nameof(Movie.MediaItemIds)))
                element.Data[nameof(Movie.MediaItemIds)] = mediaItemIds;
        }
        public void ClearCaches()
        {
            foreach (var cache in _ModelCaches.Values)
                cache.Clear();
        }
        private void Clear()
        {
            ClearCaches();
            _Database.Clear();
        }
        private long[] GetMovieMediaItemIds(long movieId)
        {
            return _Database.GetAll<DataClassifiedEntryMediaItem>(new KeyValuePair<string, object>(nameof(DataClassifiedEntryMediaItem.EntryId), movieId))
                            .Select(e => e.MediaItemId)
                            .ToArray();
        }
        private long[] GetTVShowEpisodeMediaItemIds(long episodeId)
        {
            return _Database.GetAll<DataClassifiedEntryMediaItem>(new KeyValuePair<string, object>(nameof(DataClassifiedEntryMediaItem.EntryId), episodeId))
                            .Select(e => e.MediaItemId)
                            .ToArray();
        }
        public void CreateDemoData()
        {
            Clear();
            Setup setup = new Setup() { Name = nameof(Setup) };
            MediaSource[] mediaSources = new MediaSource[]
            {
                //new HttpMediaSource()
                //{
                //    Name = "Test",
                //    Uri = $"http://mstromberg.ddns.net:50010/Folder?path=/MediaServer/Disk3/Test"
                //},
                new HttpMediaSource()
                {
                    Name = "Filme",
                    Uri = $"http://mstromberg.ddns.net:50010/Folder?path=/MediaServer/Disk3/Filme"
                },
                 new HttpMediaSource()
                 {
                 Name = "Serien",
                 Uri = $"http://mstromberg.ddns.net:50010/Folder?path=/MediaServer/Crucial X62/Serien"
                 },
                 new HttpMediaSource()
                 {
                 Name = "Serien (2)",
                 Uri = $"http://mstromberg.ddns.net:50010/Folder?path=/MediaServer/Disk2/Serien"
                 }
            };
            Genre[] genres = new Genre[] {
                new Genre(null){ Name = "Action" },
                new Genre(null){ Name = "Sport" },
                new Genre(null){ Name = "Animation" },
                new Genre(null){ Name = "Abenteuer",
                                AlternateNames = new GenreName[]{
                                    new GenreName(null){ Name = "Adventure"}
                                } },
                new Genre(null){ Name = "Science Fiction" ,
                                AlternateNames = new GenreName[]{
                                    new GenreName(null){ Name = "Science-Fiction"},
                                    new GenreName(null){ Name = "Sci-Fi"}
                                } },
                new Genre(null){ Name = "Komödie",
                                AlternateNames = new GenreName[]
                                {
                                    new GenreName(null){ Name = "Comedy" }
                                }
                },
                new Genre(null){ Name = "Seifenopern",
                                AlternateNames = new GenreName[]
                                {
                                    new GenreName(null){ Name = "Soap" }
                                }
                },
                new Genre(null){ Name = "Kinder",
                                AlternateNames = new GenreName[]
                                {
                                    new GenreName(null){ Name = "Children" },
                                    new GenreName(null){ Name = "Child" },
                                    new GenreName(null){ Name = "Cartoon" }
                                }
                },
                new Genre(null){ Name = "Romanzen",
                                AlternateNames = new GenreName[]
                                {
                                    new GenreName(null){ Name = "Romance" },
                                    new GenreName(null){ Name = "Liebesfilm" }
                                }
                },
                new Genre(null){ Name = "Dramen",
                AlternateNames = new GenreName[]{
                                    new GenreName(null){ Name = "Drama"}
                                }
                },
                new Genre(null){ Name = "Kurzfilme",
                AlternateNames = new GenreName[]{
                                    new GenreName(null){ Name = "Short"},
                                    new GenreName(null){ Name = "Kurzfilm"}
                                }
                },
                new Genre(null){ Name = "Musik" ,
                                AlternateNames = new GenreName[]{
                                    new GenreName(null){ Name = "Musical"},
                                    new GenreName(null){ Name = "Music"}
                                } },
                new Genre(null){ Name = "Historie" ,
                                AlternateNames = new GenreName[]{
                                    new GenreName(null){ Name = "History"},
                                    new GenreName(null){ Name = "Geschichte"}
                                } },
                new Genre(null){ Name = "Krimi" ,
                                AlternateNames = new GenreName[]{
                                    new GenreName(null){ Name = "Crime"}
                                } },
                new Genre(null){ Name = "Thriller" },
                new Genre(null){ Name = "Mystery",
                                 AlternateNames = new GenreName[]{
                                    new GenreName(null){ Name = "Suspense"}
                                }  },
                new Genre(null){ Name = "Fantasy" },
                new Genre(null){ Name = "Kriegsfilm" ,
                                AlternateNames = new GenreName[]{
                                    new GenreName(null){ Name = "War"},
                                    new GenreName(null){ Name = "Krieg"}
                                } },
                new Genre(null){ Name = "Biografie" ,
                                AlternateNames = new GenreName[]{
                                    new GenreName(null){ Name = "Biography"}
                                } },
                new Genre(null){ Name = "Dokumentation" ,
                                AlternateNames = new GenreName[]{
                                    new GenreName(null){ Name = "Documentation"},
                                    new GenreName(null){ Name = "Documentary"}
                                } },
                new Genre(null){ Name = "Horror" ,
                                AlternateNames = new GenreName[]{
                                    new GenreName(null){ Name = "Grusel"}
                                } },
                new Genre(null){ Name = "Miniserien" ,
                                AlternateNames = new GenreName[]{
                                    new GenreName(null){ Name = "Mini-Series"},
                                    new GenreName(null){ Name = "Mehrteiler"},
                                    new GenreName(null){ Name = "Miniserie"}
                                } },

                new Genre(null){ Name = "Familie" ,
                                AlternateNames = new GenreName[]{
                                    new GenreName(null){ Name = "Family"}
                                } },
                new Genre(null){ Name = "Western" },
            };
            AddOrUpdate(setup);
            AddOrUpdateRange(mediaSources);
            foreach (var genre in genres)
                AddOrUpdateGenre(genre);
        }

        public void AddOrUpdate<T>(T model) where T : BaseServiceModel
        {
            var dbModel = ((BaseServiceModel)model).GetDatabaseModel();
            var cache = GetModelCache(dbModel.GetType());
            var isNew = dbModel.Id == 0;
            dbModel = _Database.AddOrUpdate(dbModel);            
            cache.Update(model, dbModel);
            model.Id = dbModel.Id;
            model.CreatedAt = dbModel.CreatedAt;
            dbModel.SetRestorePoint();
            if (isNew)
                Hold(model);
            ItemUpdated?.Invoke(this, new BaseServiceModelEventArgs(model));
        }

        public event EventHandler<BaseServiceModelEventArgs> ItemUpdated;

        public void AddOrUpdateRange<T>(params T[] models) where T : BaseServiceModel
        {
            foreach (var model in models)
                AddOrUpdate(model);
        }

        public IEnumerable<CacheElement> GetAllCachedObjects()
        {
            foreach (var cache in _ModelCaches.Values.ToList())
                foreach (var element in cache.GetAllElements().ToList())
                    yield return element;
        }

        #region MediaSource
        public IEnumerable<MediaSource> GetSources()
        {
            var sourceIds = _Database
                .GetAll<MediaDataSource>()
                .Select(source => source.Id);
            foreach (var id in sourceIds)
                yield return GetSource(id);
        }
        public MediaSource AddOrUpdateSource(MediaSource source)
        {
            AddOrUpdate<MediaSource>(source);
            return source;
        }

        public MediaSource GetNextScanSource()
        {
            var source = _Database
                .GetAll<MediaDataSource>()
                .OrderBy(s => s.LastScan)
                .FirstOrDefault();
            if (source is null)
                return null;
            return GetSource(source.Id);
        }

        public MediaSource GetSource(long id)
        {
            var cache = GetModelCache(typeof(MediaDataSource));
            var source = cache.GetServiceModel<MediaSource>(id);
            return source;
        }
        #endregion
        #region MediaCollection
        public MediaCollection GetMediaCollectionByPath(long sourceId, string fullPath)
        {
            var collectionId = _Database
                .GetAll<MediaDataItemCollection>(
                    new KeyValuePair<string, object>(nameof(MediaDataItemCollection.SourceId), sourceId),
                    new KeyValuePair<string, object>(nameof(MediaDataItemCollection.Path), fullPath))
                .Select(c => c.Id)
                .FirstOrDefault();
            if (collectionId == 0)
                return null;
            return GetMediaCollection(collectionId);
        }

        public MediaCollection GetMediaCollection(long id)
        {
            var cache = GetModelCache(typeof(MediaDataItemCollection));
            var collection = cache.GetServiceModel<MediaCollection>(id);
            return collection;
        }

        public MediaCollection AddOrUpdateMediaCollection(MediaCollection collection)
        {
            AddOrUpdate(collection);
            return collection;
        }
        public void Delete(MediaCollection collection)
        {
            var cache = GetModelCache(typeof(MediaDataItemCollection));
            cache.Remove(collection);

            var mediaItems = GetMediaCollectionItems(collection.Id)
                .Select(mi =>
                {
                    Release(mi);
                    return mi;
                });
            foreach (var mediaItem in mediaItems)
                Delete(mediaItem);

            var parentCollection = GetMediaCollection(collection.ParentId);

            var dbModel = collection.GetDatabaseModel() as MediaDataItem;
            if (!_Database.Delete<MediaDataItem>(dbModel))
                throw new ApplicationException($"Media item collection could not be deleted.");

            if (parentCollection is null)
                return;
            try
            {
                var hasChildCollections = GetChildMediaCollections(parentCollection.Id)
                    .Select(col => { Release(col); return col; })
                    .ToArray()
                    .Any();
                if (hasChildCollections)
                    return;
                var hasMediaItems = GetMediaCollectionItems(parentCollection.Id)
                    .Select(mi => { Release(mi); return mi; })
                    .ToArray()
                    .Any();
                if (hasMediaItems)
                    return;
            }
            finally
            {
                Release(parentCollection);
            }
            Delete(parentCollection);
        }
        public IEnumerable<MediaCollection> GetUnclassifiedMediaCollections()
        {
            var collectionIds = _Database
                .GetAll<MediaDataItemCollection>(
                    new KeyValuePair<string, object>(nameof(MediaDataItemCollection.Classified), false))
                .Select(c => c.Id);
            foreach (var id in collectionIds)
                yield return GetMediaCollection(id);
        }
        public IEnumerable<MediaCollection> GetChildMediaCollections(long parentId)
        {
            var collectionIds = _Database
                .GetAll<MediaDataItemCollection>(
                    new KeyValuePair<string, object>(nameof(MediaDataItemCollection.ParentId), parentId))
                .Select(c => c.Id);
            foreach (var id in collectionIds)
                yield return GetMediaCollection(id);
        }
        #endregion
        #region MediaItem
        public void Delete(MediaItem mediaItem)
        {
            Delete(mediaItem, false);
        }
        public void Delete(MediaItem mediaItem, bool deleteBelongingItems)
        {
            if (deleteBelongingItems)
            {
                var mediaItems = GetMediaCollectionItems(mediaItem.ParentCollectionId)
                    .Where(mi =>
                    {
                        var result = mi.Id != mediaItem.Id;
                        result &= mi.Name.StartsWith(Path.GetFileNameWithoutExtension(mediaItem.Name));
                        Release(mi);
                        return result;
                    })
                    .ToArray();
                foreach (var mi in mediaItems)
                    Delete(mi, false);
            }

            var dbModel = mediaItem.GetDatabaseModel() as MediaDataItem;
            if (!_Database.Delete<MediaDataItem>(dbModel))
                throw new ApplicationException($"Media item could not be deleted.");
        }
        public IEnumerable<MediaItem> GetMediaItems()
        {
            return _Database
                .GetAll<MediaDataItem>()
                .Select(c => c.Id)
                .Select(id => GetMediaItem(id));
        }
        public IEnumerable<MediaItem> GetCopyMediaItems(long originalId)
        {
            var itemIds = _Database.GetAll<MediaDataItem>(
                                        new KeyValuePair<string, object>(nameof(MediaDataItem.OriginalMediaItemId), originalId))
                                   .Select(c => c.Id);
            foreach (var itemId in itemIds)
                yield return GetMediaItem(itemId);
        }
        public IEnumerable<MediaItem> GetDueMediaItems()
        {
            var dueDate = DateTime.Now;
            var itemIds = _Database.GetAll<MediaDataItem>(
                                        new KeyValuePair<string, object>(nameof(MediaDataItem.CopyType), DataMediaItemCopyType.Cache))
                                   .Where(c => c.DueDate < dueDate)
                                   .Select(c => c.Id)
                                   .Concat(_Database.GetAll<MediaDataItem>(
                                                new KeyValuePair<string, object>(nameof(MediaDataItem.CopyType), DataMediaItemCopyType.Download))
                                           .Where(c => c.DueDate < dueDate)
                                           .Select(c => c.Id));
            foreach (var itemId in itemIds)
                yield return GetMediaItem(itemId);
        }
        public IEnumerable<MediaItem> GetMediaItemsThatNeedsPictureUpdate()
        {
            var itemIds = _Database.GetAll<MediaDataItem>(
                                        new KeyValuePair<string, object>(nameof(MediaDataItem.NeedsPictureUpdate), true))
                                   .Select(c => c.Id);
            foreach (var itemId in itemIds)
                yield return GetMediaItem(itemId);
        }
        public MediaItem GetMediaItemByPath(long collectionId, string fullPath)
        {
            var mediaItemId = _Database.GetAll<MediaDataItem>(new KeyValuePair<string, object>(nameof(MediaDataItem.ParentCollectionId), collectionId),
                                                              new KeyValuePair<string, object>(nameof(MediaDataItem.Path), fullPath))
                                       .Select(c => c.Id)
                                       .FirstOrDefault();
            if (mediaItemId == 0)
                return null;
            return GetMediaItem(mediaItemId);
        }
        public MediaItem GetMediaItemByPath(string relPath)
        {
            var mediaItemId = _Database.GetAll<MediaDataItem>(new KeyValuePair<string, object>(nameof(MediaDataItem.Path), relPath))
                                       .Select(c => c.Id)
                                       .FirstOrDefault();
            if (mediaItemId == 0)
                return null;
            return GetMediaItem(mediaItemId);
        }
        public IEnumerable<MediaItem> GetMediaItems(params MediaItemCopyType[] copyType)
        {
            var copyTypes = copyType.Select(ct => (DataMediaItemCopyType)ct).ToArray();
            var cache = GetModelCache(typeof(MediaItem));
            var itemIds = copyTypes.SelectMany(ct => _Database.GetAll<MediaDataItem>(new KeyValuePair<string, object>(nameof(MediaDataItem.CopyType), ct))
                .Select(mi => mi.Id));
            foreach (var itemId in itemIds)
                yield return GetMediaItem(itemId);
        }
        public MediaItem GetMediaItem(long id)
        {
            var cache = GetModelCache(typeof(MediaDataItem));
            var item = cache.GetServiceModel<MediaItem>(id) as MediaItem;
            return item;
        }

        public MediaItem AddOrUpdateMediaItem(MediaItem mediaItem)
        {
            AddOrUpdate(mediaItem);
            return mediaItem;
        }
        public IEnumerable<MediaItem> GetUnclassifiedMediaItems()
        {
            var cache = GetModelCache(typeof(MediaItem));
            var items = cache
                .GetAll()
                .Cast<MediaDataItem>()
                .Where(item => !item.Classified)
                .OrderBy(item => item.LastClassificationTry);
            foreach (var item in items)
                yield return GetMediaItem(item.Id);

            var itemIds = _Database.GetAll<MediaDataItem>(new KeyValuePair<string, object>(nameof(MediaDataItem.Classified), false))
                                   .OrderBy(item => item.LastClassificationTry)
                                   .Select(c => c.Id)
                                   .ToArray();
            foreach (var itemId in itemIds)
                yield return GetMediaItem(itemId);
        }

        public IEnumerable<MediaItem> GetMediaCollectionItems(long collectionId)
        {
            var itemIds = _Database.GetAll<MediaDataItem>(new KeyValuePair<string, object>(nameof(MediaDataItem.ParentCollectionId), collectionId))
                                   .Select(c => c.Id);
            foreach (var itemId in itemIds)
                yield return GetMediaItem(itemId);
        }
        
        #endregion
        #region Movie
        public Movie GetMovieByMediaItem(long mediaItemId)
        {
            var movieId = _Database
                .GetAll<DataClassifiedEntryMediaItem>(
                    new KeyValuePair<string, object>(nameof(DataClassifiedEntryMediaItem.MediaItemId), mediaItemId))
                .Select(c => c.EntryId)
                .FirstOrDefault();
            if (movieId == 0)
                return null;
            return GetMovie(movieId);
        }

        public Movie GetMovie(long id)
        {
            var cache = GetModelCache(typeof(DataClassifiedEntry));
            var source = cache.GetServiceModel<Movie>(id) as Movie;
            if (source is null) return null;
            if (source.Type != EntryType.Movie)
                throw new ArgumentException(nameof(source.Type));
            return source;
        }

        public IEnumerable<Movie> GetMoviesByName(string name)
        {
            var movieIds = _Database
                .GetAll<DataClassifiedEntry>(
                    new KeyValuePair<string, object>(nameof(DataClassifiedEntry.Type), DataEntryType.Movie),
                    new KeyValuePair<string, object>(nameof(DataClassifiedEntry.Name), name))
                .Select(c => c.Id);
            foreach (var itemId in movieIds)
                yield return GetMovie(itemId);
        }

        public IEnumerable<Movie> GetCollectionMovies(long collectionId)
        {
            var movieIds = _Database
                .GetAll<DataClassifiedEntry>(
                    new KeyValuePair<string, object>(nameof(DataClassifiedEntry.Type), DataEntryType.Movie),
                    new KeyValuePair<string, object>(nameof(DataClassifiedEntry.CollectionId), collectionId))
                .Select(c => c.Id);
            foreach (var itemId in movieIds)
                yield return GetMovie(itemId);
        }

        public Movie AddOrUpdateMovie(Movie movie)
        {
            AddOrUpdate(movie);
            UpdateMovieMediaItems(movie.GetDatabaseModel() as DataClassifiedEntry, movie.Id, movie.MediaItemIds);
            return movie;
        }
        public void Delete(Movie movie)
        {
            var cache = GetModelCache(typeof(DataClassifiedEntry));
            cache.Remove(movie);

            List<long> collectionIds = new List<long>();
            foreach (var mediaItemId in movie.MediaItemIds)
            {
                var mediaItem = GetMediaItem(mediaItemId);
                if (mediaItem is null)
                    continue;
                collectionIds.Add(mediaItem.ParentCollectionId);
                Delete(mediaItem, true);
            }

            var movieCollection = GetMovieCollection(movie.CollectionId);

            var roles = GetRoles(movie.Id);
            foreach (var role in roles)
                Delete(role);

            var dbModel = movie.GetDatabaseModel() as DataClassifiedEntry;
            if (!_Database.Delete<DataClassifiedEntry>(dbModel))
                throw new ApplicationException($"Movie item could not be deleted.");


            if (movieCollection is null)
            {
                foreach (var collectionId in collectionIds.Distinct())
                {
                    var collection = GetMediaCollection(collectionId);
                    if (collection is null)
                        continue;
                    Delete(collection);
                }
            }
            else
                try
                {
                    var hasRemainingMovies = GetCollectionMovies(movieCollection.Id)
                        .Select(col => { Release(col); return col; })
                        .ToArray()
                        .Any();
                    if (hasRemainingMovies)
                        return;
                    Delete(movieCollection);
                }
                finally
                {
                    Release(movieCollection);
                }
        }
        public IEnumerable<Movie> GetMovies()
        {
            return _Database.GetAll<DataClassifiedEntry>(
                    new KeyValuePair<string, object>(nameof(DataClassifiedEntry.Type), EntryType.Movie))
                            .Select(c => c.Id)
                            .Select(id => GetClassifiedEntry(id))
                            .Cast<Movie>();
        }
        #endregion
        private void UpdateMovieMediaItems(DataClassifiedEntry dbModel, long id, long[] mediaItemIds)
        {
            var newEntries = mediaItemIds.Select(
                mediaItemId => new DataClassifiedEntryMediaItem()
                {
                    EntryId = id,
                    MediaItemId = mediaItemId
                }
                ).ToArray();
            var oldEntries = _Database.GetAll<DataClassifiedEntryMediaItem>(
                new KeyValuePair<string, object>(nameof(DataClassifiedEntryMediaItem.EntryId), id))
                .ToArray();

            var entriesToDelete = oldEntries.Where(oE => !newEntries.Any(nE => nE.MediaItemId == oE.MediaItemId)).ToArray();
            var entriesToAdd = newEntries.Where(nE => !oldEntries.Any(oE => oE.MediaItemId == nE.MediaItemId)).ToArray();
            foreach (var entryToAdd in entriesToAdd)
                _Database.AddOrUpdate(entryToAdd);
            foreach (var entryToDelete in entriesToDelete)
                _ = _Database.Delete(entryToDelete);
        }
        #region MovieCollection
        public MovieCollection GetMovieCollection(long id)
        {
            if (id == 0)
                return null;
            var cache = GetModelCache(typeof(DataClassifiedEntry));
            var source = cache.GetServiceModel<MovieCollection>(id);
            if (source is null) return null;
            if (source.Type != EntryType.MovieCollection)
                throw new ArgumentException(nameof(source.Type));
            return source;
        }

        public MovieCollection AddOrUpdateMovieCollection(MovieCollection movieCollection)
        {
            AddOrUpdate(movieCollection);
            return movieCollection;
        }
        public void Delete(MovieCollection movieCollection)
        {
            var cache = GetModelCache(typeof(DataClassifiedEntry));
            cache.Remove(movieCollection);

            var movies = GetCollectionMovies(movieCollection.Id)
                .Select(m => { Release(m); return m; })
                .ToArray();
            foreach (var movie in movies)
                Delete(movie);

            var collection = GetMediaCollection(movieCollection.MediaItemCollectionId);

            var dbModel = movieCollection.GetDatabaseModel() as DataClassifiedEntry;
            if (!_Database.Delete<DataClassifiedEntry>(dbModel))
                throw new ApplicationException($"Movie item could not be deleted.");
            if (collection is null)
                return;
            try
            {
                var hasChildCollections = GetChildMediaCollections(collection.Id)
                    .Select(col => { Release(col); return col; })
                    .ToArray()
                    .Any();
                if (hasChildCollections)
                    return;
                var hasMediaItems = GetMediaCollectionItems(collection.Id)
                    .Select(mi => { Release(mi); return mi; })
                    .ToArray()
                    .Any();
                if (hasMediaItems)
                    return;
            }
            finally
            {
                Release(collection);
            }
            Delete(collection);
        }
        public MovieCollection GetMovieCollectionByMediaCollection(long mediaCollectionId)
        {
            var collectionId = _Database
                .GetAll<DataClassifiedEntry>(
                new KeyValuePair<string, object>(nameof(DataClassifiedEntry.Type), DataEntryType.MovieCollection),
                new KeyValuePair<string, object>(nameof(DataClassifiedEntry.MediaItemCollectionId), mediaCollectionId))
                .Select(c => c.Id)
                .FirstOrDefault();
            if (collectionId == 0)
                return null;
            return GetMovieCollection(collectionId);
        }
        #endregion
        public ClassifiedEntry AddOrUpdateEntry(ClassifiedEntry entry)
        {
            AddOrUpdate(entry);
            var collectionEntry = entry as IMediaItemCollectionEntry;
            if (collectionEntry is not null)
                UpdateMovieMediaItems(entry.GetDatabaseModel() as DataClassifiedEntry, entry.Id, collectionEntry.MediaItemIds);
            return entry;
        }

        public IEnumerable<ClassifiedEntry> GetOverview(int offset, int count, string genre, params EntryType[] entryTypes)
        {
            var entryIds = _Database.GetAll<DataClassifiedEntry>(offset, count,
                    new Filter() { Name = nameof(DataClassifiedEntry.Enabled), Value = true },
                    new Filter() { Name = nameof(DataClassifiedEntry.Visible), Value = true },
                    new Filter() { Name = nameof(DataClassifiedEntry.Type), Value = entryTypes },
                    new Filter() { Name = string.IsNullOrWhiteSpace(genre) ? "" : nameof(DataClassifiedEntry.Genre), Value = genre, Type = FilterType.Contains })
                                    .Select(c => c.Id);
            foreach (var itemId in entryIds)
                yield return GetClassifiedEntry(itemId);
        }
        public ClassifiedEntry GetClassifiedEntry(long id)
        {
            var cache = GetModelCache(typeof(DataClassifiedEntry));
            var source = cache.GetServiceModel<ClassifiedEntry>(id) as ClassifiedEntry;
            return source;
        }
        public IEnumerable<ClassifiedEntry> GetClassifiedEntriesWithPicture(string name)
        {
            var entryIds = _Database.GetAll<DataClassifiedEntry>()
                                    .Where(entry => 
                                    { 
                                        var result = !string.IsNullOrWhiteSpace(entry.PicturePath) && entry.PicturePath.EndsWith(name);
                                        result |= !string.IsNullOrWhiteSpace(entry.BannerPath) && entry.BannerPath.EndsWith(name);
                                        return result;
                                    })                                    
                                    .Select(c => c.Id);
            foreach (var itemId in entryIds)
                yield return GetClassifiedEntry(itemId);
        }
        #region TVShowEpisode
        public TVShowEpisode GetTVShowEpisodeByMediaItem(long mediaItemId)
        {
            var episodeId = _Database
                .GetAll<DataClassifiedEntryMediaItem>(
                    new KeyValuePair<string, object>(nameof(DataClassifiedEntryMediaItem.MediaItemId), mediaItemId))
                .Select(c => c.EntryId)
                .FirstOrDefault();
            if (episodeId == 0)
                return null;
            return GetTVShowEpisode(episodeId);
        }

        public TVShowEpisode GetTVShowEpisodeByIdentification(string showName, int seasonNo, int episode, string part)
        {
            var show = GetShowByName(showName);
            if (show is null) return null;
            var season = GetShowSeason(show, seasonNo);
            if (season is null)
                return null;

            var episodeId = _Database
                .GetAll<DataClassifiedEntry>(
                    new KeyValuePair<string, object>(nameof(DataClassifiedEntry.Type), DataEntryType.TVShowEpisode),
                    new KeyValuePair<string, object>(nameof(DataClassifiedEntry.CollectionId), season.Id),
                    new KeyValuePair<string, object>(nameof(DataClassifiedEntry.Number), episode))
                .Select(c => c.Id)
                .FirstOrDefault();
            if (episodeId == 0)
                return null;
            return GetTVShowEpisode(episodeId);
        }

        public TVShowEpisode AddOrUpdateEpisode(TVShowEpisode episode)
        {
            AddOrUpdate(episode);
            UpdateMovieMediaItems(episode.GetDatabaseModel() as DataClassifiedEntry, episode.Id, episode.MediaItemIds);
            return episode;
        }
        public void Delete(TVShowEpisode episode)
        {
            var cache = GetModelCache(typeof(DataClassifiedEntry));
            cache.Remove(episode);

            List<long> collectionIds = new List<long>();
            foreach (var mediaItemId in episode.MediaItemIds)
            {
                var mediaItem = GetMediaItem(mediaItemId);
                if (mediaItem is null)
                    continue;
                collectionIds.Add(mediaItem.ParentCollectionId);
                Delete(mediaItem, true);
            }

            foreach (var collectionId in collectionIds.Distinct())
            {
                var collection = GetMediaCollection(collectionId);
                if (collection is null)
                    continue;
                Delete(collection);
            }

            var dbModel = episode.GetDatabaseModel() as DataClassifiedEntry;
            if (!_Database.Delete<DataClassifiedEntry>(dbModel))
                throw new ApplicationException($"Episode item could not be deleted.");
        }
        public TVShowEpisode GetTVShowEpisode(long id)
        {
            var cache = GetModelCache(typeof(DataClassifiedEntry));
            var source = cache.GetServiceModel<TVShowEpisode>(id) as TVShowEpisode;
            if (source is null) return null;
            if (source.Type != EntryType.TVShowEpisode)
                throw new ArgumentException(nameof(source.Type));
            return source;
        }
        public IEnumerable<TVShowEpisode> GetEpisodes(long seasonId)
        {
            var episodeIds = _Database
                .GetAll<DataClassifiedEntry>(
                    new KeyValuePair<string, object>(nameof(DataClassifiedEntry.Type), DataEntryType.TVShowEpisode),
                    new KeyValuePair<string, object>(nameof(DataClassifiedEntry.CollectionId), seasonId))
                .OrderBy(e => e.Number)
                .ThenBy(e => e.PartNo)
                .ThenBy(e => e.Name)
                .Select(c => c.Id);
            foreach (var id in episodeIds)
                yield return GetTVShowEpisode(id);
        }
        #endregion TVShowEpisode
        #region TVShowSeason
        public TVShowSeason GetShowSeason(TVShow show, int seasonNo)
        {
            var seasonId = _Database
                .GetAll<DataClassifiedEntry>(
                    new KeyValuePair<string, object>(nameof(DataClassifiedEntry.Type), DataEntryType.TVShowSeason),
                    new KeyValuePair<string, object>(nameof(DataClassifiedEntry.CollectionId), show.Id),
                    new KeyValuePair<string, object>(nameof(DataClassifiedEntry.Number), seasonNo))
                .Select(c => c.Id)
                .FirstOrDefault();
            if (seasonId == 0)
                return null;
            return GetTVShowSeason(seasonId);
        }
        public TVShowSeason GetTVShowSeason(long id)
        {
            var cache = GetModelCache(typeof(DataClassifiedEntry));
            var source = cache.GetServiceModel<TVShowSeason>(id) as TVShowSeason;
            if (source is null) return null;
            if (source.Type != EntryType.TVShowSeason)
                throw new ArgumentException(nameof(source.Type));
            return source;
        }
        public TVShowSeason AddOrUpdateSeason(TVShowSeason season)
        {
            AddOrUpdate(season);
            return season;
        }
        public IEnumerable<TVShowSeason> GetSeasons(long showId)
        {
            var seasonIds = _Database
                .GetAll<DataClassifiedEntry>(
                    new KeyValuePair<string, object>(nameof(DataClassifiedEntry.Type), DataEntryType.TVShowSeason),
                    new KeyValuePair<string, object>(nameof(DataClassifiedEntry.CollectionId), showId))
                .OrderBy(c => c.Number)
                .ThenBy(c => c.PartNo)
                .ThenBy(c => c.Name)
                .Select(c => c.Id);
            foreach (var id in seasonIds)
                yield return GetTVShowSeason(id);
        }
        #endregion
        #region TVShow
        public TVShow GetShowByName(string name)
        {
            var showId = _Database
                .GetAll<DataClassifiedEntry>(
                    new KeyValuePair<string, object>(nameof(DataClassifiedEntry.Type), DataEntryType.TVShow),
                    new KeyValuePair<string, object>(nameof(DataClassifiedEntry.Name), name))
                .Select(c => c.Id)
                .FirstOrDefault();
            if (showId == 0)
                return null;
            return GetTVShow(showId);
        }
        public TVShow GetTVShow(long id)
        {
            var cache = GetModelCache(typeof(DataClassifiedEntry));
            var source = cache.GetServiceModel<TVShow>(id) as TVShow;
            if (source is null) return null;
            if (source.Type != EntryType.TVShow)
                throw new ArgumentException(nameof(source.Type));
            return source;
        }

        public TVShow AddOrUpdateTVShow(TVShow show)
        {
            AddOrUpdate(show);
            return show;
        }
        #endregion

        #region Genres
        public IEnumerable<Genre> GetGenres()
        {
            var Ids = _Database
                .GetAll<DataGenre>()
                .Select(c => c.Id);
            foreach (var id in Ids)
                yield return GetGenre(id);
        }
        public Genre GetGenre(long id)
        {
            var cache = GetModelCache(typeof(DataGenre));
            var genre = cache.GetServiceModel<Genre>(id) as Genre;
            return genre;
        }
        public Genre AddOrUpdateGenre(Genre genre)
        {
            AddOrUpdate(genre);
            UpdateGenreNames(genre);
            return genre;
        }

        private void UpdateGenreNames(Genre genre)
        {
            var genreNames = _Database.GetAll<DataGenreName>(new KeyValuePair<string, object>(nameof(DataGenreName.DataGenreId), genre.Id)).ToArray();
            var genreNamesToAdd = genre.AlternateNames.Where(n => !genreNames.Any(gN => gN.Name == n.Name)).ToArray();
            var genreNamesToRemove = genreNames.Where(gN => !genre.AlternateNames.Any(g => g.Name == gN.Name)).ToArray();
            foreach (var genreName in genreNamesToAdd)
            {
                genreName.GenreId = genre.Id;
                var replace = genreNamesToRemove.FirstOrDefault();
                if (replace is not null)
                {
                    genreName.Id = replace.Id;
                    genreNamesToRemove = genreNamesToRemove.Skip(1).ToArray();
                }
                var dbModel = genreName.GetDatabaseModel() as DataGenreName;
                _Database.AddOrUpdate<DataGenreName>(dbModel);
                genreName.Id = dbModel.Id;
                genreName.CreatedAt = dbModel.CreatedAt;
                dbModel.SetRestorePoint();
            }
            foreach (var genreName in genreNamesToRemove)
                _Database.Delete<DataGenreName>(genreName);

        }


        #endregion
        #region Playlists 
        public IEnumerable<Playlist> GetPlaylists(Models.Playlists.PlaylistType general)
        {
            var entryIds = _Database.GetAll<DataPlaylist>(
                new KeyValuePair<string, object>(nameof(DataPlaylist.Type), (DataPlaylist.PlaylistType)general))
                                    .Select(c => c.Id);
            foreach (var itemId in entryIds)
                yield return GetPlaylist(itemId);
        }
        public Playlist GetPlaylist(long id)
        {
            var cache = GetModelCache(typeof(DataPlaylist));
            var source = cache.GetServiceModel<Playlist>(id) as Playlist;
            return source;
        }

        public Playlist AddOrUpdatePlaylist(Playlist playlist)
        {
            AddOrUpdate(playlist);
            UpdatePlaylistEntries(playlist);
            return playlist;
        }

        private void UpdatePlaylistEntries(Playlist playlist)
        {
            var oldEntries = _Database.GetAll<DataPlaylistEntry>(
                new KeyValuePair<string, object>(nameof(DataPlaylistEntry.PlaylistId), playlist.Id))
                .OrderBy(e => e.Id)
                .ToArray();
            int offset = 0;
            foreach (var item in playlist.Items)
            {
                if (offset <= oldEntries.GetUpperBound(0))
                {
                    item.Id = oldEntries[offset].Id;
                    oldEntries[offset].EntryId = item.EntryId;
                    oldEntries[offset].MediaItemId = item.MediaItemId;
                    _Database.AddOrUpdate(oldEntries[offset]);
                }
                else
                {
                    var newEntry = new DataPlaylistEntry()
                    {
                        PlaylistId = playlist.Id,
                        EntryId = item.EntryId,
                        MediaItemId = item.MediaItemId
                    };
                    _Database.AddOrUpdate(newEntry);
                    item.Id = newEntry.Id;
                }
                offset += 1;
            }
            var entriesToDelete = oldEntries.Skip(offset).ToArray();
            foreach (var entryToDelete in entriesToDelete)
                _ = _Database.Delete(entryToDelete);
        }
        #endregion

        #region Log Entry
        public void AddOrUpdateLogEntry(LogEntry entry)
        {
            AddOrUpdate(entry);
            Release(entry, true);
        }

        public void ClearLogs()
        {
            _Database.Truncate<DataLogEntry>();
        }

        #endregion
        #region Actors
        public Actor AddOrUpdateActor(Actor entry)
        {
            entry = CompleteActor(entry);
            AddOrUpdate(entry);
            return entry;
        }
        public void Delete(Actor actor)
        {
            var roles = GetActorsRoles(actor.Id).ToArray();
            foreach (var role in roles)
                Delete(role);

            var dbModel = actor.GetDatabaseModel() as DataActor;
            if (dbModel is not null)
                if (!_Database.Delete<DataActor>(dbModel))
                    throw new ApplicationException($"Actor could not be deleted.");
        }
        public IEnumerable<Actor> GetActorsByName(string name)
        {
            var entryIds = _Database.GetAll<DataActor>(
                new KeyValuePair<string, object>(nameof(DataActor.Name), name))
                                    .Select(c => c.Id);
            foreach (var itemId in entryIds)
                yield return GetActor(itemId);
        }
        public Actor GetActor(long id)
        {
            var cache = GetModelCache(typeof(DataActor));
            var actor = cache.GetServiceModel<Actor>(id) as Actor;
            actor = CompleteActor(actor);
            return actor;
        }
        public IEnumerable<Actor> GetActorsThatNeedsPictureUpdate()
        {
            var entryIds = _Database.GetAll<DataActor>(
                new KeyValuePair<string, object>(nameof(DataActor.NeedsPictureUpdate), true))
                                    .Select(c => c.Id);
            foreach (var itemId in entryIds)
                yield return GetActor(itemId);
        }
        public IEnumerable<Actor> GetActorsWithPicture(string pictureFileName)
        {
            var entryIds = _Database.GetAll<DataActor>()
                                    .Where(entry =>
                                    {
                                        var result = !string.IsNullOrWhiteSpace(entry.PicturePath) && entry.PicturePath.EndsWith(pictureFileName);
                                        result |= !string.IsNullOrWhiteSpace(entry.BannerPath) && entry.BannerPath.EndsWith(pictureFileName);
                                        return result;
                                    })
                                    .Select(c => c.Id);
            foreach (var itemId in entryIds)
                yield return GetActor(itemId);
        }
        private Actor CompleteActor(Actor actor)
        {
            if (actor is not null)
            if (!actor.RoleCountUpdated)
            {
                    actor.RoleCount = _Database
                        .GetAll<DataRole>(new KeyValuePair<string, object>(nameof(DataRole.ActorId), actor.Id))
                        .Count();
                    actor.RoleCountUpdated = true;
            }
            return actor;
        }
        #endregion
        #region Roles
        public Role AddOrUpdateRole(Role entry)
        {
            AddOrUpdate(entry);
            return entry;
        }
        public IEnumerable<Role> GetRoles(long entryId)
        {
            var entryIds = _Database.GetAll<DataRole>(
                new KeyValuePair<string, object>(nameof(DataRole.EntryId), entryId))
                                    .Select(c => c.Id);
            foreach (var itemId in entryIds)
                yield return GetRole(itemId);
        }
        public IEnumerable<Role> GetActorsRoles(long actorId)
        {
            var entryIds = _Database.GetAll<DataRole>(
                new KeyValuePair<string, object>(nameof(DataRole.ActorId), actorId))
                                    .Select(c => c.Id);
            foreach (var itemId in entryIds)
                yield return GetRole(itemId);
        }
        public Role GetRole(long id)
        {
            var cache = GetModelCache(typeof(DataRole));
            var source = cache.GetServiceModel<Role>(id) as Role;
            return source;
        }
        public void Delete(Role role)
        {
            var actor = GetActor(role.ActorId);
            try
            {
                var dbModel = role.GetDatabaseModel() as DataRole;
                if (dbModel is not null)
                    if (!_Database.Delete<DataRole>(dbModel))
                        throw new ApplicationException($"Role could not be deleted.");

                if (actor is not null)
                {
                    var remainingRoles = GetActorsRoles(actor.Id)
                        .Where(role => role is not null)
                        .Select(role => { Release(role); return role; })
                        .ToArray();
                    if (remainingRoles.Any())
                    {
                        actor.RoleCount = remainingRoles.Length;
                        actor.RoleCountUpdated = true;
                        return;
                    }
                }
            }
            finally
            {
                Release(actor);
            }
            Delete(actor);
        }
        #endregion

        public void Release(IEnumerable<BaseServiceModel> entries)
        {
            Release(entries, false);
        }
        public void Release(IEnumerable<BaseServiceModel> entries, bool force)
        {
            foreach (var entry in entries)
                Release(entry, force);
        }
        public void Release(BaseServiceModel entry)
        {
            Release(entry, false);
        }
        public void Release(BaseServiceModel entry, bool force)
        {
            if (entry is null) return;
            var attr = entry.GetType().GetCustomAttribute<DataModelReferenceAttribute>() as DataModelReferenceAttribute;
            if (attr is null) return;
            var cache = GetModelCache(attr.DataModelType);
            lock (cache)
                cache.Release(entry, force);
        }
        public void Hold(BaseServiceModel entry)
        {
            if (entry is null) return;
            var attr = entry.GetType().GetCustomAttribute<DataModelReferenceAttribute>() as DataModelReferenceAttribute;
            if (attr is null) return;
            var cache = GetModelCache(attr.DataModelType);
            lock(cache)
                cache.Hold(entry);
        }

        private DateTime _LastReleaseCheck = DateTime.Now;
        private TimeSpan _ReleaseCheckInterval = TimeSpan.FromSeconds(60);
        protected override Task ExecuteTimerAsync()
        {
            if (_LastReleaseCheck.Add(_ReleaseCheckInterval) > DateTime.Now)
                return Task.CompletedTask;
            _LastReleaseCheck = DateTime.Now;
            foreach (var cache in _ModelCaches.Values)
                cache.CheckReleases();
            return Task.CompletedTask;
        }
    }
}
