using System;
using System.Linq;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Tests.Helper;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace VideoPlayer.Tests.Services.Library.Scanner
{
    [Disabled]
    public class SingleMovieMediaClassifierTests: BaseTest
    {

        protected override void Init(object argument)
        {
            base.Init(argument);
            InitializeEmptyDatabase();
            InitializeClassifier(false);
            InitializeScanner(false);
            AddSingleMovie();
        }

        

        protected override void MediaClassifier_MediaItemClassified(object sender, BaseServiceModelEventArgs e)
        {
            base.MediaClassifier_MediaItemClassified(sender, e);
            var mediaItems = MediaLibrary.GetMediaItems().ToArray();
            var movies = MediaLibrary.GetMovies().ToArray();
            AssertRecordCount(movies, movies.Length);
        }

        protected override async Task ExecuteAsync(object argument)
        {
            
            await ExecuteScanAndClassification(() =>
            {
                var mediaItems = MediaLibrary.GetUnclassifiedMediaItems().ToArray();
                AssertRecordCount(mediaItems, 11);
            });

            var entries = MediaLibrary
                .GetOverview(0, 10, "", EntryType.Movie, EntryType.MovieCollection)
                .OfType<Movie>()
                .Select(e =>
                {
                    AssertTrue(e.CreatedAt > LastExecutionBegin, $"Invalid created at {e.CreatedAt} for movie.");
                    AssertTrue(string.IsNullOrWhiteSpace(e.BannerPath), $"{nameof(e.BannerPath)} is not empty");
                    AssertTrue(!string.IsNullOrWhiteSpace(e.PicturePath), $"{nameof(e.PicturePath)} is empty");
                    e.CreatedAt = DateTime.MinValue;
                    e.BannerPath = string.Empty;
                    e.PicturePath = string.Empty;
                    return e;
                })
                .ToArray();
            var expected = new ClassifiedEntry[] { 
                new Movie(null)
                {
                    BannerPath = "",
                    CollectionId = 2,
                    CreatedAt = DateTime.MinValue,
                    DownloadMediaItemId = 0,
                    Enabled = true,
                    Genres = new[] { "Comedy", "Drama", "Romance" },
                    Id = 1,
                    IsSingle = true,
                    Language = null,
                    MediaItemIds   = new long[] { 1 },
                    Name = "(500) Days of Summer",
                    OriginalTitle = "(500) Days of Summer",
                    PicturePath = "",
                    Plot = "Tom ist in Summer verliebt. Seit sie die Schwelle zur Grußkartenfirma, in der er arbeitet, überschritten hat, ist sich der Möchtegern-Architekt sicher: Summer ist die Frau, mit der er sein restliches Leben verbringen möchte. Doch Toms Traumfrau glaubt weder an die Liebe, noch will sie eine Beziehung führen. Abgesehen von diesem fundamentalen Widerspruch haben Summer und Tom jedoch so viele Gemeinsamkeiten, dass sich aus der Bürobekanntschaft bald eine Freundschaft mit Extras entwickelt. Als Summer die Quasi-Beziehung nach 500 Tagen himmlischer Höhen und traumatischer Tiefen schließlich beendet, ruft sich Tom die prägendsten Momente ihres Zusammenseins immer wieder vor Augen, um herauszufinden, warum sein Happily Everafter ein solch jähes Ende fand…",
                    PremieredAt = new DateTime(2009, 10, 22),
                    ReleaseDate = new DateTime(2009, 10, 22),
                    TrailerMediaItemId = 0,
                    Type = EntryType.Movie,
                    Visible = true                    
                }
            };
            AssertRecordsEqual( entries , expected);
        }

    }
}
