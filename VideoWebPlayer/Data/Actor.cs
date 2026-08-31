using System;
using System.Collections.Generic;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Represents an actor (cast member) with optional portrait picture.
    /// </summary>
    public class Actor
    {
        /// <summary>
        /// Gets or sets the actor identifier.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the normalized name used for duplicate detection and search.
        /// </summary>
        public string NormalizedName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the optional portrait picture identifier.
        /// </summary>
        public long? PictureId { get; set; }

        /// <summary>
        /// Gets or sets the optional portrait picture.
        /// </summary>
        public Picture? Picture { get; set; }

        /// <summary>
        /// Gets or sets the created timestamp.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets the movie link entries.
        /// </summary>
        public ICollection<MovieActor> MovieActors { get; set; } = new List<MovieActor>();

        /// <summary>
        /// Gets the TV show episode link entries.
        /// </summary>
        public ICollection<TVShowEpisodeActor> TVShowEpisodeActors { get; set; } = new List<TVShowEpisodeActor>();
    }
}
