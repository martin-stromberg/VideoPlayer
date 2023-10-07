using SQLite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using VideoPlayerLib.Services.Database.Models;

namespace VideoPlayerLib.Services.Database
{
    public class MediaLibraryDatabase : IMediaLibraryDatabase, ILogDatabase
    {
        private readonly MediaLibraryDatabaseSettings settings;
        private SQLiteAsyncConnection connection;

        public MediaLibraryDatabase(MediaLibraryDatabaseSettings settings)
        {
            this.settings = settings;
        }

        protected SQLiteAsyncConnection Connection
        {
            get
            {
                if (connection == null)
                    connection = new SQLiteAsyncConnection(settings.FilePath, settings.OpenFlags);
                return connection;
            }
        } 

        private async Task InitOrUpgradeAsync()
        {
            var result = await Connection.CreateTableAsync<Models.MediaSource>();
            result = await Connection.CreateTableAsync<Models.MediaCollection>();
            result = await Connection.CreateTableAsync<Models.MediaItem>();
            result = await Connection.CreateTableAsync<Models.LogEntry>();
            result = await Connection.CreateTableAsync<Models.TVShow>();
            result = await Connection.CreateTableAsync<Models.TVShowSeason>();
            result = await Connection.CreateTableAsync<Models.TVShowEpisode>();
            result = await Connection.CreateTableAsync<Models.TVShowEpisodeMediaItem>();
            result = await Connection.CreateTableAsync<Models.Movie>();
            result = await Connection.CreateTableAsync<Models.MovieCollection>();
            result = await Connection.CreateTableAsync<Models.MovieMediaItem>();            
        }

        public async Task<AsyncTableQuery<MediaSource>> GetSourcesAsync()
        {
            await InitOrUpgradeAsync();
            return Connection.Table<Models.MediaSource>();
        }
        public async Task<Models.MediaSource> GetSourceAsync(long id)
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<Models.MediaSource>()
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<AsyncTableQuery<Models.MediaCollection>> GetMediaCollectionsAsync()
        {
            await InitOrUpgradeAsync();
            return Connection.Table<Models.MediaCollection>();
        }
        public async Task<Models.MediaCollection> GetMediaCollectionAsync(long id)
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<Models.MediaCollection>()
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<AsyncTableQuery<MediaItem>> GetMediaItemsAsync()
        {
            await InitOrUpgradeAsync();
            return Connection.Table<Models.MediaItem>();
        }
        public async Task<Models.MediaItem> GetMediaItemAsync(long id)
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<Models.MediaItem>()
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<MediaSource> AddOrUpdateSourceAsync(MediaSource mediaSource)
        {
            return await AddOrUpdate<MediaSource>(mediaSource) as MediaSource;
        }

        private async Task<BaseDataModel> AddOrUpdate<T>(T model) where T : new()
        {
            var dataModel = model as BaseDataModel;
            if (dataModel == null)
                throw new ArgumentException(nameof(model));            

            await InitOrUpgradeAsync();
            Debug.WriteLine($"AddOrUpdate({typeof(T)})");
            var existing = (await Connection.Table<T>()
                .ToArrayAsync())                
                .FirstOrDefault(rec => (rec as BaseDataModel).IsRecord(model as BaseDataModel)) as BaseDataModel;
            if (existing == null)
            {
                Debug.WriteLine($"AddOrUpdate({typeof(T)}).Insert");
                await Connection.InsertAsync(model);
                return dataModel;
            }

            Debug.WriteLine($"AddOrUpdate({typeof(T)}).Update");
            existing.Update(dataModel);
            await Connection.UpdateAsync(existing);
            return dataModel;
        }

        public async Task<MediaCollection> AddOrUpdateMediaCollectionAsync(MediaCollection collection)
        {
            return await AddOrUpdate<MediaCollection>(collection) as MediaCollection;
        }

        public async Task<MediaItem> AddOrUpdateMediaItemAsync(MediaItem mediaItem)
        {
            return await AddOrUpdate<MediaItem>(mediaItem) as MediaItem;
        }

        public async Task RemoveMediaCollection(MediaCollection collection)
        {
            await Connection.DeleteAsync(collection);
        }

        public async Task RemoveMediaItem(MediaItem mediaItem)
        {
            await Connection.DeleteAsync(mediaItem);
        }

        public async Task RemoveMovie(long movieId)
        {
            var movie = await GetMovie(movieId);
            await Connection.DeleteAsync(movie);
        }

        public async Task RemoveTVShow(long id)
        {
            var show = await GetTVShow(id);
            var seasons = await GetTVShowSeasons(show.Id);
            await RemoveTVShowSeasons(seasons);
            await Connection.DeleteAsync(show);
        }

        private async Task RemoveTVShowSeasons(IEnumerable<TVShowSeason> seasons)
        {
            foreach (var season in seasons)
                await RemoveTVShowSeason(season);
        }

        private async Task RemoveTVShowSeason(TVShowSeason season)
        {
            var episodes = await GetTVShowEpisodes(season.Id);
            await RemoveTVShowEpisodes(episodes);
            await Connection.DeleteAsync(season);
        }

        private async Task RemoveTVShowEpisodes(IEnumerable<TVShowEpisode> episodes)
        {
            foreach (var episode in episodes)
                await RemoveTVShowEpisode(episode);
        }

