using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Tests.Helper;

namespace VideoPlayer.Tests.Services.Library.Scanner
{
    [Disabled]
    public class TVShowClassifierTests : BaseTest
    {
        protected override void Init(object argument)
        {
            base.Init(argument);
            InitializeEmptyDatabase();
            InitializeClassifier(false);
            InitializeScanner(false);
            AddTVShow();
        }

        
        protected override async Task ExecuteAsync(object argument)
        {
            await ExecuteScanAndClassification(() =>
            {
                var mediaItems = MediaLibrary.GetUnclassifiedMediaItems().ToArray();
                AssertRecordCount(mediaItems, 74);
            });

            var entries = MediaLibrary
                .GetOverview(0, 10, "", EntryType.TVShow, EntryType.TVShowCollection)
                .OfType<TVShow>()
                .Select(e =>
                {
                    AssertTrue(e.CreatedAt > LastExecutionBegin, $"Invalid created at {e.CreatedAt} for movie.");
                    AssertTrue(!string.IsNullOrWhiteSpace(e.BannerPath), $"{nameof(e.BannerPath)} is not empty");
                    AssertTrue(!string.IsNullOrWhiteSpace(e.PicturePath), $"{nameof(e.PicturePath)} is empty");
                    e.CreatedAt = DateTime.MinValue;
                    e.BannerPath = string.Empty;
                    e.PicturePath = string.Empty;
                    return e;
                })
                .ToArray();
            var expected = new ClassifiedEntry[] {
                new TVShow(null)
                {
                    BannerPath = "",
                    CreatedAt = DateTime.MinValue,
                    Enabled = true,
                    Id = 1,
                    Language = "de-DE",
                    OriginalName = "",                    
                    Name = "How I Met Your Mother (DE)",
                    PicturePath = "",
                    Plot = "Ted Mosby erzählt im Jahr 2030 seinen Kindern alle Details darüber, wie er seine Frau kennengelernt hat. Seine Erläuterungen beginnen im Jahr 2005, als sich Ted als Architekturstudent eine Wohnung mit seinem Freund Marshall teilt. Kurze Zeit später lernt Ted die Nachrichtensprecherin Robin kennen und lieben. Sie will jedoch von einer festen Beziehung noch gar nichts wissen ... Völlig unklar ist deshalb, welche der Frauen an Teds Seite nun die Mutter seiner Kinder wird.",
                    PremieredAt = new DateTime(2005, 09, 19),
                    ReleaseDate = new DateTime(2005, 09, 19),
                    Type = EntryType.TVShow,
                    Visible = true
                }
            };
            AssertRecordsEqual(entries, expected);

            var show = entries.First();
            var seasons = MediaLibrary.GetSeasons(show.Id)
                .Select(e =>
                {
                    AssertTrue(e.CreatedAt > LastExecutionBegin, $"Invalid created at {e.CreatedAt} for movie.");
                    AssertTrue(!string.IsNullOrWhiteSpace(e.BannerPath), $"{nameof(e.BannerPath)} is not empty");
                    AssertTrue(!string.IsNullOrWhiteSpace(e.PicturePath), $"{nameof(e.PicturePath)} is empty");
                    e.CreatedAt = DateTime.MinValue;
                    e.BannerPath = string.Empty;
                    e.PicturePath = string.Empty;
                    return e;
                })
                .ToArray();
            var expectedSeasons = new TVShowSeason[]
            {
                new TVShowSeason(null)
                {
                    BannerPath = "",
                    CreatedAt = DateTime.MinValue,
                    Enabled = true,
                    Id = 2,
                    Name = null,
                    Number = 1,
                    Type = EntryType.TVShowSeason,
                    PicturePath = "",
                    PremieredAt = new DateTime(2005, 09, 19),
                    ReleaseDate = new DateTime(2005, 09, 19),
                    ShowId = show.Id,
                    Visible = true
                },
                new TVShowSeason(null)
                {
                    BannerPath = "",
                    CreatedAt = DateTime.MinValue,
                    Enabled = true,
                    Id = 7,
                    Name = null,
                    Number = 2,
                    Type = EntryType.TVShowSeason,
                    PicturePath = "",
                    PremieredAt = new DateTime(2006, 09, 18),
                    ReleaseDate = new DateTime(2006, 09, 18),
                    ShowId = show.Id,
                    Visible = true
                },
                new TVShowSeason(null)
                {
                    BannerPath = "",
                    CreatedAt = DateTime.MinValue,
                    Enabled = true,
                    Id = 12,
                    Name = null,
                    Number = 3,
                    Type = EntryType.TVShowSeason,
                    PicturePath = "",
                    PremieredAt = new DateTime(2007, 09, 24),
                    ReleaseDate = new DateTime(2007, 09, 24),
                    ShowId = show.Id,
                    Visible = true
                }
            };
            AssertRecordsEqual(seasons, expectedSeasons);

            var episodes = seasons
                .SelectMany(s => MediaLibrary.GetEpisodes(s.Id))
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
            AssertRecordCount(episodes, 11);
            episodes = episodes.Take(3).ToArray();
            var expectedEpisodes = new TVShowEpisode[] { 
                new TVShowEpisode(null)
                {
                    BannerPath = "",
                    CreatedAt = DateTime.MinValue,
                    DownloadMediaItemId =0,
                    Enabled = true,
                    Episode = 1,
                    Id = 3,
                    Language = "",
                    MediaItemIds = new long[]{ 2 },
                    Name = "Verliebt, verlobt, versagt",
                    OriginalName = "",
                    Part = "",
                    PicturePath = "",
                    Plot = "Als Teds Freund Marshall seiner Freundin Lily einen Heiratsantrag macht, stellt der Architekturstudent fest, dass ihm etwas Wesentliches in seinem Leben fehlt: Er möchte sich auch endlich einmal so richtig verlieben und heiraten. Kurze Zeit später lernt er tatsächlich in einer Bar die Fernsehmoderatorin Robin kennen und ist sofort Feuer und Flamme für die Frau. Doch das erste Date verläuft für Ted absolut nicht so, wie er sich das eigentlich vorgestellt hätte...",
                    PremieredAt = new DateTime(2005, 09, 19),
                    ReleaseDate = new DateTime(2005, 09, 19),
                    SeasonId = 2,
                    Type = EntryType.TVShowEpisode,
                    Visible = true
                },
                new TVShowEpisode(null)
                {
                    BannerPath = "",
                    CreatedAt = DateTime.MinValue,
                    DownloadMediaItemId =0,
                    Enabled = true,
                    Episode = 2,
                    Id = 4,
                    Language = "",
                    MediaItemIds = new long[]{ 7 },
                    Name = "Die lila Giraffe",
                    OriginalName = "",
                    Part = "",
                    PicturePath = "",
                    Plot = "Ted kann Robin nicht vergessen und ist fest davon überzeugt, dass sie seine zukünftige Frau sein könnte. Allerdings muss er dann von Lily erfahren, dass Robin an keiner festen Beziehung interessiert ist. Ted versucht daraufhin alles, sich nach außen hin locker zu zeigen. Insgeheim entwickelt er jedoch einen Plan, um zu einem zweiten Date mit Robin zu kommen. Er lädt sie ganz zufällig zu einer Party bei sich ein, die jedoch erst noch organisiert werden muss...",
                    PremieredAt = new DateTime(2005, 09, 26),
                    ReleaseDate = new DateTime(2005, 09, 26),
                    SeasonId = 2,
                    Type = EntryType.TVShowEpisode,
                    Visible = true
                },
                new TVShowEpisode(null)
                {
                    BannerPath = "",
                    CreatedAt = DateTime.MinValue,
                    DownloadMediaItemId =0,
                    Enabled = true,
                    Episode = 3,
                    Id = 5,
                    Language = "",
                    MediaItemIds = new long[]{ 12 },
                    Name = "Frauen, Flieger, Freiheit",
                    OriginalName = "",
                    Part = "",
                    PicturePath = "",
                    Plot = "Nachdem Robin Ted versichert hat, dass sie nur Freunde sind, lässt er sich widerwillig von seinem Freund Barney überreden, mit ihm zum Flughafen zu fahren, wo es von attraktiven Mädchen nur so wimmelt. Und ehe sich beide versehen, sitzen sie auch schon mit zwei davon in einem Flieger, der sie nach Philadelphia bringt. Während des Flugs müssen sie jedoch erfahren, dass die Frauen auf dem Weg zu ihren Freunden sind. Und dann hat es auch noch die Polizei auf die beiden abgesehen...",
                    PremieredAt = new DateTime(2005, 10, 03),
                    ReleaseDate = new DateTime(2005, 10, 03),
                    SeasonId = 2,
                    Type = EntryType.TVShowEpisode,
                    Visible = true
                }
            };
            AssertRecordsEqual(episodes, expectedEpisodes);
        }
    }
}
