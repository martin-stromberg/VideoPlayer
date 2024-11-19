using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Database.Models;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Playlists;
using VideoPlayer.Tests.Helper;

namespace VideoPlayer.Tests.Services.Library
{
    [Disabled]
    public class MediaLibraryTests : BaseTest
    {
        private DateTime startDate = DateTime.MinValue;
        protected override void Init(object argument)
        {
            base.Init(argument);
            InitializeEmptyDatabase();
            InitializeClassifier(false);
            InitializeScanner(false);
            InitializeDownloadManager();
        }
        protected override async Task ExecuteAsync(object argument)
        {
            startDate = DateTime.Now;
            ExecuteGenreTests();
            await ExecutePlaylistTestsAsync();
        }

        private async Task ExecutePlaylistTestsAsync()
        {
            await ExecutePlaySingleMovieAsync();
            await ExecutePlayTVShow();
        }

        private async Task ExecutePlayTVShow()
        {
            AddTVShow();
            await ExecuteScanAndClassification(() =>
            {
                var mediaItems = MediaLibrary.GetUnclassifiedMediaItems().ToArray();
                AssertRecordCount(mediaItems, 74);
            });

            var overview = MediaLibrary.GetOverview(0, 10, "", Service.Library.Models.Classified.EntryType.TVShow)
                .ToArray();
            var show = overview.FirstOrDefault();
            var season = MediaLibrary.GetSeasons(show.Id).FirstOrDefault();
            var episodes = MediaLibrary.GetEpisodes(season.Id).ToArray();
            var episode = episodes.FirstOrDefault();
            var mediaItem = episode.MediaItemIds
                .Select(id => MediaLibrary.GetMediaItem(id))
                .Where(mi => mi.CopyType == MediaItemCopyType.Original)
                .FirstOrDefault();

            var expectedPlaylist = MediaLibrary.GetPlaylists(PlaylistType.General).Take(1).ToArray();
            MediaLibrary.ClearCaches();
            var playlist = MediaLibrary.GetPlaylists(PlaylistType.General).FirstOrDefault();
            playlist.Add(mediaItem, null);
            MediaLibrary.AddOrUpdatePlaylist(playlist);

            expectedPlaylist = new Service.Library.Models.Playlists.Playlist[]
                {
                    new Service.Library.Models.Playlists.Playlist(null)
                    {
                        Id = 1,
                        CreatedAt = DateTime.MinValue,
                        AutoDownload = false,
                        BagMode = false,
                        Name = "Meine Playlist",
                        Type = PlaylistType.General
                    }
                }.Select(pl =>
                {
                    pl.Items.Add(new PlaylistEntry(null)
                    {
                        CreatedAt = DateTime.MinValue,
                        Id = 2,
                        Item = mediaItem,
                        Name = null,
                        PlaylistId = 1
                    });
                    return pl;
                }).ToArray();

            CheckPlaylists(expectedPlaylist, null);
        }

        private async Task ExecutePlaySingleMovieAsync()
        {
            var source = AddMediaSource("PlaylistTest", true);
            var collection = AddMediaCollection(source, null, "(500) Days of Summer (2009)", false);
            var mediaItem = AddMediaItem(source, collection, "(500) Days of Summer (2009).mp4", false);
            mediaItem = AddMediaItem(source, collection, "(500) Days of Summer (2009).nfo", false);
            await ExecuteScanAndClassification(() =>
            {
                var mediaItems = MediaLibrary.GetUnclassifiedMediaItems().ToArray();
                AssertRecordCount(mediaItems, 2);
            });
            mediaItem = MediaLibrary.GetMediaItems().FirstOrDefault();
            var movie = MediaLibrary.GetMovieByMediaItem(mediaItem.Id);
            AssertTrue(movie is not null, $"Movie element not found.");

            var playlist = new VideoPlayer.Service.Library.Models.Playlists.Playlist(null)
            {
                AutoDownload = false,
                BagMode = false,
                Name = "Meine Playlist",
                Type = PlaylistType.General
            };
            MediaLibrary.AddOrUpdatePlaylist(playlist);

            var actual = MediaLibrary.GetPlaylists(PlaylistType.General)
                .Select(entry =>
                {
                    entry.CreatedAt = DateTime.MinValue;
                    foreach (var item in entry.Items)
                    {
                        item.Item.MetaInformation.LastUpdate = DateTime.MinValue;
                    }
                    return entry;
                })
                .ToArray();
            var expected = new Service.Library.Models.Playlists.Playlist[]
            {
                new Service.Library.Models.Playlists.Playlist(null)
                {
                    Id = 1,
                    CreatedAt = DateTime.MinValue,
                    AutoDownload = false,
                    BagMode = false,
                    Name = "Meine Playlist",
                    Type = PlaylistType.General
                }
            }
            .ToArray();
            AssertRecordsEqual(actual, expected);

            playlist.Add(mediaItem, movie);
            MediaLibrary.AddOrUpdatePlaylist(playlist);

            mediaItem.LastMetaInformationUpdate = DateTime.MinValue;
            mediaItem.MetaInformation.LastUpdate = DateTime.MinValue;
            var expectedPlaylists = new Service.Library.Models.Playlists.Playlist[]
                {
                    new Service.Library.Models.Playlists.Playlist(null)
                    {
                        Id = 1,
                        CreatedAt = DateTime.MinValue,
                        AutoDownload = false,
                        BagMode = false,
                        Name = "Meine Playlist",
                        Type = PlaylistType.General
                    }
                }.Select(pl =>
                {
                    pl.Items.Add(new PlaylistEntry(null)
                    {
                        CreatedAt = DateTime.MinValue,
                        Id = 1,
                        Item = mediaItem,
                        Name = null,
                        PlaylistId = 1,
                        Entry = movie
                    });
                    return pl;
                }).ToArray();
            var expectedEntries = expectedPlaylists.SelectMany(p => p.Items).ToArray();
            CheckPlaylists(expectedPlaylists, expectedEntries);

            playlist.Clear();
            MediaLibrary.AddOrUpdatePlaylist(playlist);
            expectedPlaylists[0].Items.Clear();
            expectedEntries = new PlaylistEntry[0];
            CheckPlaylists(expectedPlaylists, expectedEntries);
        }

