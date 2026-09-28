using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Records that the user deliberately removed a specific (media type, media id) reference from a
    /// specific playlist (via <see cref="Services.PlaylistService.RemoveMediaFromPlaylistAsync"/>), so that
    /// the automatic backfill mechanism (<see cref="Services.PlaylistBackfillService"/>) - which would
    /// otherwise re-add it the moment it is next seen as a cascade child of a TVShow/TVShowSeason/
    /// MovieCollection entry still contained in the playlist - knows to leave it out.
    /// </summary>
    /// <remarks>
    /// Deliberately keyed the same way as <see cref="PlaylistEntry"/> itself (playlist + media type + media
    /// id), not by <see cref="PlaylistEntry.Id"/>: the excluded entry no longer exists as a row once removed,
    /// and the whole point of this table is to remember the exclusion past that point.
    /// <para>
    /// The exclusion is lifted again the moment the user manually (re-)adds that same (media type, media id)
    /// reference to the same playlist via <see cref="Services.PlaylistService.AddMediaToPlaylistAsync"/> -
    /// either directly, or as a cascade child of a collection the user adds (e.g. re-adding an entire TV
    /// show after having previously removed one specific episode from it). That manual add is itself an
    /// unambiguous, explicit inclusion decision by the user, which should take precedence over the earlier
    /// removal; without lifting it, a title the user explicitly asked to have in the playlist again would
    /// forever be invisible to future backfill runs for no discoverable reason.
    /// </para>
    /// </remarks>
    public class PlaylistEntryExclusion
    {
        /// <summary>
        /// Gets or sets the exclusion record identifier.
        /// </summary>
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the owning playlist identifier.
        /// </summary>
        [ForeignKey(nameof(Playlist))]
        public long PlaylistId { get; set; }

        /// <summary>
        /// Gets or sets the owning playlist.
        /// </summary>
        public Playlist Playlist { get; set; } = null!;

        /// <summary>
        /// Gets or sets the media type (one of the values in <c>MediaTypeValues</c>) of the excluded reference.
        /// </summary>
        [Required]
        public required string MediaType { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the excluded media content.
        /// </summary>
        public long MediaId { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the user removed this reference from the playlist.
        /// </summary>
        public DateTime ExcludedAt { get; set; } = DateTime.UtcNow;
    }
}
