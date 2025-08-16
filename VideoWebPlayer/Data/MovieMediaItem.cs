using VideoWebPlayer.Data;

public class MovieMediaItem
{
    public long MovieId { get; set; }
    public Movie Movie { get; set; } = null!;

    public long MediaItemId { get; set; }
    public MediaItem MediaItem { get; set; } = null!;
}