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

        // Placeholder for future license/access checking; the server currently always returns true.
        public bool IsAccessible { get; set; } = true;
    }
}
