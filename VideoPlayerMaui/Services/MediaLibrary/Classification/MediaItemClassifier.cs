using System;
using System.Linq;
using VideoPlayer.Models;
using VideoPlayer.Models.MediaItems;
using VideoPlayer.Models.MetaInformation;
using VideoPlayer.Models.Movies;
using VideoPlayer.Models.TVShows;

namespace VideoPlayer.Services.MediaLibrary.Classification
{
    public interface IMediaItemClassifier { }

    public class MediaItemClassifier: IMediaItemClassifier
    {

        private readonly IMediaLibrary mediaLibrary;

        public MediaItemClassifier(IMediaLibrary mediaLibrary)
            : base()
        {
            this.mediaLibrary = mediaLibrary;
            this.mediaLibrary.ModelElementAdded += MediaLibrary_ModelElementAddedAsync;
            this.mediaLibrary.ModelElementUpdated += MediaLibrary_ModelElementUpdatedAsync;
            this.mediaLibrary.ModelElementRemoved += MediaLibrary_ModelElementRemoved;
        }

        private void MediaLibrary_ModelElementRemoved(object sender, BaseModelEventArgs e)
        {
            RemoveMediaItemAsync(e.Element as MediaItem);
        }

        private void MediaLibrary_ModelElementUpdatedAsync(object sender, BaseModelEventArgs e)
        {
            CollectMediaItemAsync(e.Element as MediaItem).Wait();
        }

        private void MediaLibrary_ModelElementAddedAsync(object sender, BaseModelEventArgs e)
        {
            CollectMediaItemAsync(e.Element as MediaItem).Wait();
        }

        private async Task RemoveMediaItemAsync(MediaItem mediaItem)
        {
            if (mediaItem == null)
                return;
            if (mediaItem.CopyType != MediaItemCopyType.None)
                await RemoveDuplicate(mediaItem);
        }

        private async Task RemoveDuplicate(MediaItem mediaItem)
        {
            var episode = await mediaLibrary.FindTVShowEpisodeByMediaItem(mediaItem.OriginalMediaItemId);
            await RemoveTVShowEpisode(episode, mediaItem);
        }

        private async Task RemoveTVShowEpisode(TVShowEpisode episode, MediaItem mediaItem)
        {
            if (episode == null)
                return;
            var season = await mediaLibrary.GetTVShowSeason(episode.SeasonId);
            var show = await mediaLibrary.GetTVShow(season.ShowId);
            switch (mediaItem.CopyType)
            {
                case MediaItemCopyType.Download:
                    episode.DownloadMediaItem = null;
                    break;
            }
            await mediaLibrary.AddTVShowEpisodeAsync(show, season, episode);
        }

        private async Task CollectMediaItemAsync(MediaItem mediaItem)
        {
            if (mediaItem == null)
                return;
            if (mediaItem.CopyType != MediaItemCopyType.None)
                await CollectDuplicate(mediaItem);
            else if (mediaItem.MetaInfo is MovieInformation)
                await CollectMovieAsync(mediaItem, mediaItem.MetaInfo as MovieInformation);
            else if (mediaItem.MetaInfo is EpisodeInformation)
                await CollectTVShowAsync(mediaItem, mediaItem.MetaInfo as EpisodeInformation);
        }

        private async Task CollectDuplicate(MediaItem mediaItem)
        {
            var episode = await mediaLibrary.FindTVShowEpisodeByMediaItem(mediaItem.OriginalMediaItemId);
            await CollectTVShowEpisode(episode, mediaItem);
        }

        private async Task CollectTVShowEpisode(TVShowEpisode episode, MediaItem mediaItem)
        {
            if (episode == null)
                return;
            var season = await mediaLibrary.GetTVShowSeason(episode.SeasonId);
            var show = await mediaLibrary.GetTVShow(season.ShowId);
            switch (mediaItem.CopyType)
            {
                case MediaItemCopyType.Download:
                    episode.DownloadMediaItem = mediaItem;
                    break;
            }

            await mediaLibrary.AddTVShowEpisodeAsync(show, season, episode);
        }

