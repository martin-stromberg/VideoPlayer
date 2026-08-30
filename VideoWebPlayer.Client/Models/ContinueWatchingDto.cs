namespace VideoWebPlayer.Client.Models
{
    public class ContinueWatchingDto
    {
        public string MediaType { get; set; } = "";
        public DtoMediaEntry Entry { get; set; }
        public long PositionSeconds { get; set; }
        public long? DurationSeconds { get; set; }
        public string Title { get; set; } = "";
        public long? PosterPictureId { get; set; }
        public DateTime? WatchedAt { get; set; }
    }
}
