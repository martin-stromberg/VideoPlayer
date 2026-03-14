using SQLite;

namespace VideoWebPlayer.Maui.Models;

[Table("CarouselCache")]
public class CachedCarouselItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string CarouselName { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public long EntryId { get; set; }

    public string? MediaType { get; set; }

    public string? Title { get; set; }

    public string? ImageUrl { get; set; }

    public long? PosterPictureId { get; set; }

    public long? SeasonId { get; set; }

    public long? EpisodeId { get; set; }

    public DateTime CachedAt { get; set; }
}
