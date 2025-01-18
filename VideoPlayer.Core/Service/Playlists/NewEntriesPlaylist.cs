using Microsoft.Extensions.Logging;
using VideoPlayer.Service.Download;
using VideoPlayer.Service.Events;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Library.Models.Playlists;

namespace VideoPlayer.Service.Playlists
{
    public class NewEntriesPlaylist : BasePlaylistService
    {
        private readonly IMediaLibrary mediaLibrary;

        public NewEntriesPlaylist(
            IMediaLibrary mediaLibrary, 
            IMediaCollectionSelector mediaCollectionSelector,
            IDownloadManager downloadManager,
            ILogger logger) 
            : base(mediaLibrary, mediaCollectionSelector, downloadManager, PlaylistType.New, logger)
        {
            base.CorrectInvisibleMediaItems = true;
            this.mediaLibrary = mediaLibrary;
        }
        protected override void ProcessNotification(NotificationEventArgs e)
        {
            base.ProcessNotification(e);
            switch (e.Name)
            {
                case "EntryClassified-New":
                    ProcessUpdatedEntry(e.Data as TVShowEpisode);
                    ProcessUpdatedEntry(e.Data as TVShowSeason);
                    ProcessUpdatedEntry(e.Data as TVShow);
                    ProcessUpdatedEntry(e.Data as MovieCollection);
                    ProcessUpdatedEntry(e.Data as Movie);
                    break;
            }
        }
        protected override Playlist InitCurrentPlaylist()
        {
            var playlist = base.InitCurrentPlaylist();
            return playlist;
        }
        protected override void SaveChanges()
        {
            while (Current.Items.Count > 10)
                Current.Items.RemoveAt(Current.Items.Count - 1);
            base.SaveChanges();
        }
        private void Add(PlaylistEntry entry)
        {
            Current.Items.Insert(0, entry);
            SaveChanges();
        }

        private PlaylistEntry FindEntry(TVShow show)
        {
            return Current.Items
                .FirstOrDefault(entry =>
                {
                    var entryEpisode = entry.Entry as TVShowEpisode;
                    var entrySeason = entry.Entry as TVShowSeason ?? (entryEpisode is null ? null : mediaLibrary.GetTVShowSeason(entryEpisode.SeasonId));
                    var entryShow = entry.Entry as TVShow ?? (entrySeason is null ? null : mediaLibrary.GetTVShow(entrySeason.ShowId));
                    if (entryShow is null)
                        return false;
                    return entryShow.Id == show.Id;
                });
        }
        private PlaylistEntry FindEntry(MovieCollection collection)
        {
            return Current.Items
                .FirstOrDefault(entry =>
                {
                    var entryMovie = entry.Entry as Movie;
                    var entryCollection = entry.Entry as MovieCollection ?? (entryMovie is null ? null : MediaLibrary.GetMovieCollection(entryMovie.CollectionId));
                    if (entryCollection is null) return false;
                    return entryCollection.Id == collection.Id;
                });
        }
        private PlaylistEntry FindEntry(Movie movie)
        {
            if (movie.CollectionId != 0)
            {
                var collection = MediaLibrary.GetMovieCollection(movie.CollectionId);
                return FindEntry(collection);
            }
            return Current.Items
                .FirstOrDefault(entry =>
                {
                    var entryMovie = entry.Entry as Movie;
                    if (entryMovie is null) return false;
                    return entryMovie.Id == movie.Id;
                });
        }

        private void ProcessUpdatedEntry(Movie movie)
        {
            if (movie is null) return;
            var existing = FindEntry(movie);
            if (existing is null)
                Add(new PlaylistEntry(null)
                {
                    Entry = movie
                });
        }

        private void ProcessUpdatedEntry(MovieCollection collection)
        {
            if (collection is null) return;
            if (!collection.Visible) return;
            var existing = FindEntry(collection);
            if (existing is null)
                Add(new PlaylistEntry(null)
                {
                    Entry = collection
                });
        }

        private void ProcessUpdatedEntry(TVShow show)
        {
            if (show is null) return;
            var existing = FindEntry(show);
            if (existing is null)
                Add(new PlaylistEntry(null)
                {
                    Entry = show
                });
        }

        private void ProcessUpdatedEntry(TVShowSeason season)
        {
            if (season is null) return;
            var show = MediaLibrary.GetTVShow(season.ShowId);
            var existing = FindEntry(show);
            if (existing is null)
                Add(new PlaylistEntry(null)
                {
                    Entry = season
                });
        }

        private void ProcessUpdatedEntry(TVShowEpisode episode)
        {
            if (episode is null) return;
            var season = mediaLibrary.GetTVShowSeason(episode.SeasonId);
            var show = MediaLibrary.GetTVShow(season.ShowId);
            var existing = FindEntry(show);
            if (existing is null)
                Add(new PlaylistEntry(null)
                {
                    Entry = episode
                });
        }
    }
}
