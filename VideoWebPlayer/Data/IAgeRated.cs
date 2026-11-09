using System;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Gemeinsame Altersfreigabe-Eigenschaften von <see cref="Movie"/> und <see cref="TVShow"/>.
    /// </summary>
    public interface IAgeRated
    {
        /// <summary>
        /// Gets or sets the normalized age rating for display (e.g. "FSK 12" or "PG-13").
        /// </summary>
        string? AgeRating { get; set; }

        /// <summary>
        /// Gets or sets the unmodified age rating source value (e.g. "Rated PG-13" or "DE:16").
        /// </summary>
        string? AgeRatingRaw { get; set; }

        /// <summary>
        /// Gets or sets the comparable minimum age derived from the rating, if known.
        /// </summary>
        int? AgeRatingMinAge { get; set; }

        /// <summary>
        /// Gets or sets a value indicating when (or whether) the age rating has been classified.
        /// </summary>
        DateTime? AgeRatingClassifiedAt { get; set; }
    }
}
