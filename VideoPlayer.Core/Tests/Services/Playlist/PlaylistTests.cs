using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Playlists;
using VideoPlayer.Tests.Helper;

namespace VideoPlayer.Tests.Services.Playlist
{
    [Disabled]
    public class PlaylistTests : BaseTest
    {
        protected override void Init(object loopArgument)
        {
            base.Init(loopArgument);
            InitializeEmptyDatabase();
            InitializeClassifier(false);
            InitializeScanner(false);
            InitializeDownloadManager();
            InitializePlaylistManager();
            AddSingleMovie();
            AddMultiMovie();
            AddTVShow();            
        }
        

        protected override async Task ExecuteAsync(object loopArgument)
        {
            await ExecuteScanAndClassification(() =>
            {
                var mediaItems = MediaLibrary.GetUnclassifiedMediaItems().ToArray();
                AssertRecordCount(mediaItems, 111);
            });
            ExecuteSingleMovieWatch();
            ExecuteTVShowWatch();
            ExecuteMovieCollectionWatch();
            ExecuteMixedTVShowWatch();
            ExecuteTypeMixedWatches();
        }

        private void ExecuteTypeMixedWatches()
        {
            var movies = MediaLibrary.GetOverview(0, int.MaxValue, "", "", Service.Library.Models.Classified.EntryType.Movie);
            var movie = movies.FirstOrDefault();
            var shows = MediaLibrary.GetOverview(0, int.MaxValue, "", "", Service.Library.Models.Classified.EntryType.TVShow);
            var show = shows.FirstOrDefault();
            var seasons = MediaLibrary.GetSeasons(show.Id)
                .OrderBy(s => s.Number)
                .ToArray();
            var episodes = seasons
                .SelectMany(s => MediaLibrary
                    .GetEpisodes(s.Id)
                    .OrderBy(e => e.Episode)
                    .ThenBy(e => e.Part))
                .ToList();
            var movieCollections = MediaLibrary.GetOverview(0, int.MaxValue, "", "", Service.Library.Models.Classified.EntryType.MovieCollection);
            var movieCollection = movieCollections.FirstOrDefault();
            var collectionMovies = MediaLibrary.GetCollectionMovies(movieCollection.Id)
                    .OrderBy(m => m.ReleaseDate)
                    .ToArray();

            var firstEntry = movie;
            var secondEntry = episodes.First();
            var thirdEntry = collectionMovies.FirstOrDefault();
            var fourthEntry = episodes.Skip(1).First();
            var fifthEntry = thirdEntry;
            ExecuteWatch(firstEntry, 0, 1, playlist =>
            {
                AssertFalse(playlist.Items.Count == 0, $"Playlist is empty!");
                AssertFalse(playlist.Items.Count > 1, $"Playlist has more than one entry!");
                AssertObjectsEqual(playlist.Items.FirstOrDefault().Entry, firstEntry);
            });
            ExecuteWatch(secondEntry, 1, 2, playlist =>
            {
                AssertTrue(playlist.Items.Count == 2, $"Playlist does not contain 2 entries."); 
                AssertObjectsEqual(playlist.Items.FirstOrDefault().Entry, secondEntry);
                AssertObjectsEqual(playlist.Items.Skip(1).FirstOrDefault().Entry, firstEntry);
            });
            ExecuteWatch(thirdEntry, 2, 3, playlist =>
            {
                AssertTrue(playlist.Items.Count == 3, $"Playlist does not contain 3 entries.");
                AssertObjectsEqual(playlist.Items.FirstOrDefault().Entry, thirdEntry);
                AssertObjectsEqual(playlist.Items.Skip(1).FirstOrDefault().Entry, secondEntry);
                AssertObjectsEqual(playlist.Items.Skip(2).FirstOrDefault().Entry, firstEntry);
            });
            ExecuteWatch(fourthEntry, 3, 3, playlist =>
            {
                AssertTrue(playlist.Items.Count == 3, $"Playlist does not contain 3 entries.");
                AssertObjectsEqual(playlist.Items.FirstOrDefault().Entry, fourthEntry);
                AssertObjectsEqual(playlist.Items.Skip(1).FirstOrDefault().Entry, thirdEntry);
                AssertObjectsEqual(playlist.Items.Skip(2).FirstOrDefault().Entry, firstEntry);
            });
            ExecuteWatch(fifthEntry, 3, 3, playlist =>
            {
                AssertTrue(playlist.Items.Count == 3, $"Playlist does not contain 3 entries.");
                AssertObjectsEqual(playlist.Items.FirstOrDefault().Entry, thirdEntry);
                AssertObjectsEqual(playlist.Items.Skip(1).FirstOrDefault().Entry, fourthEntry);
                AssertObjectsEqual(playlist.Items.Skip(2).FirstOrDefault().Entry, firstEntry);
            });
        }

