using VideoWebPlayer.Data;

public class MediaSourceUser
{
    public long MediaSourceId { get; set; }
    public MediaSource MediaSource { get; set; }
    public string UserId { get; set; }
    public ApplicationUser User { get; set; }
}