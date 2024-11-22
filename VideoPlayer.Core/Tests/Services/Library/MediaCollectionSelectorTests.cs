using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Tests.Helper;

namespace VideoPlayer.Tests.Services.Library
{
    [Disabled]
    public class MediaCollectionSelectorTests : BaseTest
    {
        protected override void Init(object loopArgument)
        {
            base.Init(loopArgument);
            InitializeEmptyDatabase();
            InitializeClassifier(false);
            InitializeScanner(false);
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
            ExecuteSelectorWithEpisodeEntries();
            ExecuteSelectorWithEpisodeItems();
        }

        private void ExecuteSelectorWithEpisodeEntries()
        {
            MediaCollectionSelector selector = new MediaCollectionSelector(MediaLibrary);
            var shows = MediaLibrary.GetOverview(0, int.MaxValue, "", Service.Library.Models.Classified.EntryType.TVShow);
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
            var currentEpisode = episodes.FirstOrDefault();
            episodes.RemoveAt(0);
            while (currentEpisode is not null)
            {                
                var nextEpisode = selector.FindNextEntry(currentEpisode) as TVShowEpisode;
                var expectedEpisode = episodes.FirstOrDefault();
                AssertObjectsEqual(nextEpisode, expectedEpisode);

                currentEpisode = nextEpisode;
                if (currentEpisode is not null)
                    episodes.RemoveAt(0);
            }
            AssertFalse(episodes.Any(), $"Remaining episode list is not empty.");
        }

        private void ExecuteSelectorWithEpisodeItems()
        {
            MediaCollectionSelector selector = new MediaCollectionSelector(MediaLibrary);
            var shows = MediaLibrary.GetOverview(0, int.MaxValue, "", Service.Library.Models.Classified.EntryType.TVShow);
            var show = shows.FirstOrDefault();
            var seasons = MediaLibrary.GetSeasons(show.Id)
                .OrderBy(s => s.Number)
                .ToArray();
            var episodes = seasons
                .SelectMany(s => MediaLibrary
                    .GetEpisodes(s.Id)
                    .OrderBy(e => e.Episode)
                    .ThenBy(e => e.Part))
                .Select(episode =>
                {
                    return episode.MediaItemIds
                        .Select(id => MediaLibrary.GetMediaItem(id))
                        .Where(mi => mi.CopyType != Service.Library.Models.MediaItemCopyType.Trailer)
                        .OrderByDescending(mi => mi.CopyType)
                        .FirstOrDefault();
                })
                .ToList();
            var currentEpisode = episodes.FirstOrDefault();
            episodes.RemoveAt(0);
            while (currentEpisode is not null)
            {
                var nextEpisode = selector.FindNextMediaItem(currentEpisode);
                var expectedEpisode = episodes.FirstOrDefault();
                AssertObjectsEqual(nextEpisode, expectedEpisode);

                currentEpisode = nextEpisode;
                if (currentEpisode is not null)
                    episodes.RemoveAt(0);
            }
            AssertFalse(episodes.Any(), $"Remaining episode list is not empty.");
        }
    }
}