        private void ExecuteWatch(ClassifiedEntry entry,
            int expectedPlaylistEntryCountBefore,
            int expectedPlaylistEntryCountAfter,
            Action<Service.Library.Models.Playlists.Playlist> callback)
        {
            var playlist = MediaLibrary.GetPlaylists(Service.Library.Models.Playlists.PlaylistType.NextPlayback).FirstOrDefault();
            var currentMediaItem = GetPlayableMediaItem(entry);
            ExecuteMediaItemPlayback(currentMediaItem, TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(5),
                       (time, duration) =>
                       {
                           if (playlist is null)
                               playlist = MediaLibrary.GetPlaylists(Service.Library.Models.Playlists.PlaylistType.NextPlayback).FirstOrDefault();
                           if (time <= TimeSpan.FromSeconds(5) && expectedPlaylistEntryCountBefore == 0)
                               AssertTrue(playlist is null || playlist.Items.Count == 0, $"Playlist is not empty!");
                           else if (time <= TimeSpan.FromSeconds(5) && expectedPlaylistEntryCountBefore != 0)
                           {
                               AssertFalse(playlist.Items.Count < expectedPlaylistEntryCountBefore, $"Playlist has less than {expectedPlaylistEntryCountBefore} entries!");
                               AssertFalse(playlist.Items.Count > expectedPlaylistEntryCountBefore, $"Playlist has more than {expectedPlaylistEntryCountBefore} entry!");
                           }
                           else if (time.Add(TimeSpan.FromSeconds(30)) <= duration)
                           {
                               AssertFalse(playlist.Items.Count < expectedPlaylistEntryCountAfter, $"Playlist has less than {expectedPlaylistEntryCountAfter} entries!");
                               AssertFalse(playlist.Items.Count > expectedPlaylistEntryCountAfter, $"Playlist has more than {expectedPlaylistEntryCountAfter} entry!");
                           }
                       });
            callback(playlist);
        }

        private void ExecuteMixedTVShowWatch()
        {
            var shows = MediaLibrary.GetOverview(0, int.MaxValue, "", "", Service.Library.Models.Classified.EntryType.TVShow);
            var show = shows.FirstOrDefault();
            var seasons = MediaLibrary.GetSeasons(show.Id)
                .OrderBy(s => s.Number)
                .ToArray();
            var episodes = seasons
                .SelectMany(s => MediaLibrary
                    .GetEpisodes(s.Id)
                    .OrderBy(e => e.Episode)
                    .ThenBy(e => e.Part))
                .ToList();

            var firstEntry = episodes.Skip(1).FirstOrDefault();
            var secondEntry = episodes.Skip(4).FirstOrDefault();
            var thirdEntry = episodes.FirstOrDefault();            
            ExecuteWatch(firstEntry, 0, 1, (playlist) =>
            {
                AssertFalse(playlist.Items.Count == 0, $"Playlist is empty!");
                AssertFalse(playlist.Items.Count > 1, $"Playlist has more than one entry!");
                AssertObjectsEqual(playlist.Items.FirstOrDefault().Entry, firstEntry);
            });
            ExecuteWatch(secondEntry, 1, 1, (playlist) =>
            {
                AssertFalse(playlist.Items.Count == 0, $"Playlist is empty!");
                AssertFalse(playlist.Items.Count > 1, $"Playlist has more than one entry!");
                AssertObjectsEqual(playlist.Items.FirstOrDefault().Entry, secondEntry);
            });
            ExecuteWatch(thirdEntry, 1, 1, (playlist) =>
            {
                AssertFalse(playlist.Items.Count == 0, $"Playlist is empty!");
                AssertFalse(playlist.Items.Count > 1, $"Playlist has more than one entry!");
                AssertObjectsEqual(playlist.Items.FirstOrDefault().Entry, thirdEntry);
            });
            ExecuteTVShowWatch(false);
        }
        

