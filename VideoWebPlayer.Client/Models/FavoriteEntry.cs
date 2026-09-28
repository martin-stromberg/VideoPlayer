using System.ComponentModel.DataAnnotations;

namespace VideoWebPlayer.Data
{
    /// <summary>
    /// A single favorite entry of a user.
    /// </summary>
    public class DtoFavoriteEntry
    {
        /// <summary>
        /// Gets or sets the unique identifier of the favorite entry.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the media entry that was marked as favorite.
        /// </summary>
        public DtoMediaEntry Entry { get; set; } = null!;

        /// <summary>
        /// Gets or sets the point in time the entry was marked as favorite.
        /// </summary>
        public DateTime CreatedAt { get; set; }

    }
}