        private void CheckPlaylists(Service.Library.Models.Playlists.Playlist[] expected, PlaylistEntry[] expectedEntries)
        {
            expected = expected
                .Select(entry =>
                {
                    entry.CreatedAt = DateTime.MinValue;
                    foreach (var item in entry.Items)
                    {
                        if (item.Item is not null)
                        {
                            if (item.Item.MetaInformation is not null)
                                item.Item.MetaInformation.LastUpdate = DateTime.MinValue;
                            item.Item.LastMetaInformationUpdate = DateTime.MinValue;
                        }
                    }
                    return entry;
                })
                .ToArray();
            foreach (var resetCache in new bool[] { false, true })
            {
                if (resetCache)
                    MediaLibrary.ClearCaches();
                var actual = MediaLibrary.GetPlaylists(PlaylistType.General)
                    .Select(entry =>
                    {
                        entry.CreatedAt = DateTime.MinValue;
                        foreach (var item in entry.Items)
                        {
                            item.CreatedAt = DateTime.MinValue;
                            if (item.Item is not null)
                            {
                                if (item.Item.MetaInformation is not null)
                                    item.Item.MetaInformation.LastUpdate = DateTime.MinValue;
                                item.Item.LastMetaInformationUpdate = DateTime.MinValue;
                            }
                        }
                        return entry;
                    })
                    .ToArray();

                AssertRecordsEqual(actual, expected);
                if (expectedEntries is not null)
                {
                    var actualEntries = actual.First().Items.ToArray();
                    AssertRecordsEqual(actualEntries, expectedEntries);
                }
            }
        }

        private void ExecuteGenreTests()
        {
            var genre = new Genre(null)
            {
                Name = "Action"
            };
            MediaLibrary.AddOrUpdateGenre(genre);

            var actualGenres = Database.GetAll<DataGenre>()
                .Select(g =>
                {
                    AssertTrue(g.CreatedAt >= startDate, $"{nameof(g.CreatedAt)} < {startDate}");
                    g.CreatedAt = DateTime.MinValue;
                    g.LastModified = DateTime.MinValue;
                    return g;
                })
                .ToArray();
            var expectedGenres = new DataGenre[] {
                new DataGenre { Id = 1, Name = "Action", CreatedAt = DateTime.MinValue, LastModified = DateTime.MinValue}
            };
            AssertRecordsEqual(actualGenres, expectedGenres);
            var actualGenreNames = Database.GetAll<DataGenreName>()
                .Select(g =>
                {
                    AssertTrue(g.CreatedAt >= startDate, $"{nameof(g.CreatedAt)} < {startDate}");
                    g.CreatedAt = DateTime.MinValue;
                    g.LastModified = DateTime.MinValue;
                    return g;
                })
                .ToArray();
            var expectedGenreNames = new DataGenreName[] { };
            AssertRecordsEqual(actualGenreNames, expectedGenreNames);

            genre = new Genre(null)
            {
                Name = "Komödie",
                AlternateNames = new GenreName[]
                {
                    new GenreName(null){ Name = "Comedy" },
                    new GenreName(null){ Name = "Witziges" },
                }
            };
            MediaLibrary.AddOrUpdateGenre(genre);
            actualGenres = Database.GetAll<DataGenre>()
                .Select(g =>
                {
                    AssertTrue(g.CreatedAt >= startDate, $"{nameof(g.CreatedAt)} < {startDate}");
                    g.CreatedAt = DateTime.MinValue;
                    g.LastModified = DateTime.MinValue;
                    return g;
                })
                .ToArray();
            expectedGenres = expectedGenres.Concat(new DataGenre[]
            {
                new DataGenre()
                {
                    Name = "Komödie",
                    Id = 2,
                    CreatedAt = DateTime.MinValue,
                    LastModified = DateTime.MinValue
                }
            }).ToArray();
            AssertRecordsEqual(actualGenres, expectedGenres);
            actualGenreNames = Database.GetAll<DataGenreName>()
                .Select(g =>
                {
                    AssertTrue(g.CreatedAt >= startDate, $"{nameof(g.CreatedAt)} < {startDate}");
                    g.CreatedAt = DateTime.MinValue;
                    g.LastModified = DateTime.MinValue;
                    return g;
                })
                .ToArray();
            expectedGenreNames = expectedGenreNames
                .Concat(new DataGenreName[]
                {
                    new DataGenreName() { Id = 1, Name = "Comedy", DataGenreId = 2},
                    new DataGenreName() { Id = 2, Name = "Witziges", DataGenreId = 2},
                })
                .ToArray();
            AssertRecordsEqual(actualGenreNames, expectedGenreNames);


            var comedy = MediaLibrary.GetGenres().Where(g => g.Name == "Komödie").ToArray();
            var expectedComedy = new Genre[] { genre };
            AssertRecordsEqual(comedy, expectedComedy);
        }
    }
}