        private void ExecuteMovieCollectionWatch()
        {
            var movieCollections = MediaLibrary.GetOverview(0, int.MaxValue, "", "", Service.Library.Models.Classified.EntryType.MovieCollection);
            var movieCollection = movieCollections.FirstOrDefault();
            ExecuteMovieCollectionPlayback(movieCollection);
        }

        private void ExecuteMovieCollectionPlayback(ClassifiedEntry movieCollection)
        {
            var movies = MediaLibrary
                .GetCollectionMovies(movieCollection.Id)
                .OrderBy(m => m.ReleaseDate)
                .ToList();
            var firstMovie = movies.FirstOrDefault();
            var lastMovie = movies.LastOrDefault();
            var firstMediaItem = GetPlayableMediaItem(firstMovie);
            var lastMediaItem = GetPlayableMediaItem(lastMovie);

            var currentMediaItem = firstMediaItem;

            var playlist = MediaLibrary.GetPlaylists(Service.Library.Models.Playlists.PlaylistType.NextPlayback).FirstOrDefault();
            AssertTrue(currentMediaItem is not null, $"No first episode found.");
            int loopCounter = 0;
            var isFirst = true;
            while (currentMediaItem is not null)
                try
                {
                    loopCounter += 1;
                    ExecuteMediaItemPlayback(currentMediaItem, TimeSpan.FromMinutes(20),TimeSpan.MaxValue,
                       (time, duration) =>
                       {
                           if (playlist is null)
                               playlist = MediaLibrary.GetPlaylists(Service.Library.Models.Playlists.PlaylistType.NextPlayback).FirstOrDefault();
                           if (isFirst && time <= TimeSpan.FromSeconds(5))
                               AssertTrue(playlist is null || playlist.Items.Count == 0, $"Playlist is not empty!");
                           else if (time.Add(TimeSpan.FromSeconds(30)) <= duration)
                           {
                               AssertFalse(playlist.Items.Count == 0, $"Playlist is empty!");
                               AssertFalse(playlist.Items.Count > 1, $"Playlist has more than one entry!");
                           }
                       });
                    isFirst = false;
                    movies.RemoveAt(0);
                    var nextExpectedEpisode = movies.FirstOrDefault();
                    var playlistEntry = playlist.Items.FirstOrDefault();
                    if (playlistEntry is null)
                    {
                        AssertTrue(nextExpectedEpisode is null, $"Playback ended with expected next episode.");
                        AssertTrue(currentMediaItem.Id == lastMediaItem.Id, $"Playback does not end with last media item.");
                        currentMediaItem = null;
                    }
                    else
                    {
                        currentMediaItem = playlistEntry.Item;
                        var nextEpisode = MediaLibrary.GetMovieByMediaItem(currentMediaItem.Id);
                        AssertObjectsEqual(nextEpisode, nextExpectedEpisode);
                    }
                }
                catch (Exception ex)
                {
                    throw new ApplicationException($"Error in episode loop {loopCounter}: {ex.Message}", ex);
                }
            AssertTrue(playlist.Items.Count == 0, $"Playlist is not empty!");
        }

        private void ExecuteTVShowWatch(bool expectEmptyList = true)
        {
            var shows = MediaLibrary.GetOverview(0, int.MaxValue, "", "", Service.Library.Models.Classified.EntryType.TVShow);
            var show = shows.FirstOrDefault();            
            ExecuteShowPlayback(show, expectEmptyList);
        }

