using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Processor;
using VideoPlayer.Service.Settings;

namespace VideoPlayer.Service.Download
{
    public class DownloadChecker : BaseDownloadService
    {
        private ConcurrentQueue<DownloadSession> queue = new ConcurrentQueue<DownloadSession>();
        
        
        public DownloadChecker(IEnvironment environment, IApplicationSettings applicationSettings, IMediaLibrary mediaLibrary, IMediaCollectionSelector mediaCollectionSelector, IProcessorCollection processorCollection, ILogger logger) 
            : base(environment, applicationSettings, mediaLibrary, mediaCollectionSelector, processorCollection, logger)
        {
        }

        private DownloadSession Add(ClassifiedEntry entry, MediaItemCopyType copyType, TimeSpan dueTime)
        {
            var session = new DownloadSession()
            {
                Entry = entry,
                Item = null,
                CopyType = copyType,
                DueTime = dueTime
            };
            Add(session);
            return session;
        }
        public void Add(DownloadSession session)
        {
            queue.Enqueue(session);
        }
        public bool HasJobs
        {
            get => !queue.IsEmpty;
        }
        public DownloadSession FindExistingSession(DownloadSession session)
        {
            var existing = queue.FirstOrDefault(s => s.SessionId != session.SessionId
                && s.Item is not null
                && session.Item is not null
                && s.Item.Id == session.Item.Id);
            if (existing is null)
                existing = queue.FirstOrDefault(s => s.SessionId != session.SessionId
                    && s.Entry is not null
                    && session.Entry is not null
                    && s.Entry.Id == session.Entry.Id);
            return existing;
        }
        public DownloadSession FindExistingDownloadSession(DownloadSession session)
        {
            DownloadSessionEventArgs downloadSession = new DownloadSessionEventArgs(session);
            CheckSessionExists(this, downloadSession);
            return downloadSession.Session;

            
        }
        public event EventHandler<DownloadSessionEventArgs> CheckSessionExists;
        public bool IsInQueue(MediaItem mediaItem)
        {
            return queue.Any(e => e.Item?.Id == mediaItem.Id);
        }
        private bool IsSessionAlreadyQueued(DownloadSession session)
        {
            var existing = FindExistingDownloadSession(session);
            return existing is not null;
        }

