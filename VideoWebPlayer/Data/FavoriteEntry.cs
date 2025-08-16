using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace VideoWebPlayer.Data
{
    public class FavoriteEntry
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public string UserId { get; set; } // Identity-User

        public long? MovieCollectionId { get; set; }
        public long? TVShowId { get; set; }
        public long? TVShowSeasonId { get; set; }
        public long? TVShowEpisodeId { get; set; }
        public long? MovieId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
    }
}