using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.BaseServices;
using VideoPlayer.Service.Download;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Library.Models.Playlists;

namespace VideoPlayer.Service.Playlists
{
    public class GeneralPlaylist: BasePlaylistService
    {
        public GeneralPlaylist(
            IMediaLibrary mediaLibrary,
            IDownloadManager downloadManager,
            IMediaCollectionSelector mediaCollectionSelector,
            ILogger logger)
            :base(mediaLibrary, mediaCollectionSelector, PlaylistType.General, logger)
        {
            this.downloadManager = downloadManager;
        }

        private readonly IDownloadManager downloadManager;

        protected override void ExecutePlaybackRequest(PlaylistEntry e)
        {
            PlaybackRequest?.Invoke(this, e);
        }
        protected override void ExecuteDownloadRequest(DownloadEventArgs e)
        {
            var entry = e.ModelObject as PlaylistEntry;
            e.Session = downloadManager.Enqueue(entry.Entry, entry.Item);
            e.Session.Starting += Session_Starting;
            e.Session.Progress += Session_Progress;
            e.Session.Finished += Session_Finished;
        }

        private void Session_Finished(object sender, DownloadEventArgs e)
        {
            (sender as DownloadManager.DownloadSession).Progress -= Session_Progress;
            (sender as DownloadManager.DownloadSession).Finished -= Session_Finished;
        }

        private void Session_Progress(object sender, ProgressEventArgs e)
        {
            var session = (sender as DownloadManager.DownloadSession);
            DownloadProgress?.Invoke(this, new DownloadProgressEventArgs(session.Entry, e.Progress));
        }

        private void Session_Starting(object sender, DownloadEventArgs e)
        {
            (sender as DownloadManager.DownloadSession).Starting -= Session_Starting;
            DownloadStarting?.Invoke(this, e);
        }

        protected override void ExecuteDownloadFailed(DownloadEventArgs e)
        {
            base.ExecuteDownloadFailed(e);
            PlaybackRequest?.Invoke(this, null);
        }

        public event EventHandler<PlaylistEntry> PlaybackRequest;
        public event EventHandler<DownloadEventArgs> DownloadStarting;
        public event EventHandler<DownloadProgressEventArgs> DownloadProgress;

        public void Start(ClassifiedEntry entry)
        {
            Current.Clear();
            Add(entry as Movie);
            Add(entry as TVShow);
            Add(entry as TVShowSeason);
            Add(entry as TVShowEpisode, true);
        }

        private void Add(TVShow show)
        {
            foreach (var episode in MediaCollectionSelector.FindNextEntries(show).Cast<TVShowEpisode>())
                Add(episode);
        }
        private void Add(TVShowSeason show)
        {
            foreach (var episode in MediaCollectionSelector.FindNextEntries(show).Cast<TVShowEpisode>())
                Add(episode);
        }

        private void Add(TVShowEpisode episode, bool addFollowing = false)
        {
            if (episode is not null)
            {
                Current.Add(new PlaylistEntry(null)
                {
                    Entry = episode
                });
                if (addFollowing)
                {
                    foreach (var nextEpisode in MediaCollectionSelector.FindNextEntries(episode)
                        .Cast<TVShowEpisode>()
                        .SkipWhile(e => e.Id == episode.Id))
                        Add(nextEpisode);
                }
            }
        }

        private void Add(Movie movie)
        {
            if (movie is not null)
                Current.Add(new PlaylistEntry(null)
                {
                    Entry = movie
                });
        }

        internal void Continue(MediaItem currentMediaItem)
        {
            var firstEntry = Current.Items.FirstOrDefault();
            if (firstEntry is null) return;
            if (firstEntry.MediaItemId != currentMediaItem.Id) return;
            Current.Remove(firstEntry);
        }
    }
}
