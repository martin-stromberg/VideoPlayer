using VideoWebPlayer.Data;

public class TVShowEpisodeMediaItem
{
    public long TVShowEpisodeId { get; set; }
    public TVShowEpisode TVShowEpisode { get; set; } = null!;

    public long MediaItemId { get; set; }
    public MediaItem MediaItem { get; set; } = null!;
}