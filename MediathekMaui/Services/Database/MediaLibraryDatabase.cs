using Mediathek.Services.Database.Models;
using SQLite;
using System;
using System.Diagnostics;
using System.Linq;

namespace Mediathek.Services.Database
{
    public class MediaLibraryDatabase: IMediaLibraryDatabase, ILogDatabase, ISettingsDataSource, IJobDatabase
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
            var result = await Connection.CreateTableAsync<MediaSource>();
            result = await Connection.CreateTableAsync<MediaCollection>();
            result = await Connection.CreateTableAsync<Services.Database.Models.MediaItem>();
            result = await Connection.CreateTableAsync<LogEntry>();
            result = await Connection.CreateTableAsync<Services.Database.Models.TVShow>();
            result = await Connection.CreateTableAsync<Services.Database.Models.TVShowSeason>();
            result = await Connection.CreateTableAsync<Services.Database.Models.TVShowEpisode>();
            result = await Connection.CreateTableAsync<TVShowEpisodeMediaItem>();
            result = await Connection.CreateTableAsync<Services.Database.Models.Movie>();
            result = await Connection.CreateTableAsync<Services.Database.Models.MovieCollection>();
            result = await Connection.CreateTableAsync<MovieMediaItem>();
            result = await Connection.CreateTableAsync<Services.Database.Models.Playlist>();
            result = await Connection.CreateTableAsync<Services.Database.Models.PlaylistEntry>();
            result = await Connection.CreateTableAsync<PlaybackHistoryEntry>();
            result = await Connection.CreateTableAsync<Models.Settings>();
            result = await Connection.CreateTableAsync<DownloadJob>();
            result = await Connection.CreateTableAsync<Services.Database.Models.TVShowCollection>();
        }

        public async Task<AsyncTableQuery<MediaSource>> GetSourcesAsync()
        {
            await InitOrUpgradeAsync();
            return Connection.Table<MediaSource>();
        }

        public async Task<MediaSource> GetSourceAsync(long id)
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<MediaSource>().FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<AsyncTableQuery<MediaCollection>> GetMediaCollectionsAsync()
        {
            await InitOrUpgradeAsync();
            return Connection.Table<MediaCollection>();
        }

        public async Task<MediaCollection> GetMediaCollectionAsync(long id)
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<MediaCollection>().FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<AsyncTableQuery<Services.Database.Models.MediaItem>> GetMediaItemsAsync()
        {
            await InitOrUpgradeAsync();
            return Connection.Table<Services.Database.Models.MediaItem>();
        }

        public async Task<Services.Database.Models.MediaItem> GetMediaItemAsync(long id)
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<Services.Database.Models.MediaItem>().FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<MediaSource> AddOrUpdateSourceAsync(MediaSource mediaSource)
        {
            return await AddOrUpdate<MediaSource>(mediaSource) as MediaSource;
        }

        private async Task<BaseDataModel> AddOrUpdate<T>(T model) where T: new()
        {
            var dataModel = model as BaseDataModel;
            if (dataModel == null)
                throw new ArgumentException(nameof(model));

            await InitOrUpgradeAsync();
            Debug.WriteLine($"AddOrUpdate({typeof(T)})");
            var existing = (await Connection.Table<T>().ToArrayAsync())
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

        public async Task<Services.Database.Models.MediaItem> AddOrUpdateMediaItemAsync(Services.Database.Models.MediaItem mediaItem)
        {
            return await AddOrUpdate<Services.Database.Models.MediaItem>(mediaItem) as Services.Database.Models.MediaItem;
        }

        public async Task RemoveMediaCollection(MediaCollection collection)
        {
            await Connection.DeleteAsync(collection);
        }

        public async Task RemoveMediaItem(Services.Database.Models.MediaItem mediaItem)
        {
            await Connection.DeleteAsync(mediaItem);
        }

        public async Task RemoveMovie(long movieId)
        {
            var movie = await GetMovie(movieId);
            var movieMediaItems = await GetMovieMediaItems(movieId);
            foreach (var mmi in movieMediaItems)
                await Connection.DeleteAsync(mmi);
            await Connection.DeleteAsync(movie);
        }

        public async Task RemoveMovieCollection(long id)
        {
            var collection = await GetMovieCollection(id);
            var movies = (await GetMovies()).Where(m => m.CollectionId == id);
            foreach (var movie in movies)
                await RemoveMovie(movie.Id);
            await Connection.DeleteAsync(collection);
        }

        public async Task RemoveTVShow(long id)
        {
            var show = await GetTVShow(id);
            var seasons = await GetTVShowSeasons(show.Id);
            await RemoveTVShowSeasons(seasons);
            await Connection.DeleteAsync(show);
        }

        private async Task RemoveTVShowSeasons(IEnumerable<Services.Database.Models.TVShowSeason> seasons)
        {
            foreach (var season in seasons)
                await RemoveTVShowSeason(season);
        }

        public async Task RemoveTVShowSeason(Services.Database.Models.TVShowSeason season)
        {
            var episodes = await GetTVShowEpisodes(season.Id);
            await RemoveTVShowEpisodes(episodes);
            await Connection.DeleteAsync(season);
        }

        public async Task RemoveTVShowSeason(long id)
        {
            var season = await GetTVShowSeason(id);
            await RemoveTVShowSeason(season);
        }

        private async Task RemoveTVShowEpisodes(IEnumerable<Services.Database.Models.TVShowEpisode> episodes)
        {
            foreach (var episode in episodes)
                await RemoveTVShowEpisode(episode);
        }

        private async Task RemoveTVShowEpisode(Services.Database.Models.TVShowEpisode episode)
        {
            var mediaItems = await GetTVShowEpisodeMediaItems(episode.Id);
            foreach (var mediaItem in mediaItems)
                await Connection.DeleteAsync(mediaItem);
            await Connection.DeleteAsync(episode);
        }

        public async Task RemoveTVShowEpisode(long id)
        {
            var episode = await GetTVShowEpisode(id);
            await RemoveTVShowEpisode(episode);
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

        public async Task<Services.Database.Models.Movie> GetMovieByMediaItem(long mediaItemId)
        {
            await InitOrUpgradeAsync();
            var movieId = (await Connection.Table<MovieMediaItem>()
                                           .FirstOrDefaultAsync(mmi => mmi.MediaItemId == mediaItemId))?.MovieId;
            return await Connection.Table<Services.Database.Models.Movie>().FirstOrDefaultAsync(m => m.Id == movieId);
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

        public async Task<Services.Database.Models.Movie> AddOrUpdateMovie(Services.Database.Models.Movie movie)
        {
            return await AddOrUpdate<Services.Database.Models.Movie>(movie) as Services.Database.Models.Movie;
        }

        public async Task<MovieMediaItem> AddMovieMediaItem(MovieMediaItem mediaItem)
        {
            return await AddOrUpdate<MovieMediaItem>(mediaItem) as MovieMediaItem;
        }

        public async Task<IEnumerable<Services.Database.Models.TVShow>> GetTVShowsByName(string name)
        {
            name = name.ToLower();
            await InitOrUpgradeAsync();
            return await Connection.Table<Services.Database.Models.TVShow>()
                                   .Where(mmi => mmi.Name.ToLower() == name)
                                   .ToArrayAsync();
        }

        public async Task<Services.Database.Models.TVShow> GetTVShow(long id)
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<Services.Database.Models.TVShow>().FirstOrDefaultAsync(mmi => mmi.Id == id);
        }

        public async Task<Services.Database.Models.TVShow> AddOrUpdateTVShow(Services.Database.Models.TVShow show)
        {
            return await AddOrUpdate<Services.Database.Models.TVShow>(show) as Services.Database.Models.TVShow;
        }

        public async Task<Services.Database.Models.TVShowSeason> AddOrUpdateTVShowSeason(Services.Database.Models.TVShowSeason season)
        {
            return await AddOrUpdate<Services.Database.Models.TVShowSeason>(CorrectSeasonName(season)) as Services.Database.Models.TVShowSeason;
        }

        public async Task<Services.Database.Models.TVShowEpisode> AddOrUpdateTVShowEpisode(Services.Database.Models.TVShowEpisode episode)
        {
            return await AddOrUpdate<Services.Database.Models.TVShowEpisode>(CorrectEpisodeName(episode)) as Services.Database.Models.TVShowEpisode;
        }

        private Services.Database.Models.TVShowEpisode CorrectEpisodeName(Services.Database.Models.TVShowEpisode episode)
        {
            if (int.TryParse(episode.EpisodeNo, out var episodeNo))
                episode.EpisodeNo = $"Folge {episodeNo.ToString().PadLeft(2, '0')}";
            return episode;
        }

        public async Task<IEnumerable<Services.Database.Models.TVShowSeason>> GetTVShowSeasons(long showId)
        {
            await InitOrUpgradeAsync();
            return (await Connection
                .Table<Services.Database.Models.TVShowSeason>()
                .Where(mmi => mmi.ShowId == showId)
                .ToArrayAsync())
                .Select(season => CorrectSeasonName(season));
        }

        private Services.Database.Models.TVShowSeason CorrectSeasonName(Services.Database.Models.TVShowSeason season)
        {
            if (int.TryParse(season.Name, out var seasonNo))
                season.Name = $"Staffel {seasonNo.ToString().PadLeft(2, '0')}";
            return season;
        }

        public async Task<IEnumerable<Services.Database.Models.TVShowEpisode>> GetTVShowEpisodes(long seasonId)
        {
            await InitOrUpgradeAsync();
            return (await Connection
                .Table<Services.Database.Models.TVShowEpisode>()
                .Where(mmi => mmi.SeasonId == seasonId)
                .ToArrayAsync())
                .Select(episode => CorrectEpisodeName(episode));
        }

        public async Task RemoveTVShowEpisodeMediaItemsAsync(long episodeId)
        {
            var items = await Connection.Table<TVShowEpisodeMediaItem>()
                                        .Where(mmi => mmi.EpisodeId == episodeId)
                                        .ToArrayAsync();
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

        public async Task<IEnumerable<Services.Database.Models.Movie>> GetMovies()
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<Services.Database.Models.Movie>().ToArrayAsync();
        }

        public async Task<Services.Database.Models.Movie> GetMovie(long id)
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<Services.Database.Models.Movie>().FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<IEnumerable<Services.Database.Models.TVShow>> GetTVShows()
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<Services.Database.Models.TVShow>().ToArrayAsync();
        }

        public async Task<Services.Database.Models.TVShowSeason> GetTVShowSeason(long id)
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<Services.Database.Models.TVShowSeason>()
                                   .FirstOrDefaultAsync(rec => rec.Id == id);
        }

        public async Task<Services.Database.Models.TVShowEpisode> GetTVShowEpisode(long id)
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<Services.Database.Models.TVShowEpisode>()
                                   .FirstOrDefaultAsync(rec => rec.Id == id);
        }

        public async Task<Services.Database.Models.TVShowEpisode> FindTVShowEpisodeByMediaItem(long originalMediaItemId)
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<Services.Database.Models.TVShowEpisode>()
                                   .FirstOrDefaultAsync(rec => rec.PrimaryMediaItemId == originalMediaItemId);
        }

        public async Task<IEnumerable<Services.Database.Models.MovieCollection>> GetMovieCollectionsByName(string name)
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<Services.Database.Models.MovieCollection>()
                                   .Where(coll => coll.Name == name)
                                   .ToArrayAsync();
        }

        public async Task<Services.Database.Models.MovieCollection> AddOrUpdateMovieCollection(Services.Database.Models.MovieCollection collection)
        {
            return await AddOrUpdate<Services.Database.Models.MovieCollection>(collection) as Services.Database.Models.MovieCollection;
        }

        public async Task<IEnumerable<Services.Database.Models.MovieCollection>> GetMovieCollections()
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<Services.Database.Models.MovieCollection>().ToArrayAsync();
        }

        public async Task<Services.Database.Models.MovieCollection> GetMovieCollection(long id)
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<Services.Database.Models.MovieCollection>()
                                   .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task RemoveSource(MediaSource source)
        {
            await InitOrUpgradeAsync();
            await Connection.DeleteAsync(source);
        }

        public async Task<IEnumerable<MovieMediaItem>> GetMovieMediaItemsForMediaItem(long id)
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<MovieMediaItem>().Where(mmi => mmi.MediaItemId == id).ToArrayAsync();
        }

        public async Task RemoveMovieMediaItemAsync(MovieMediaItem movieMediaItem)
        {
            await Connection.DeleteAsync(movieMediaItem);
        }

        public async Task<IEnumerable<TVShowEpisodeMediaItem>> GetTVShowMediaItemsForMediaItem(long id)
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<TVShowEpisodeMediaItem>().Where(mmi => mmi.MediaItemId == id).ToArrayAsync();
        }

        public async Task RemoveMovieMediaItemAsync(TVShowEpisodeMediaItem tvshowMediaItem)
        {
            await Connection.DeleteAsync(tvshowMediaItem);
        }

        public async Task<IEnumerable<Services.Database.Models.Playlist>> GetPlaylists()
        {
            return await Connection.Table<Services.Database.Models.Playlist>().ToArrayAsync();
        }

        public async Task<Services.Database.Models.Playlist> GetPlaylist(long id)
        {
            return await Connection.Table<Services.Database.Models.Playlist>()
                                   .FirstOrDefaultAsync(playlist => playlist.Id == id);
        }

        public async Task<IEnumerable<Services.Database.Models.PlaylistEntry>> GetPlaylistEntries(long id)
        {
            return await Connection.Table<Services.Database.Models.PlaylistEntry>()
                                   .Where(entry => entry.PlaylistId == id)
                                   .ToArrayAsync();
        }

        public async Task<Services.Database.Models.Playlist> AddOrUpdatePlaylistAsync(Services.Database.Models.Playlist playlist)
        {
            return await AddOrUpdate<Services.Database.Models.Playlist>(playlist) as Services.Database.Models.Playlist;
        }

        public async Task<Services.Database.Models.PlaylistEntry> AddOrUpdatePlaylistEntryAsync(Services.Database.Models.PlaylistEntry dataModel2)
        {
            return await AddOrUpdate<Services.Database.Models.PlaylistEntry>(dataModel2) as Services.Database.Models.PlaylistEntry;
        }

        public async Task RemovePlaylist(long id)
        {
            var dbItem = await Connection.Table<Models.Playlist>().FirstOrDefaultAsync(p => p.Id == id);
            if (dbItem is not null)
                await Connection.DeleteAsync(dbItem);
        }

        public async Task RemovePlaylistEntryAsync(Services.Database.Models.PlaylistEntry[] mediaItemsToDelete)
        {
            foreach (var item in mediaItemsToDelete)
                await RemovePlaylistEntryAsync(item);
        }

        public async Task RemovePlaylistEntryAsync(Services.Database.Models.PlaylistEntry mediaItemToDelete)
        {
            await Connection.DeleteAsync(mediaItemToDelete);
        }

        public async Task<IEnumerable<PlaybackHistoryEntry>> GetPlaybackHistoryEntriesAsync()
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<PlaybackHistoryEntry>().ToArrayAsync();
        }

        public async Task<PlaybackHistoryEntry> AddOrUpdatePlaybackHistoryEntry(PlaybackHistoryEntry item)
        {
            return await AddOrUpdate<PlaybackHistoryEntry>(item) as PlaybackHistoryEntry;
        }

        public async Task<Models.Settings> GetSettingsAsync()
        {
            await InitOrUpgradeAsync();
            var result = await Connection.Table<Models.Settings>().FirstOrDefaultAsync();
            if (result == null)
                result = new Models.Settings();
            return result;
        }

        public async Task<Models.Settings> UpdateSettingsAsync(Models.Settings settings)
        {
            return await AddOrUpdate<Models.Settings>(settings) as Models.Settings;
        }

        public async Task<IEnumerable<DownloadJob>> GetDownloadJobs()
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<DownloadJob>().ToArrayAsync();
        }

        public async Task AddDownloadJob(DownloadJob job)
        {
            _ = await AddOrUpdate<DownloadJob>(job) as DownloadJob;
        }

        public async Task RemoveDownloadJob(DownloadJob job)
        {
            await Connection.DeleteAsync(job);
        }

        public async Task<bool> DownloadJobsExist()
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<DownloadJob>().FirstOrDefaultAsync() is not null;
        }

        public async Task<Services.Database.Models.TVShowCollection> AddOrUpdateTVShowCollection(Models.TVShowCollection collection)
        {
            return await AddOrUpdate<Services.Database.Models.TVShowCollection>(collection) as Services.Database.Models.TVShowCollection;
        }

        public async Task<IEnumerable<Models.TVShowCollection>> GetTVShowCollections()
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<Models.TVShowCollection>().ToArrayAsync();
        }

        public async Task<IEnumerable<Models.TVShowCollection>> GetTVShowCollectionsByName(string name)
        {
            name = name.ToLower();
            await InitOrUpgradeAsync();
            return await Connection.Table<Models.TVShowCollection>()
                                   .Where(mmi => mmi.Name.ToLower() == name)
                                   .ToArrayAsync();
        }

        public async Task<Models.TVShowCollection> GetTVShowCollection(long id)
        {
            await InitOrUpgradeAsync();
            return await Connection.Table<Models.TVShowCollection>().FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task RemoveTVShowCollection(long id)
        {
            var collection = await GetTVShowCollection(id);
            await Connection.DeleteAsync(collection);
        }

    }
}
