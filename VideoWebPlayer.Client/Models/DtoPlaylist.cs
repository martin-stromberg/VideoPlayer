namespace VideoWebPlayer.Client.Models
{
    public class DtoPlaylist
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string SortMode { get; set; } = PlaylistSortModeValues.ByReleaseDate;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
