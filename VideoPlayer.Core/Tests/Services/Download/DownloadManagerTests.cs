using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Library.Models.MediaInformation;
using VideoPlayer.Tests.Helper;

namespace VideoPlayer.Tests.Services.Download
{
    [Disabled]
    public class DownloadManagerTests : BaseTest
    {
        protected override object[] LoopArguments => new object[] { false, true };
        protected override void Init(object argument)
        {
            base.Init(argument);
            InitializeEmptyDatabase();
            InitializeClassifier(false);
            InitializeScanner(false);
            InitializeDownloadManager();

            var source = AddMediaSource("Filme", true);
            var collection = AddMediaCollection(source, null, "(500) Days of Summer (2009)", false);
            var mediaItem = AddMediaItem(source, collection, "(500) Days of Summer (2009).mp4", false);
            mediaItem = AddMediaItem(source, collection, "(500) Days of Summer (2009).nfo", false);
        }
        protected override async Task ExecuteAsync(object argument)
        {
            var resetCache = (bool)argument;

            //Filme erfassen und klassifizieren:
            await ExecuteScanAndClassification(() =>
            {
                var mediaItems = MediaLibrary.GetUnclassifiedMediaItems().ToArray();
                AssertRecordCount(mediaItems, 2);
            });
            var movie = MediaLibrary.GetMovies().FirstOrDefault();
            var mediaItem = MediaLibrary
                .GetMediaItems()
                .Where(mi => movie.MediaItemIds.Contains(mi.Id))
                .FirstOrDefault();
            var actualOverviewEntries = MediaLibrary.GetOverview(0, 10, "", "", EntryType.Movie).ToArray();
            AssertRecordCount(actualOverviewEntries, 1);

            if (resetCache)
                MediaLibrary.ClearCaches();

            //Film laden
            await ExecuteDownloads(movie);
            var actual = MediaLibrary.GetMediaItems()
                .Select(mi =>
                {
                    mi = mi.Clone() as MediaItem;
                    if (mi.CopyType == MediaItemCopyType.Cache)
                    {
                        AssertTrue(mi.DueDate != DateTime.MinValue, $"No due date is set for downloaded media item.");
                        mi.DueDate = DateTime.MinValue;
                    }
                    mi.CreatedAt = DateTime.MinValue;
                    mi.LastAccess = DateTime.MinValue;
                    mi.LastClassificationTry = DateTime.MinValue;
                    mi.LastMetaInformationUpdate = DateTime.MinValue;
                    if (mi.MetaInformation is not null)
                    {
                        mi.MetaInformation.LastUpdate = DateTime.MinValue;
                        var movI = mi.MetaInformation as MovieInformation;
                        movI.Actors = Enumerable.Range(1, movI.Actors.Length).Select(i => new ActorInformation()).ToArray();
                    }
                    if (mi.Path.StartsWith("/Cache/"))
                        mi.Path = $"/Cache/{mi.Name}";
                    return mi;
                })
                .ToArray();
            var expected = new MediaItem[]
            {
                new MediaItem()
                {
                    Id = 1,
                    CreatedAt = DateTime.MinValue,
                    Name = "(500) Days of Summer (2009).mp4",
                    Classified = true,
                    CopyType = MediaItemCopyType.Original,
                    LastAccess = DateTime.MinValue,
                    LastClassificationTry = DateTime.MinValue,
                    LastMetaInformationUpdate = DateTime.MinValue,
                    MetaInformation = new Service.Library.Models.MediaInformation.MovieInformation()
                    {
                        Genres = new string[]{ "Comedy", "Drama", "Romance" },
                        Plot = "Tom ist in Summer verliebt. Seit sie die Schwelle zur Grußkartenfirma, in der er arbeitet, überschritten hat, ist sich der Möchtegern-Architekt sicher: Summer ist die Frau, mit der er sein restliches Leben verbringen möchte. Doch Toms Traumfrau glaubt weder an die Liebe, noch will sie eine Beziehung führen. Abgesehen von diesem fundamentalen Widerspruch haben Summer und Tom jedoch so viele Gemeinsamkeiten, dass sich aus der Bürobekanntschaft bald eine Freundschaft mit Extras entwickelt. Als Summer die Quasi-Beziehung nach 500 Tagen himmlischer Höhen und traumatischer Tiefen schließlich beendet, ruft sich Tom die prägendsten Momente ihres Zusammenseins immer wieder vor Augen, um herauszufinden, warum sein Happily Everafter ein solch jähes Ende fand…",
                        ReleaseDate = new DateTime(2009, 10, 22),
                        PremieredAt = new DateTime(2009, 10, 22),
                        Year = 2009,
                        Language = null,
                        OriginalTitle = "(500) Days of Summer",
                        Title = "(500) Days of Summer",
                        LastUpdate = DateTime.MinValue,
                        Actors = Enumerable.Range(1, 97).Select(i => new ActorInformation(){ }).ToArray()
                    },
                    OriginalMediaItemId = 0,
                    ParentCollectionId = 2,
                    Path = "/(500) Days of Summer (2009)/(500) Days of Summer (2009).mp4"
                },
                new MediaItem()
                {
                    Id = 2,
                    CreatedAt = DateTime.MinValue,
                    Name = "(500) Days of Summer (2009).nfo",
                    Classified = true,
                    CopyType = MediaItemCopyType.Original,
                    LastAccess = DateTime.MinValue,
                    LastClassificationTry = DateTime.MinValue,
                    LastMetaInformationUpdate = DateTime.MinValue,
                    MetaInformation = null,
                    OriginalMediaItemId = 0,
                    ParentCollectionId = 2,
                    Path = "/(500) Days of Summer (2009)/(500) Days of Summer (2009).nfo"
                },
                new MediaItem()
                {
                    Id = 3,
                    CreatedAt = DateTime.MinValue,
                    Name = "(500) Days of Summer (2009).mp4",
                    Classified = false,
                    CopyType = MediaItemCopyType.Cache,
                    LastAccess = DateTime.MinValue,
                    LastClassificationTry = DateTime.MinValue,
                    LastMetaInformationUpdate = DateTime.MinValue,
                    LastPictureUpdateTry = DateTime.MinValue,
                    LastPosition = TimeSpan.Zero,
                    DueDate = DateTime.MinValue,
                    NeedsPictureUpdate = false,
                    MetaInformation = null,
                    OriginalMediaItemId = 1,
                    ParentCollectionId = 2,
                    Path = "/Cache/(500) Days of Summer (2009).mp4"
                }
            };
            AssertRecordsEqual(actual, expected);
            actualOverviewEntries = MediaLibrary.GetOverview(0, 10, "", "", EntryType.Movie).ToArray();
            AssertRecordCount(actualOverviewEntries, 1);

            if (resetCache)
                MediaLibrary.ClearCaches();

            //Film erneut laden
            await ExecuteDownloads(movie);
            actual = MediaLibrary.GetMediaItems()
                .Select(mi =>
                {
                    mi = mi.Clone() as MediaItem;
                    if (mi.CopyType == MediaItemCopyType.Cache)
                    {
                        AssertTrue(mi.DueDate != DateTime.MinValue, $"No due date is set for downloaded media item.");
                        mi.DueDate = DateTime.MinValue;
                    }
                    mi.CreatedAt = DateTime.MinValue;
                    mi.LastAccess = DateTime.MinValue;
                    mi.LastClassificationTry = DateTime.MinValue;
                    mi.LastMetaInformationUpdate = DateTime.MinValue;
                    if (mi.MetaInformation is not null)
                    {
                        mi.MetaInformation.LastUpdate = DateTime.MinValue;
                        var movI = mi.MetaInformation as MovieInformation;
                        movI.Actors = Enumerable.Range(1, movI.Actors.Length).Select(i => new ActorInformation()).ToArray();
                    }
                    if (mi.Path.StartsWith("/Cache/"))
                        mi.Path = $"/Cache/{mi.Name}";
                    return mi;
                })
                .ToArray();
            AssertRecordsEqual(actual, expected);
            actualOverviewEntries = MediaLibrary.GetOverview(0, 10, "", "", EntryType.Movie).ToArray();
            AssertRecordCount(actualOverviewEntries, 1);

            if (resetCache)
                MediaLibrary.ClearCaches();

            //Download Klassifizieren
            await ExecuteClassification();
            expected[2].Classified = true;
            expected[2].NeedsPictureUpdate = true;
            actual = MediaLibrary.GetMediaItems()
                .Select(mi =>
                {
                    mi = mi.Clone() as MediaItem;
                    if (mi.CopyType == MediaItemCopyType.Cache)
                    {
                        AssertTrue(mi.DueDate != DateTime.MinValue, $"No due date is set for downloaded media item.");
                        mi.DueDate = DateTime.MinValue;
                    }
                    mi.CreatedAt = DateTime.MinValue;
                    mi.LastAccess = DateTime.MinValue;
                    mi.LastClassificationTry = DateTime.MinValue;
                    mi.LastMetaInformationUpdate = DateTime.MinValue;
                    if (mi.MetaInformation is not null)
                    {
                        mi.MetaInformation.LastUpdate = DateTime.MinValue;
                        var movI = mi.MetaInformation as MovieInformation;
                        movI.Actors = Enumerable.Range(1, movI.Actors.Length).Select(i => new ActorInformation()).ToArray();
                    }
                    if (mi.Path.StartsWith("/Cache/"))
                        mi.Path = $"/Cache/{mi.Name}";
                    return mi;
                })
                .ToArray();
            AssertRecordsEqual(actual, expected);

            if (resetCache)
                MediaLibrary.ClearCaches();

            var actualMovie = MediaLibrary
                .GetMovies()
                .Take(1)
                .Select(m =>
                {
                    m = m.Clone() as Movie;
                    m.CreatedAt = DateTime.MinValue;
                    m.ReleaseDate = DateTime.MinValue;
                    m.PremieredAt = DateTime.MinValue;
                    return m;
                })
                .ToArray();
            var expectedMovie = new Movie[]
            {
                new Movie(null)
                {
                    Id = 1,
                    BannerPath = "",
                    PicturePath = "",
                    CollectionId = 2,
                    CreatedAt = DateTime.MinValue,
                    DownloadMediaItemId = 3,
                    Enabled = true,
                    Genres = new string[] { "Comedy", "Drama", "Romance" },
                    IsSingle = true,
                    Language = null,
                    MediaItemIds = new long[] { 1, 3 },
                    Name = "(500) Days of Summer",
                    OriginalTitle = "(500) Days of Summer",
                    Plot = "Tom ist in Summer verliebt. Seit sie die Schwelle zur Grußkartenfirma, in der er arbeitet, überschritten hat, ist sich der Möchtegern-Architekt sicher: Summer ist die Frau, mit der er sein restliches Leben verbringen möchte. Doch Toms Traumfrau glaubt weder an die Liebe, noch will sie eine Beziehung führen. Abgesehen von diesem fundamentalen Widerspruch haben Summer und Tom jedoch so viele Gemeinsamkeiten, dass sich aus der Bürobekanntschaft bald eine Freundschaft mit Extras entwickelt. Als Summer die Quasi-Beziehung nach 500 Tagen himmlischer Höhen und traumatischer Tiefen schließlich beendet, ruft sich Tom die prägendsten Momente ihres Zusammenseins immer wieder vor Augen, um herauszufinden, warum sein Happily Everafter ein solch jähes Ende fand…",
                    PremieredAt = DateTime.MinValue,
                    ReleaseDate = DateTime.MinValue,
                    TrailerMediaItemId = 0,                    
                    Type = EntryType.Movie,
                    Visible = true
                }
            };
            AssertRecordsEqual(actualMovie, expectedMovie);
            actualOverviewEntries = MediaLibrary.GetOverview(0, 10, "", "", EntryType.Movie).ToArray();
            AssertRecordCount(actualOverviewEntries, 1);

            if (resetCache)
                MediaLibrary.ClearCaches();

            //Film erneut laden
            await ExecuteDownloads(movie);
            actual = MediaLibrary.GetMediaItems()
                .Select(mi =>
                {
                    mi = mi.Clone() as MediaItem;
                    if (mi.CopyType == MediaItemCopyType.Cache)
                    {
                        AssertTrue(mi.DueDate != DateTime.MinValue, $"No due date is set for downloaded media item.");
                        mi.DueDate = DateTime.MinValue;
                    }
                    mi.CreatedAt = DateTime.MinValue;
                    mi.LastAccess = DateTime.MinValue;
                    mi.LastClassificationTry = DateTime.MinValue;
                    mi.LastMetaInformationUpdate = DateTime.MinValue;
                    if (mi.MetaInformation is not null)
                    {
                        mi.MetaInformation.LastUpdate = DateTime.MinValue;
                        var movI = mi.MetaInformation as MovieInformation;
                        movI.Actors = Enumerable.Range(1, movI.Actors.Length).Select(i => new ActorInformation()).ToArray();
                    }
                    if (mi.Path.StartsWith("/Cache/"))
                        mi.Path = $"/Cache/{mi.Name}";
                    return mi;
                })
                .ToArray();
            AssertRecordsEqual(actual, expected);
            actualOverviewEntries = MediaLibrary.GetOverview(0, 10, "", "", EntryType.Movie).ToArray();
            AssertRecordCount(actualOverviewEntries, 1);
        }
    }
}