        private async Task CollectTVShowAsync(MediaItem mediaItem, EpisodeInformation episodeInformation)
        {
            TVShowInformation showInformation = null;
            MediaItemCollection showCollection = null;
            MediaItemCollection seasonCollection = null;
            var collection = await mediaLibrary.GetMediaItemCollectionAsync(mediaItem.ParentCollectionId);
            while ((showInformation == null) && (collection != null))
            {
                showInformation = collection.MetaInfo as TVShowInformation;
                if (showInformation != null)
                    showCollection = collection;

                if (showInformation != null)
                    seasonCollection = collection;
                collection = await mediaLibrary.GetMediaItemCollectionAsync(collection.ParentCollectionId);
            }

            var show = new TVShow()
            {
                Name = showInformation?.Title ?? episodeInformation.ShowName,
                PicturePath = showCollection?.PicturePath,
                BannerPath = showCollection?.BannerPath
            };
            var season = new TVShowSeason()
            {
                Name = $"{episodeInformation.Season}",
                PicturePath = seasonCollection?.PicturePath,
                BannerPath = seasonCollection?.BannerPath ?? show.BannerPath
            };
            var episode = new TVShowEpisode() { Name = mediaItem.Name, };
            episode.Name = episodeInformation.Title;
            episode.EpisodeNo = episodeInformation.Episode;
            episode.MediaItems = new long[] { mediaItem.Id };
            episode.PrimaryMediaItem = mediaItem;
            episode.PicturePath = mediaItem.PicturePath;
            episode.Plot = episodeInformation.Plot;
            await CollectShowAsync(show, season, episode);
        }

        private async Task CollectShowAsync(TVShow show, TVShowSeason season, TVShowEpisode episode)
        {
            if (string.IsNullOrWhiteSpace(show.Name))
                return;

            var existingShows = await mediaLibrary.FindTVShowByNameAsync(show.Name);
            var existingShow = (show.Id != 0) ? (await mediaLibrary.FindTVShowAsync(show.Id)) : existingShows.FirstOrDefault();
            if (existingShow == null)
            {
                await mediaLibrary.AddTVShowAsync(show);
                await mediaLibrary.AddTVShowSeasonAsync(show, season);
                await mediaLibrary.AddTVShowEpisodeAsync(existingShow, season, episode);
                return;
            }
            existingShow.PicturePath = show.PicturePath ?? existingShow.PicturePath;
            existingShow.BannerPath = show.BannerPath ?? existingShow.BannerPath;
            await mediaLibrary.AddTVShowAsync(existingShow);

            var existingSeason = (await mediaLibrary.GetTVShowSeasons(existingShow.Id))
                .Where(s => s != null)
                .FirstOrDefault(s => s.Name == season.Name);
            if (existingSeason == null)
            {
                if (int.TryParse(season.Name, out var seasonNo))
                    season.Name = $"Staffel {seasonNo.ToString().PadLeft(2, '0')}";
                existingSeason = (await mediaLibrary.GetTVShowSeasons(existingShow.Id))
                    .Where(s => s != null)
                    .FirstOrDefault(s => s.Name == season.Name);
            }
            if (existingSeason == null)
            {
                await mediaLibrary.AddTVShowSeasonAsync(existingShow, season);
                return;
            }
            if (int.TryParse(existingSeason.Name, out var sNo))
                existingSeason.Name = $"Staffel {sNo.ToString().PadLeft(2, '0')}";
            existingSeason.PicturePath = season.PicturePath ?? existingSeason.PicturePath;
            existingSeason.BannerPath = show.BannerPath ?? existingSeason.BannerPath;
            await mediaLibrary.AddTVShowSeasonAsync(existingShow, existingSeason);

            var existingEpisode = (await mediaLibrary.GetTVShowEpisodes(existingSeason.Id))
                .FirstOrDefault(e => e.EpisodeNo == episode.EpisodeNo);
            if (existingEpisode == null)
            {
                if (int.TryParse(episode.EpisodeNo, out var episodeNo))
                    episode.EpisodeNo = $"Folge {episodeNo.ToString().PadLeft(2, '0')}";
                existingEpisode = (await mediaLibrary.GetTVShowEpisodes(existingSeason.Id))
                    .FirstOrDefault(e => e.EpisodeNo == episode.EpisodeNo);
            }
            if (existingEpisode == null)
            {
                await mediaLibrary.AddTVShowEpisodeAsync(existingShow, existingSeason, episode);
                return;
            }
            if (int.TryParse(existingEpisode.EpisodeNo, out var eNo))
                existingEpisode.EpisodeNo = $"Folge {eNo.ToString().PadLeft(2, '0')}";
            existingEpisode.Name = existingEpisode.Name ?? episode.Name;
            existingEpisode.PicturePath = episode.PicturePath ?? existingEpisode.PicturePath;
            existingEpisode.MediaItems = existingEpisode
                .MediaItems
                .Concat(episode.MediaItems)
                .Distinct()
                .OrderBy(id => id)
                .ToArray();
            existingEpisode.PrimaryMediaItem = episode.PrimaryMediaItem;
            await mediaLibrary.AddTVShowEpisodeAsync(existingShow, existingSeason, existingEpisode);
        }