        private void ExecuteShowPlayback(ClassifiedEntry show, bool expectEmptyList = true)
        {
            var seasons = MediaLibrary.GetSeasons(show.Id)
                .OrderBy(s => s.Number)
                .ToArray();
            var episodes = seasons
                .SelectMany(s => MediaLibrary
                    .GetEpisodes(s.Id)
                    .OrderBy(e => e.Episode)
                    .ThenBy(e => e.Part))
                .ToList();
            var firstEpisode = episodes.FirstOrDefault();
            var lastEpisode = episodes.LastOrDefault();
            var firstMediaItem = GetPlayableMediaItem(firstEpisode);
            var lastMediaItem = GetPlayableMediaItem(lastEpisode);
            var currentMediaItem = firstMediaItem;

            var playlist = MediaLibrary.GetPlaylists(Service.Library.Models.Playlists.PlaylistType.NextPlayback).FirstOrDefault();
            AssertTrue(currentMediaItem is not null, $"No first episode found.");
            var isFirst = expectEmptyList;
            var loopCounter = 0;
            while (currentMediaItem is not null)
                try
                {
                    loopCounter += 1;
                    ExecuteMediaItemPlayback(currentMediaItem, TimeSpan.FromMinutes(20), TimeSpan.MaxValue,
                       (time, duration) =>
                       {
                           if (playlist is null)
                               playlist = MediaLibrary.GetPlaylists(Service.Library.Models.Playlists.PlaylistType.NextPlayback).FirstOrDefault();
                           if (isFirst && time <= TimeSpan.FromSeconds(5))
                               AssertTrue(playlist is null || playlist.Items.Count == 0, $"Playlist is not empty!");
                           else if (time.Add(TimeSpan.FromSeconds(30)) <= duration)
                           {
                               AssertFalse(playlist.Items.Count == 0, $"Playlist is empty!");
                               AssertFalse(playlist.Items.Count > 1, $"Playlist has more than one entry!");
                           }
                       });
                    isFirst = false;
                    episodes.RemoveAt(0);
                    var nextExpectedEpisode = episodes.FirstOrDefault();
                    var playlistEntry = playlist.Items.FirstOrDefault();
                    if (playlistEntry is null)
                    {
                        AssertTrue(nextExpectedEpisode is null, $"Playback ended with expected next episode.");
                        AssertTrue(currentMediaItem.Id == lastMediaItem.Id, $"Playback does not end with last media item.");
                        currentMediaItem = null;
                    }
                    else
                    {
                        currentMediaItem = playlistEntry.Item;
                        var nextEpisode = MediaLibrary.GetTVShowEpisodeByMediaItem(currentMediaItem.Id);
                        AssertObjectsEqual(nextEpisode, nextExpectedEpisode);
                    }
                }
                catch (Exception ex)
                {
                    throw new ApplicationException($"Error in episode loop {loopCounter}: {ex.Message}", ex);
                }
            AssertTrue(playlist.Items.Count == 0, $"Playlist is not empty!");
        }

        private void ExecuteSingleMovieWatch()
        {
            var movies = MediaLibrary.GetOverview(0, int.MaxValue, "", "", Service.Library.Models.Classified.EntryType.Movie);
            var movie = movies.FirstOrDefault();
            var mediaItem = GetPlayableMediaItem(movie);
            var playlist = MediaLibrary.GetPlaylists(Service.Library.Models.Playlists.PlaylistType.NextPlayback).FirstOrDefault();

            ExecuteMediaItemPlayback(mediaItem, TimeSpan.FromMinutes(90), TimeSpan.MaxValue,
                (time, duration) =>
                {
                    if (playlist is null)
                        playlist = MediaLibrary.GetPlaylists(Service.Library.Models.Playlists.PlaylistType.NextPlayback).FirstOrDefault();
                    if (time <= TimeSpan.FromSeconds(5))
                        AssertTrue(playlist is null || playlist.Items.Count == 0, $"Playlist is not empty!");
                    else if (time.Add(TimeSpan.FromSeconds(30)) <= duration)
                    {
                        AssertFalse(playlist.Items.Count == 0, $"Playlist is empty!");
                        AssertFalse(playlist.Items.Count > 1, $"Playlist has more than one entry!");
                    }
                    else
                        AssertTrue(playlist.Items.Count == 0, $"Playlist is not empty!");
                });
            AssertTrue(playlist.Items.Count == 0, $"Playlist is not empty!");
        }

        private void ExecuteMediaItemPlayback(
            MediaItem mediaItem, 
            TimeSpan duration,
            TimeSpan stopAt,
            Action<TimeSpan, TimeSpan> callback)
        {
            TimeSpan currentTime = TimeSpan.Zero;
            TimeSpan interval = TimeSpan.FromSeconds(1);
            while (currentTime <= duration && currentTime <= stopAt)
            {
                PlaylistManager.ProcessVideoPosition(mediaItem, currentTime, duration);
                callback(currentTime, duration);
                currentTime += interval;
            }
        }
    }
}
