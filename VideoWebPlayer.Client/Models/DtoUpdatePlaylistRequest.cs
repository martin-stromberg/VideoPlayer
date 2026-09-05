namespace VideoWebPlayer.Client.Models
{
    public class DtoUpdatePlaylistRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? SortMode { get; set; }
    }
}