        private async Task RemoveTVShowEpisode(TVShowEpisode episode)
        {
            await Connection.DeleteAsync(episode);
        }

        public async Task AddLog(LogEntry entry)
        {
            await InitOrUpgradeAsync();
            _ = await AddOrUpdate<LogEntry>(entry) as LogEntry;
        }

        public async Task<IEnumerable<LogEntry>> GetLogs()
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<LogEntry>().ToArrayAsync();
        }

        public async Task RemoveLog(LogEntry log)
        {            
            await Connection.DeleteAsync(log);
        }

        public async Task<Movie> GetMovieByMediaItem(long mediaItemId)
        {
            await InitOrUpgradeAsync();
            var movieId = (await Connection.Table<MovieMediaItem>().FirstOrDefaultAsync(mmi => mmi.MediaItemId == mediaItemId))?.MovieId;
            return await Connection.Table<Movie>().FirstOrDefaultAsync(m => m.Id == movieId);
        }

        public async Task<IEnumerable<MovieMediaItem>> GetMovieMediaItems(long movieId)
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<MovieMediaItem>().Where(mmi => mmi.MovieId == movieId).ToArrayAsync();
        }

        public async Task RemoveMovieMediaItemsAsync(long movieId)
        {
            var items = await Connection.Table<MovieMediaItem>().Where(mmi => mmi.MovieId == movieId).ToArrayAsync();
            if (items.Any())
                foreach (var item in items)
                    await Connection.DeleteAsync(item);
        }

        public async Task<Movie> AddOrUpdateMovie(Movie movie)
        {
            return await AddOrUpdate<Movie>(movie) as Movie;
        }

        public async Task<MovieMediaItem> AddMovieMediaItem(MovieMediaItem mediaItem)
        {
            return await AddOrUpdate<MovieMediaItem>(mediaItem) as MovieMediaItem;
        }

        public async Task<IEnumerable<TVShow>> GetTVShowsByName(string name)
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<TVShow>().Where(mmi => mmi.Name == name).ToArrayAsync();
        }

        public async Task<TVShow> GetTVShow(long id)
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<TVShow>().FirstOrDefaultAsync(mmi => mmi.Id == id);
        }

        public async Task<TVShow> AddOrUpdateTVShow(TVShow show)
        {
            return await AddOrUpdate<TVShow>(show) as TVShow;
        }

        public async Task<TVShowSeason> AddOrUpdateTVShowSeason(TVShowSeason season)
        {
            return await AddOrUpdate<TVShowSeason>(season) as TVShowSeason;
        }

        public async Task<TVShowEpisode> AddOrUpdateTVShowEpisode(TVShowEpisode episode)
        {
            return await AddOrUpdate<TVShowEpisode>(episode) as TVShowEpisode;
        }

        public async Task<IEnumerable<TVShowSeason>> GetTVShowSeasons(long showId)
        {
            await InitOrUpgradeAsync();
            return await Connection
                .Table<TVShowSeason>()
                .Where(mmi => mmi.ShowId == showId)
                .ToArrayAsync();
        }

        public async Task<IEnumerable<TVShowEpisode>> GetTVShowEpisodes(long seasonId)
        {
            await InitOrUpgradeAsync();
            return await Connection
                .Table<TVShowEpisode>()
                .Where(mmi => mmi.SeasonId == seasonId)
                .ToArrayAsync();
        }

        public async Task RemoveTVShowEpisodeMediaItemsAsync(long episodeId)
        {
            var items = await Connection.Table<TVShowEpisodeMediaItem>().Where(mmi => mmi.EpisodeId == episodeId).ToArrayAsync();
            if (items.Any())
                foreach (var item in items)
                    await Connection.DeleteAsync(item);
        }

        public async Task<TVShowEpisodeMediaItem> AddTVShowEpisodeMediaItem(TVShowEpisodeMediaItem mediaItem)
        {
            return await AddOrUpdate<TVShowEpisodeMediaItem>(mediaItem) as TVShowEpisodeMediaItem;
        }

        public async Task<IEnumerable<TVShowEpisodeMediaItem>> GetTVShowEpisodeMediaItems(long episodeId)
        {
            await InitOrUpgradeAsync();
            return await Connection
                .Table<TVShowEpisodeMediaItem>()
                .Where(mmi => mmi.EpisodeId == episodeId)
                .ToArrayAsync();
        }

        public async Task<IEnumerable<Movie>> GetMovies()
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<Movie>().ToArrayAsync();
        }

        public async Task<Movie> GetMovie(long id)
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<Movie>().FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<IEnumerable<TVShow>> GetTVShows()
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<TVShow>().ToArrayAsync();
        }

        public async Task<TVShowSeason> GetTVShowSeason(long id)
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<TVShowSeason>().FirstOrDefaultAsync(rec => rec.Id == id);
        }

        public async Task<TVShowEpisode> GetTVShowEpisode(long id)
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<TVShowEpisode>().FirstOrDefaultAsync(rec => rec.Id == id);
        }

        public async Task<IEnumerable<MovieCollection>> GetMovieCollectionsByName(string name)
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<MovieCollection>().Where(coll => coll.Name == name).ToArrayAsync();
        }

        public async Task<MovieCollection> AddOrUpdateMovieCollection(MovieCollection collection)
        {
            return await AddOrUpdate<MovieCollection>(collection) as MovieCollection;
        }
    }
}
