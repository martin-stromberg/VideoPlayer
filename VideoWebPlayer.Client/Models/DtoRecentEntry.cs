public class DtoRecentEntry
{
    public long Id { get; set; }
    public long MediaSourceId { get; set; } // NEU: Quelle
    public DateTime PublishedAt { get; set; }
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    public RecentEntryType Type { get; set; } // Movie, MovieCollection, TVShow, TVShowSeason, TVShowEpisode
    public DtoMediaEntry Entry { get; set; }
}
public enum RecentEntryType
{
    Movie,
    MovieCollection,
    TVShow,
    TVShowSeason,
    TVShowEpisode
}