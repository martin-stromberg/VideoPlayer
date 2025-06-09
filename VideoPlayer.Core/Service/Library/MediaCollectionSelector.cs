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
        ClassifiedEntry FindFirstEpisode(TVShowSeason season);
        ClassifiedEntry FindNextEntry(ClassifiedEntry mediaItem);
        MediaItem FindNextMediaItem(MediaItem mediaItem);
        IEnumerable<ClassifiedEntry> FindNextEntries(ClassifiedEntry entry);
        ClassifiedEntry FindPreviousEntry(TVShowEpisode episode);
        ClassifiedEntry FindLastEpisode(TVShowSeason show);
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

        public ClassifiedEntry FindPreviousEntry(TVShowEpisode entry)
        {
            return FindPreviousEpisode(entry as TVShowEpisode);
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
                    .SkipWhile(m =>
                    {
                        var skip = m.Id != movie.Id;
                        if (skip)
                            mediaLibrary.Release(m);
                        return skip;
                    }))
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
                    .SkipWhile(e =>
                    {
                        var skip = e.Id != episode.Id;
                        if (skip)
                            mediaLibrary.Release(e);
                        return skip;
                    }))
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
            mediaLibrary.Release(entry);
            entry = FindNextEntry(entry);
            if (entry is null) return null;

            return (entry as IMediaItemCollectionEntry).MediaItemIds
                .Select(id => mediaLibrary.GetMediaItem(id))
                .Where(mi => mi is not null)
                .Where(mi =>
                {
                    var use = mi.CopyType == MediaItemCopyType.Original
                        || mi.CopyType == MediaItemCopyType.Download
                        || mi.CopyType == MediaItemCopyType.Cache;
                    if (use)
                        mediaLibrary.Release(mi);
                    return use;
                })
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
                .SkipWhile(m =>
                {
                    var skip = m.Id != movie.Id;
                    if (skip)
                        mediaLibrary.Release(m);
                    return skip;
                })
                .SkipWhile(m => {
                    var skip = m.Id == movie.Id;
                    if (skip)
                        mediaLibrary.Release(m);
                    return skip;
                })
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
        public ClassifiedEntry FindFirstEpisode(TVShowSeason season)
        {
            if (season is null) return null;
            return FindEpisodes(season)
                .FirstOrDefault();
        }
        public ClassifiedEntry FindLastEpisode(TVShowSeason season)
        {
            if (season is null) return null;
            return FindEpisodes(season)
                .OrderBy(e => e.Episode)
                .ThenBy(e => e.Part)
                .LastOrDefault();
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
                .SkipWhile(e =>
                {
                    var skip = e.Id != episode.Id;
                    if (skip)
                        mediaLibrary.Release(e);
                    return skip;
                })
                .SkipWhile(e => {
                    var skip = e.Id == episode.Id;
                    if (skip)
                        mediaLibrary.Release(e);
                    return skip;
                })
                .FirstOrDefault();
            if (episode is not null)
                return episode;

            var show = mediaLibrary.GetTVShow(season.ShowId);
            season = mediaLibrary.GetSeasons(show.Id)
                .OrderBy(s => s.Number)
                .ThenBy(s => s.Name)
                .SkipWhile(s =>
                {
                    var skip = s.Id != season.Id;
                    if (skip)
                        mediaLibrary.Release(s);
                    return skip;
                })
                .SkipWhile(s => {
                    var skip = s.Id == season.Id;
                    if (skip)
                        mediaLibrary.Release(s);
                    return skip;
                })
                .FirstOrDefault();
            return FindFirstEpisode(season);
        }

        private ClassifiedEntry FindPreviousEpisode(TVShowEpisode episode)
        {
            if (episode is null) return null;
            var season = mediaLibrary.GetTVShowSeason(episode.SeasonId);
            if (season is null) return null;
            var episodes = mediaLibrary.GetEpisodes(season.Id)
                .OrderByDescending(e => e.Episode)
                .ThenBy(e => e.Part)
                .ThenBy(e => e.Name)
                .ToArray();
            episode = episodes
                .SkipWhile(e =>
                {
                    var skip = e.Id != episode.Id;
                    if (skip)
                        mediaLibrary.Release(e);
                    return skip;
                })
                .SkipWhile(e => {
                    var skip = e.Id == episode.Id;
                    if (skip)
                        mediaLibrary.Release(e);
                    return skip;
                })
                .FirstOrDefault();
            if (episode is not null)
                return episode;

            var show = mediaLibrary.GetTVShow(season.ShowId);
            season = mediaLibrary.GetSeasons(show.Id)
                .OrderByDescending(s => s.Number)
                .ThenBy(s => s.Name)
                .SkipWhile(s => { 
                    var skip = s.Id != season.Id;
                    if (skip)
                        mediaLibrary.Release(s);
                    return skip;
                })
                .SkipWhile(s => {
                    var skip = s.Id == season.Id;
                    if (skip)
                        mediaLibrary.Release(s);
                    return skip;
                })
                .FirstOrDefault();
            return FindLastEpisode(season);
        }
    }
}
