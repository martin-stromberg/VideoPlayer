using System.ComponentModel.DataAnnotations;

namespace VideoWebPlayer.Data
{
    public class Genre
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public string Name { get; set; } = "";

        [Required]
        public long MediaSourceId { get; set; }
        public MediaSource MediaSource { get; set; }

        public ICollection<GenreName> AlternateNames { get; set; } = new List<GenreName>();
    }
}