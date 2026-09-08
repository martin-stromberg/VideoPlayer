namespace VideoWebPlayer.Client.Models
{
    public class DtoPlaylistEntry
    {
        public long Id { get; set; }
        public long PlaylistId { get; set; }
        public string MediaType { get; set; } = string.Empty;
        public long MediaId { get; set; }
        public string MediaTitle { get; set; } = string.Empty;
        public string? ParentMediaType { get; set; }
        public long? ParentMediaId { get; set; }
        public string? ParentMediaTitle { get; set; }
        public DateTime AddedAt { get; set; }

        // Id of the picture to display for the referenced media entity, already resolved server-side:
        // the poster picture, falling back to its banner or fanart picture if no poster is set; null if
        // none of those are available. Unlike PosterPictureId on other DTOs (e.g. DtoMovie), this value
        // may already be a banner or fanart id, since the fallback happens on the server rather than
        // client-side.
        public long? ResolvedPictureId { get; set; }

        // Whether the current user has unlocked access to the referenced media entity.
        public bool IsAccessible { get; set; } = true;

        // Manual sort order of the entry within its playlist, populated when the owning playlist's
        // SortMode is Manual; null for playlists sorted by ByReleaseDate.
        public long? SortOrder { get; set; }
    }
}
