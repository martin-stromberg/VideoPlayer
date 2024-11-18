using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.Service.Library.Models.Classified;

namespace VideoPlayer.Service.Library
{
    public interface IMediaCollectionSelector
    {
        TVShowSeason FindFirstSeason(TVShow show);
        ClassifiedEntry FindFirstEpisode(TVShow show);
        ClassifiedEntry FindNextEntry(ClassifiedEntry mediaItem);
        MediaItem FindNextMediaItem(MediaItem mediaItem);
        IEnumerable<ClassifiedEntry> FindNextEntries(ClassifiedEntry entry);
    }
    public  class MediaCollectionSelector: IMediaCollectionSelector
    {
        private readonly IMediaLibrary mediaLibrary;

        public MediaCollectionSelector(IMediaLibrary mediaLibrary)
        {
            this.mediaLibrary = mediaLibrary;
        }

        public ClassifiedEntry FindNextEntry(ClassifiedEntry mediaItem)
        {
            return FindNextEpisode(mediaItem as TVShowEpisode) 
                ?? FindFirstEpisode(mediaItem as TVShowSeason) 
                ?? FindFirstEpisode(mediaItem as TVShow)
                ?? FindNextCollectionMovie(mediaItem as Movie)
                ?? FindFirstCollectionMovie(mediaItem as MovieCollection);
        }

        public IEnumerable<ClassifiedEntry> FindNextEntries(ClassifiedEntry entry)
        {
            foreach (var episode in FindEpisodes(entry as TVShow))
                yield return episode;
            foreach (var episode in FindEpisodes(entry as TVShowSeason))
                yield return episode;
            foreach (var episode in FindEpisodes(entry as TVShowEpisode))
                yield return episode;
            foreach (var movie in FindMovies(entry as MovieCollection))
                yield return movie;
            foreach (var movie in FindMovies(entry as Movie))
                yield return movie;
        }

        private IEnumerable<Movie> FindMovies(Movie movie)
        {
            if (movie is not null && movie.CollectionId != 0)
            {
                var collection = mediaLibrary.GetMovieCollection(movie.CollectionId);
                foreach (var nextMovie in FindMovies(collection)
                    .SkipWhile(m => m.Id != movie.Id))
                    yield return nextMovie;
            }
        }

        private IEnumerable<Movie> FindMovies(MovieCollection movieCollection)
        {
            if (movieCollection is not null)
            foreach (var movie in mediaLibrary.GetCollectionMovies(movieCollection.Id)
                .OrderBy(m => m.ReleaseDate)
                .ThenBy(m => m.PremieredAt)
                .ThenBy(m => m.Name)
                .ThenBy(m => m.Id))
                yield return movie;
        }

        private IEnumerable<TVShowEpisode> FindEpisodes(TVShowEpisode episode)
        {
            if (episode is not null)
            {
                var season = mediaLibrary.GetTVShowSeason(episode.SeasonId);
                foreach (var entry in FindEpisodes(season)
                    .SkipWhile(e => e.Id != episode.Id))
                    yield return entry;
            }
        }

        private IEnumerable<TVShowEpisode> FindEpisodes(TVShow show)
        {
            foreach (var season in FindSeasons(show))
                foreach (var episode in FindEpisodes(season))
                    yield return episode;
        }

        public MediaItem FindNextMediaItem(MediaItem mediaItem)
        {
            var entry = mediaLibrary.GetMovieByMediaItem(mediaItem.Id) as ClassifiedEntry
                ?? mediaLibrary.GetTVShowEpisodeByMediaItem(mediaItem.Id);
            entry = FindNextEntry(entry);
            if (entry is null) return null;

            return (entry as IMediaItemCollectionEntry).MediaItemIds
                .Select(id => mediaLibrary.GetMediaItem(id))
                .Where(mi => mi.CopyType == MediaItemCopyType.Original
                    || mi.CopyType == MediaItemCopyType.Download
                    || mi.CopyType == MediaItemCopyType.Cache)
                .OrderByDescending(mi => mi.CopyType)
                .FirstOrDefault();
        }

        private ClassifiedEntry FindFirstCollectionMovie(MovieCollection movieCollection)
        {
            if (movieCollection is null) return null;
            return mediaLibrary
                .GetCollectionMovies(movieCollection.Id)
                .OrderBy(m => m.ReleaseDate)
                .ThenBy(m => m.PremieredAt)
                .ThenBy(m => m.Name)
                .ThenBy(m => m.Id)                
                .FirstOrDefault();
        }

        private ClassifiedEntry FindNextCollectionMovie(Movie movie)
        {
            if (movie is null) return null;
            if (movie.CollectionId == 0)
                return null;
            var collection = mediaLibrary.GetMovieCollection(movie.CollectionId);
            return FindMovies(collection)
                .SkipWhile(m => m.Id != movie.Id)
                .SkipWhile(m => m.Id == movie.Id)
                .FirstOrDefault();
        }

        private IEnumerable<TVShowSeason> FindSeasons(TVShow show)
        {
            if (show is not null)
            {
                var seasons = mediaLibrary.GetSeasons(show.Id)
                    .OrderBy(m => m.Number);
                foreach (var season in seasons)
                    yield return season;
            }
        }
        public TVShowSeason FindFirstSeason(TVShow show)
        {
            if (show is null) return null;
            var season = FindSeasons(show)
                .FirstOrDefault();
            return season;
        }
        public ClassifiedEntry FindFirstEpisode(TVShow show)
        {
            if (show is null) return null;
            return FindFirstEpisode(FindFirstSeason(show));
        }

        private IEnumerable<TVShowEpisode> FindEpisodes(TVShowSeason season)
        {
            if (season is not null)
            foreach (var episode in mediaLibrary.GetEpisodes(season.Id)
                .OrderBy(e => e.Episode)
                .ThenBy(e => e.Part)
                .ThenBy(e => e.Name))
                yield return episode;
        }
        private ClassifiedEntry FindFirstEpisode(TVShowSeason season)
        {
            if (season is null) return null;
            return FindEpisodes(season)
                .FirstOrDefault();
        }

        private ClassifiedEntry FindNextEpisode(TVShowEpisode episode)
        {
            if (episode is null) return null;
            var season = mediaLibrary.GetTVShowSeason(episode.SeasonId);
            if (season is null) return null;
            var episodes = mediaLibrary.GetEpisodes(season.Id)
                .OrderBy(e => e.Episode)
                .ThenBy(e => e.Part)
                .ThenBy(e => e.Name)
                .ToArray();
            episode = episodes
                .SkipWhile(e => e.Id != episode.Id)
                .SkipWhile(e => e.Id == episode.Id)
                .FirstOrDefault();
            if (episode is not null)
                return episode;

            var show = mediaLibrary.GetTVShow(season.ShowId);
            season = mediaLibrary.GetSeasons(show.Id)
                .OrderBy(s => s.Number)
                .ThenBy(s => s.Name)
                .SkipWhile(s => s.Id != season.Id)
                .SkipWhile(s => s.Id == season.Id)
                .FirstOrDefault();
            return FindFirstEpisode(season);
        }

        
    }
}