        private async Task CollectMovieAsync(MediaItem mediaItem, MovieInformation movieInformation)
        {
            var movieCollection = await mediaLibrary.GetMediaItemCollectionAsync(mediaItem.ParentCollectionId);
            var source = await mediaLibrary.GetSourceAsync(movieCollection.MediaSourceId);
            MovieCollection collection = null;
            if (movieCollection.ParentCollectionId != 0)
            {
                collection = new MovieCollection()
                {
                    Name = movieCollection.Name,
                    PicturePath = movieCollection.PicturePath,
                    MediaItemCollectionId = movieCollection.Id
                };
                var existingCollection = (await mediaLibrary.FindMovieCollectionByNameAsync(movieCollection.Name)).FirstOrDefault();
                if (existingCollection == null)
                    await mediaLibrary.AddMovieCollectionAsync(collection);
                else
                {
                    existingCollection.PicturePath = collection.PicturePath;
                    existingCollection.MediaItemCollectionId = collection.MediaItemCollectionId;
                    await mediaLibrary.AddMovieCollectionAsync(existingCollection);

                    collection = existingCollection;
                }
            }

            var movie = new Movie() { Name = mediaItem.Name };
            movie.CollectionId = collection?.Id ?? 0;
            movie.Name = movieInformation.Title;
            movie.Genre = movieInformation.Genre;
            movie.Plot = movieInformation.Plot;
            movie.Date = movieInformation.ReleaseDate;
            if ((movie.Date == default(DateTime)) && (movieInformation.Year > 0))
                movie.Date = new DateTime(movieInformation.Year, 1, 1);
            movie.MediaItems = new long[] { mediaItem.Id };
            movie.PicturePath = mediaItem.PicturePath;

            var existingMovie = await mediaLibrary.FindMovieAsync(mediaItem.Id);
            if (existingMovie == null)
            {
                await mediaLibrary.AddMovieAsync(movie);
                return;
            }
            existingMovie.CollectionId = (movie.CollectionId != 0) ? movie.CollectionId : existingMovie.CollectionId;
            existingMovie.Genre = movieInformation.Genre ?? existingMovie.Genre;
            existingMovie.Plot = movieInformation.Plot ?? existingMovie.Plot;
            existingMovie.MediaItems = existingMovie
                .MediaItems
                .Concat(movie.MediaItems)
                .Distinct()
                .OrderBy(id => id)
                .ToArray();
            movie.PicturePath = mediaItem.PicturePath;
            await mediaLibrary.AddMovieAsync(existingMovie);
        }

    }
}
