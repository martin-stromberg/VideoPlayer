using Mediathek.Services.Database;
using System;
using System.Linq;

namespace Mediathek.Models.Overview
{
    [DataModelReference(typeof(Services.Database.Models.OverviewElement))]
    public class OverviewElement: BaseModel
    {

        public string Type { get; set; }

        public long OriginalId { get; set; }

        public int Year { get; set; }

        public string Genre1 { get; set; }

        public string Genre2 { get; set; }

        public string Genre3 { get; set; }

        public string Genre4 { get; set; }

        public string Genre5 { get; set; }

        [Path(nameof(PicturePath))]
        public ImageSource Picture { get; set; }

        public string PicturePath { get; set; }

        public DateTime LastUpdate { get; set; }

        public bool Delete { get; set; }

        internal bool Update(TVShow show)
        {
            if (show is null)
                return false;
            return Update(show.Genres.Split(','), show.PicturePath, show.PremieredAt.Year);
        }

        internal bool Update(TVShowCollection collection, TVShow[] shows)
        {
            if (collection is null)
                return false;
            return Update(shows
                            .SelectMany(show => show.Genres.Split(','))
                            .Distinct()
                            .ToArray(),
                          shows.FirstOrDefault(show => !string.IsNullOrWhiteSpace(show.PicturePath))?.PicturePath,
                          shows.Min(show => show.PremieredAt.Year));
        }

        internal bool Update(Movie movie)
        {
            if (movie is null)
                return false;
            return Update(movie.Genres.Split(','), movie.PicturePath, movie.PremieredAt.Year);
        }

        internal bool Update(MovieCollection collection, Movie[] movies)
        {
            if (collection is null)
                return false;
            return Update(movies
                            .SelectMany(show => show.Genres.Split(','))
                            .Distinct()
                            .ToArray(),
                          movies.FirstOrDefault(show => !string.IsNullOrWhiteSpace(show.PicturePath))?.PicturePath,
                          movies.Min(show => show.PremieredAt.Year));
        }

        private bool Update(string[] newGenres, string newPicturePath, int newYear)
        {
            newGenres = newGenres.Distinct().Take(5).ToArray();
            var currGenres = new string[] { Genre1, Genre2, Genre3, Genre4, Genre5 }.Where(g => g is not null).ToArray();
            var changed = (Year != newYear) || (PicturePath != newPicturePath) || !currGenres.SequenceEqual(newGenres);
            if (!changed)
                return changed;
            Year = newYear;
            PicturePath = newPicturePath;
            Genre1 = newGenres.Skip(0).FirstOrDefault();
            Genre2 = newGenres.Skip(1).FirstOrDefault();
            Genre3 = newGenres.Skip(2).FirstOrDefault();
            Genre4 = newGenres.Skip(3).FirstOrDefault();
            Genre5 = newGenres.Skip(4).FirstOrDefault();
            return changed;
        }

    }
}
