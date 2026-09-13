namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// A single entry of a playlist, referencing the media it points to.
    /// </summary>
    public class DtoPlaylistEntry
    {
        /// <summary>
        /// Gets or sets the entry's unique identifier.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the playlist this entry belongs to.
        /// </summary>
        public long PlaylistId { get; set; }

        /// <summary>
        /// Gets or sets the type of the referenced media entity (e.g. movie, TV show or episode).
        /// </summary>
        public string MediaType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the identifier of the referenced media entity.
        /// </summary>
        public long MediaId { get; set; }

        /// <summary>
        /// Gets or sets the title of the referenced media entity.
        /// </summary>
        public string MediaTitle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of the parent media entity (e.g. the TV show a season belongs
        /// to), or <c>null</c> if the referenced media entity has no parent.
        /// </summary>
        public string? ParentMediaType { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the parent media entity, or <c>null</c> if the
        /// referenced media entity has no parent.
        /// </summary>
        public long? ParentMediaId { get; set; }

        /// <summary>
        /// Gets or sets the title of the parent media entity, or <c>null</c> if the referenced
        /// media entity has no parent.
        /// </summary>
        public string? ParentMediaTitle { get; set; }

        /// <summary>
        /// Gets or sets the timestamp at which the entry was added to the playlist.
        /// </summary>
        public DateTime AddedAt { get; set; }

        /// <summary>
        /// Gets or sets the id of the picture to display for the referenced media entity, already
        /// resolved server-side: the poster picture, falling back to its banner or fanart picture
        /// if no poster is set; <c>null</c> if none of those are available. Unlike
        /// <c>PosterPictureId</c> on other DTOs (e.g. <see cref="DtoMovie"/>), this value may
        /// already be a banner or fanart id, since the fallback happens on the server rather than
        /// client-side.
        /// </summary>
        public long? ResolvedPictureId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the current user has unlocked access to the
        /// referenced media entity.
        /// </summary>
        public bool IsAccessible { get; set; } = true;

        /// <summary>
        /// Gets or sets the manual sort order of the entry within its playlist, populated when the
        /// owning playlist's <c>SortMode</c> is <c>Manual</c>; <c>null</c> for playlists sorted by
        /// <c>ByReleaseDate</c>.
        /// </summary>
        public long? SortOrder { get; set; }
    }
}
