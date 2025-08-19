using System.ComponentModel.DataAnnotations;

namespace VideoWebPlayer.Data
{
    public class Genre
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public long MediaSourceId { get; set; }
        public MediaSource MediaSource { get; set; }
        public DateTime? StartDate { get; set; } // optional, null = immer sichtbar
        public DateTime? EndDate { get; set; } // optional, null = immer sichtbar
        public ICollection<GenreName> AlternateNames { get; set; }
        public ICollection<MovieGenre> MovieGenres { get; set; } // Hinzugefügt für die Beziehung zu MovieGenre
        public ICollection<TVShowGenre> TVShowGenres { get; set; } // Optional: für die Beziehung zu TVShowGenre
        public bool IsHidden { get; set; }
    }
}