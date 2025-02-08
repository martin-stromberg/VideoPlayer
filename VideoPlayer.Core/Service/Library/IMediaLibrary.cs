using VideoPlayer.Service.Database;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Library.Models.Playlists;
using VideoPlayer.Service.Library.Models.Sources;
using VideoPlayer.Service.Log;

namespace VideoPlayer.Service.Library
{
    public interface IMediaLibrary
    {        
        event EventHandler<BaseServiceModelEventArgs> ItemUpdated;

        void CreateDemoData();

        IEnumerable<CacheElement> GetAllCachedObjects();

        #region MediaSource
        void Delete(MediaSource source);
        IEnumerable<MediaSource> GetSources();
        MediaSource AddOrUpdateSource(MediaSource source);

        MediaSource GetSource(long id);

        MediaSource GetNextScanSource();
        #endregion
        #region MediaCollection
        IEnumerable<MediaCollection> GetSourceMediaCollections(long sourceId);
        MediaCollection GetMediaCollectionByPath(long id, string fullPath);

        MediaCollection GetMediaCollection(long id);
        void Delete(MediaCollection collection);
        MediaCollection AddOrUpdateMediaCollection(MediaCollection collection);
        IEnumerable<MediaCollection> GetUnclassifiedMediaCollections();
        IEnumerable<MediaCollection> GetChildMediaCollections(long objectId);
        #endregion
        #region MediaItem
        MediaItem GetMediaItemByPath(long collectionId, string fullPath);
        MediaItem GetMediaItemByPath(string relPath);
        IEnumerable<MediaItem> GetMediaItems(params MediaItemCopyType[] copyType);
        IEnumerable<MediaItem> GetDueMediaItems();
        IEnumerable<MediaItem> GetMediaItemsThatNeedsPictureUpdate();
        MediaItem GetMediaItem(long id);
        IEnumerable<MediaItem> GetCopyMediaItems(long id);
        IEnumerable<MediaItem> GetMediaCollectionItems(long collectionId);

        MediaItem AddOrUpdateMediaItem(MediaItem mediaItem);

        IEnumerable<MediaItem> GetUnclassifiedMediaItems();
        void Delete(MediaItem mediaItem);        
        #endregion
        #region Movie
        Movie GetMovieByMediaItem(long mediaItemId);

        Movie GetMovie(long id);

        IEnumerable<Movie> GetMoviesByName(string name);

        IEnumerable<Movie> GetCollectionMovies(long collectionId);

        Movie AddOrUpdateMovie(Movie movie);
        void Delete(Movie movie);
        #endregion

        #region MovieCollection
        MovieCollection GetMovieCollection(long id);

        MovieCollection AddOrUpdateMovieCollection(MovieCollection movieCollection);

        MovieCollection GetMovieCollectionByMediaCollection(long mediaCollectionId);
        #endregion

        #region TVShow Episode
        TVShowEpisode GetTVShowEpisode(long id);
        TVShowEpisode GetTVShowEpisodeByMediaItem(long mediaItemId);
        TVShowEpisode GetTVShowEpisodeByIdentification(string showName, int season, int episode, string part);
        TVShowEpisode AddOrUpdateEpisode(TVShowEpisode episode);
        IEnumerable<TVShowEpisode> GetEpisodes(long seasonId);
        void Delete(TVShowEpisode episode);
        #endregion
        #region TVShowSeason
        TVShowSeason GetShowSeason(TVShow show, int seasonNo);
        TVShowSeason GetTVShowSeason(long id);
        TVShowSeason AddOrUpdateSeason(TVShowSeason season);
        IEnumerable<TVShowSeason> GetSeasons(long showId);
        #endregion
        #region TVShow
        TVShow GetTVShow(long id);
        TVShow GetShowByName(string name);
        TVShow AddOrUpdateTVShow(TVShow show);
        #endregion
        ClassifiedEntry AddOrUpdateEntry(ClassifiedEntry entry);
        ClassifiedEntry GetClassifiedEntry(long id);
        IEnumerable<ClassifiedEntry> GetOverview(int offset, int count, string genre, params EntryType[] entryTypes);
        IEnumerable<ClassifiedEntry> GetClassifiedEntriesWithPicture(string name);
        IEnumerable<string> GetClassifiedEntryPictureFileNames();
        #region Genres
        IEnumerable<Genre> GetGenres();
        Genre GetGenre(long ind);
        Genre AddOrUpdateGenre(Genre altGenre);
        #endregion
        #region Playlists 
        Playlist AddOrUpdatePlaylist(Playlist playlist);
        IEnumerable<Playlist> GetPlaylists(Models.Playlists.PlaylistType general);
        Playlist GetPlaylist(long id);
        #endregion

        
        #region Log Entry
        void AddOrUpdateLogEntry(LogEntry entry);
        void ClearLogs();
        #endregion
        #region Actors
        IEnumerable<Actor> GetActorOverview(int offset, int count);
        Actor AddOrUpdateActor(Actor entry);
        IEnumerable<Actor> GetActorsByName(string name);
        Actor GetActor(long id);
        IEnumerable<Actor> GetActorsThatNeedsPictureUpdate();
        IEnumerable<Actor> GetActorsWithPicture(string pictureFileName);
        IEnumerable<string> GetActorPictureFileNames();
        #endregion
        #region Roles
        Role AddOrUpdateRole(Role entry);
        IEnumerable<Role> GetRoles(long entryId);
        IEnumerable<Role> GetActorsRoles(long actorId);
        Role GetRole(long id);
        void Delete(Role role);
        #endregion
        void AddProtocol(ClassifiedEntry entry, string description);
        IEnumerable<ProtocolEntry> GetProtocolEntries(ClassifiedEntry entry);
        IEnumerable<ProtocolEntry> GetProtocolEntries(ClassifiedEntry entry, int offset, int count);
        void Release(BaseServiceModel entry);
        void Release(BaseServiceModel entry, bool force);
        void Release(IEnumerable<BaseServiceModel> entry);
        void Release(IEnumerable<BaseServiceModel> entry, bool force);
        void Hold(BaseServiceModel entry);
        IEnumerable<Role> GetRoles(long id, int offset, int count);
        IEnumerable<Role> GetRolesWithoutRoleCount();
    }
}
