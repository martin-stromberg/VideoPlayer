using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VideoWebPlayer.Data
{
    public class ContinueWatchingEntry
    {
        [Key]
        public long Id { get; set; }

        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        public long? MovieId { get; set; }
        public long? TVShowEpisodeId { get; set; }

        [Required]
        public TimeSpan Position { get; set; }

        public TimeSpan? Duration { get; set; }

        public DateTime UpdatedAt { get; set; }

        [ForeignKey(nameof(MovieId))]
        public Movie? Movie { get; set; }

        [ForeignKey(nameof(TVShowEpisodeId))]
        public TVShowEpisode? TVShowEpisode { get; set; }
    }
}