        protected override void Execute()
        {
            while (queue.TryPeek(out var firstEntry))
            {
                if (firstEntry.Waiting)
                {
                    if (!queue.TryDequeue(out var secondFirstEntry))
                        break;
                    queue.Enqueue(secondFirstEntry);
                    break;
                }
                Check(firstEntry);
                if (!queue.TryDequeue(out var secondEntry))
                    break;
                if (secondEntry.Entry.Id != firstEntry.Entry.Id)
                    queue.Enqueue(secondEntry);
            }
        }
        private void Check(DownloadSession session)
        {
            try
            {
                session.Start();
                CompleteSession(session);
                if (SplitSession(session))
                    return;
                if (session.Item is null)
                    throw new ApplicationException($"No media item found to download.");

                if (session.Item.CopyType == session.CopyType)
                {
                    SetDue(session.Item, session.CopyType, session.DueTime);
                    mediaLibrary.AddProtocol(session.Entry, $"Update Existing Download - (MediaItem {session.Item.Id} - {session.Item.CopyType} - {session.Item.DueDate})");
                    session.Finish();
                }
                else if (session.Item.CopyType == MediaItemCopyType.Download)
                {
                    SetDue(session.Item, session.CopyType, session.DueTime);
                    mediaLibrary.AddProtocol(session.Entry, $"Update Existing Download - (MediaItem {session.Item.Id} - {session.Item.CopyType} - {session.Item.DueDate})");
                    session.Finish();
                }
                else if (session.Item.CopyType != MediaItemCopyType.Original)
                {
                    var oldPath = Path.Combine(environment.GetPath(session.Item.CopyType), $"{session.Item.Path}");
                    var newPath = Path.Combine(environment.GetPath(session.CopyType), $"{Guid.NewGuid}{Path.GetExtension(session.Item.Name)}");
                    session.Item.CopyType = session.CopyType;
                    if (session.Item.Path != newPath)
                    {
                        File.Move(session.Item.Path, newPath);
                        session.Item.Path = newPath;
                    }
                    SetDue(session.Item, session.CopyType, session.DueTime);
                    mediaLibrary.AddProtocol(session.Entry, $"Update Existing Download - (MediaItem {session.Item.Id} - {session.Item.CopyType} - {session.Item.DueDate})");
                    mediaLibrary.AddOrUpdateMediaItem(session.Item);
                    session.Finish();
                }
                else
                {
                    var existingSession = FindExistingSession(session);
                    if (existingSession is not null)
                    {
                        if (existingSession.DueTime != TimeSpan.Zero)
                            session.DueTime = existingSession.DueTime;
                    }
                    existingSession = FindExistingDownloadSession(session);
                    if (existingSession is not null)
                    {
                        if (existingSession.DueTime != TimeSpan.Zero)
                            session.DueTime = existingSession.DueTime;
                        session.Assign(existingSession);
                        queue.Enqueue(session);
                    }
                    else if (!IsSessionAlreadyQueued(session))
                        OnDownload(session);                        
                }
            }
            catch (Exception ex)
            {
                session.Fail(ex);
            }
        }
        private void OnDownload(DownloadSession session)
        {
            Download.Invoke(this, new DownloadSessionEventArgs(session));
        }
        public event EventHandler<DownloadSessionEventArgs> Download;
        private void CompleteSession(DownloadSession session)
        {
            if (session.Entry is null)
            {
                session.Entry = FindEntry(session.Item);
                mediaLibrary.Hold(session.Entry);
            }
            if (session.Item is null)
            {
                session.Item = FindItem(session.Entry);
                mediaLibrary.Hold(session.Item);
            }
            if (session.Item is null)
                return;
            if (session.Entry is not null && session.Item.CopyType == MediaItemCopyType.Original)
            {
                var existingDownloadItem = FindItem(session.Entry);
                if (existingDownloadItem is not null)
                {
                    mediaLibrary.Release(session.Item);
                    session.Item = existingDownloadItem;
                }
            }
            if (session.Item is null)
                return;
        }        
        #region Split Session
        private bool SplitSession(DownloadSession session)
        {
            if (session.Entry is null) return false;
            return SplitSession(session.Entry as TVShow, session.CopyType, session.DueTime)
                || SplitSession(session.Entry as TVShowSeason, session.CopyType, session.DueTime)
                || SplitSession(session.Entry as MovieCollection, session.CopyType, session.DueTime);
        }
        private bool SplitSession(MovieCollection movieCollection, MediaItemCopyType copyType, TimeSpan dueTime)
        {
            if (movieCollection is null) return false;
            foreach (var movie in mediaCollectionSelector.FindNextEntries(movieCollection))
                Add(movie, copyType, dueTime);
            return true;
        }
        private bool SplitSession(TVShowSeason tVShowSeason, MediaItemCopyType copyType, TimeSpan dueTime)
        {
            if (tVShowSeason is null) return false;
            var isFirst = true;
            foreach (var episode in mediaCollectionSelector.FindNextEntries(tVShowSeason)
                .OfType<TVShowEpisode>()
                .TakeWhile(episode => {
                    if (episode.SeasonId != tVShowSeason.Id)
                        return false;
                    if (isFirst || copyType == MediaItemCopyType.Download)
                    {
                        isFirst = false;
                        return true;
                    }
                    return false;
                }))
                Add(episode, copyType, dueTime);
            return true;
        }
        private bool SplitSession(TVShow tVShow, MediaItemCopyType copyType, TimeSpan dueTime)
        {
            if (tVShow is null) return false;
            var season = mediaCollectionSelector.FindFirstSeason(tVShow);
            return SplitSession(season, copyType, dueTime);
        }
        #endregion

    }
}
