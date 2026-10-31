namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// A single broadcast slot of the automated TV channel as reported by the program guide
    /// ("Programmauskunft"): the resolved content plus the airing window. When the entry describes
    /// the currently running broadcast, <see cref="PositionSeconds"/> and
    /// <see cref="RemainingSeconds"/> carry the progress within the video; both are
    /// <see langword="null"/> for upcoming broadcasts.
    /// </summary>
    public class DtoProgramGuideEntry
    {
        /// <summary>
        /// Gets or sets the identifier of the underlying program slot.
        /// </summary>
        public long SlotId { get; set; }

        /// <summary>
        /// Gets or sets the display title of the aired content (movie name or
        /// "Show — Season, E{number}: episode"), or "Unbekannt" when the soft content reference
        /// no longer resolves.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of the aired content (<see cref="MediaTypeValues.Movie"/> or
        /// <see cref="MediaTypeValues.TVShowEpisode"/>), or <see langword="null"/> when the slot
        /// references no resolvable content kind.
        /// </summary>
        public string? MediaType { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the aired content (movie or episode id), or
        /// <see langword="null"/> when the slot references no resolvable content kind.
        /// </summary>
        public long? MediaId { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the underlying media item (the video file), needed by
        /// the live-stream delivery.
        /// </summary>
        public long MediaItemId { get; set; }

        /// <summary>
        /// Gets or sets whether the slot was produced by a filler rule.
        /// </summary>
        public bool IsFiller { get; set; }

        /// <summary>
        /// Gets or sets the UTC start of the broadcast.
        /// </summary>
        public DateTimeOffset StartUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC end of the broadcast.
        /// </summary>
        public DateTimeOffset EndUtc { get; set; }

        /// <summary>
        /// Gets or sets the broadcast length in seconds.
        /// </summary>
        public double DurationSeconds { get; set; }

        /// <summary>
        /// Gets or sets the seconds already aired — set only for the currently running broadcast,
        /// otherwise <see langword="null"/>.
        /// </summary>
        public double? PositionSeconds { get; set; }

        /// <summary>
        /// Gets or sets the seconds until the broadcast ends — set only for the currently running
        /// broadcast, otherwise <see langword="null"/>.
        /// </summary>
        public double? RemainingSeconds { get; set; }

        /// <summary>
        /// Gets or sets the poster picture identifier for the aired content (movie poster; for
        /// episodes the first available of episode, season or show poster), or
        /// <see langword="null"/> when the slot has no image — the picture itself is served by
        /// <c>GET /api/pictures/{id}</c>.
        /// </summary>
        public long? PosterPictureId { get; set; }

        /// <summary>
        /// Gets or sets the plot description of the aired content (NFO <c>plot</c> of the movie
        /// or episode, falling back to the show's plot for episodes without one), or
        /// <see langword="null"/> when no description is stored.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the first genre name of the aired content (movie genres or the show's
        /// genres for episodes), or <see langword="null"/> when no genre is stored.
        /// </summary>
        public string? GenreName { get; set; }

        /// <summary>
        /// Gets or sets the show name when the slot airs a TV show episode — the card title for
        /// series broadcasts — or <see langword="null"/> for movies and unresolved content.
        /// </summary>
        public string? ShowName { get; set; }

        /// <summary>
        /// Gets or sets the season name (e.g. "Staffel 6" or "Specials") when the slot airs a TV
        /// show episode, otherwise <see langword="null"/>.
        /// </summary>
        public string? SeasonName { get; set; }

        /// <summary>
        /// Gets or sets the season number read from <see cref="SeasonName"/> when the slot airs a
        /// TV show episode, or <see langword="null"/> when the name carries no digits.
        /// </summary>
        public int? SeasonNumber { get; set; }

        /// <summary>
        /// Gets or sets the episode number within its season when the slot airs a TV show
        /// episode, otherwise <see langword="null"/>.
        /// </summary>
        public int? EpisodeNumber { get; set; }

        /// <summary>
        /// Gets or sets the episode title when the slot airs a TV show episode, otherwise
        /// <see langword="null"/>.
        /// </summary>
        public string? EpisodeName { get; set; }
    }
}
