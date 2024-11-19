using System;
using System.Linq;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Tests.Helper;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace VideoPlayer.Tests.Services.Library.Scanner
{
    public class MultiMovieMediaClassifierTests : BaseTest
    {

        protected override void Init(object argument)
        {
            base.Init(argument);
            InitializeEmptyDatabase();
            InitializeClassifier(false);
            InitializeScanner(false);
            AddMultiMovie();
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
                AssertRecordCount(mediaItems, 26);
            });

            var entries = MediaLibrary
                .GetOverview(0, 10, "", EntryType.Movie, EntryType.MovieCollection)
                .OfType<MovieCollection>()
                .Select(e =>
                {
                    AssertTrue(e.CreatedAt > LastExecutionBegin, $"Invalid created at {e.CreatedAt} for movie.");
                    AssertTrue(!string.IsNullOrWhiteSpace(e.BannerPath), $"{nameof(e.BannerPath)} is empty");
                    AssertTrue(!string.IsNullOrWhiteSpace(e.PicturePath), $"{nameof(e.PicturePath)} is empty");
                    e.CreatedAt = DateTime.MinValue;
                    e.BannerPath = string.Empty;
                    e.PicturePath = string.Empty;
                    return e;
                })
                .ToArray();
            var expected = new ClassifiedEntry[] { 
                new MovieCollection(null)
                {
                    BannerPath = "",
                    BannerBackgroundColor = "#60715F",
                    CreatedAt = DateTime.MinValue,
                    Enabled = true,
                    Id = 2,
                    IsSingle = false,
                    Name = "Bad Boys",
                    PicturePath = "",
                    PictureBackgroundColor = "#757960",
                    PremieredAt = new DateTime(1995, 06, 01),
                    ReleaseDate = new DateTime(1995, 06, 01),
                    Type = EntryType.MovieCollection,
                    Visible = true,
                    MediaItemCollectionId = 2,
                    Genres = new string[] { "Action", "Comedy", "Crime", "Drama", "Thriller" }
                }
            };
            AssertRecordsEqual( entries , expected);
        }

    }
}
