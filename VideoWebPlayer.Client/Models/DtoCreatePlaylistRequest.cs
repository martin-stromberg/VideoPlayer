namespace VideoWebPlayer.Client.Models
{
    public class DtoCreatePlaylistRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? SortMode { get; set; } = PlaylistSortModeValues.ByReleaseDate;
    }
}
