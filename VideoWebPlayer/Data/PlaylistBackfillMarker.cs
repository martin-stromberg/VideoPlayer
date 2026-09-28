using System.ComponentModel.DataAnnotations;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Persistent "has new children" mark on a collection medium (TV show, TV show season or movie
    /// collection): written by <see cref="ApplicationDbContext"/> in the same transaction that saves a newly
    /// created (or re-assigned) child - a new season/episode of a show, a new movie in a collection - and
    /// consumed by <see cref="Services.PlaylistBackfillCoordinator"/>, which delivers the new child to exactly
    /// those playlists that contain the marked collection medium as a collection entry
    /// (<see cref="Services.PlaylistBackfillService"/>).
    /// </summary>
    /// <remarks>
    /// One row per (<see cref="MediaType"/>, <see cref="MediaId"/>) (unique index), so marking the same
    /// collection medium many times (a scan creating hundreds of episodes of one season) stays idempotent.
    /// The <see cref="Version"/> counter is incremented on every re-mark; the backfill removes a mark only
    /// if its <see cref="Version"/> is still the one it processed, so a mark set while the backfill was
    /// running is never lost (it stays for the next run).
    /// </remarks>
    public class PlaylistBackfillMarker
    {
        /// <summary>
        /// Gets or sets the marker identifier.
        /// </summary>
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the media type (<c>TVShow</c>, <c>TVShowSeason</c> or <c>MovieCollection</c>, see <c>MediaTypeValues</c>) of the marked collection medium.
        /// </summary>
        [Required]
        public required string MediaType { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the marked collection medium.
        /// </summary>
        public long MediaId { get; set; }

        /// <summary>
        /// Gets or sets the time (UTC) the collection medium was last marked.
        /// </summary>
        public DateTime MarkedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the mark version: 1 when created, incremented on every further mark. Used for the
        /// race-free "remove only what was processed" deletion.
        /// </summary>
        public long Version { get; set; } = 1;
    }
}
