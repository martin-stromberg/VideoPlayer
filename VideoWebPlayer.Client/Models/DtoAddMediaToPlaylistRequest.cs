namespace VideoWebPlayer.Client.Models
{
    public class DtoAddMediaToPlaylistRequest
    {
        public string MediaType { get; set; } = string.Empty;
        public long MediaId { get; set; }
    }
}
