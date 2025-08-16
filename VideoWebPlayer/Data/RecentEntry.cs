public class RecentEntry
{
    public long Id { get; set; }
    public long MediaSourceId { get; set; } // NEU: Quelle
    public DateTime PublishedAt { get; set; }
    public RecentEntryType Type { get; set; } // Movie, MovieCollection, TVShow, TVShowSeason, TVShowEpisode
    public long? MovieId { get; set; }
    public long? MovieCollectionId { get; set; }
    public long? TVShowId { get; set; }
    public long? TVShowSeasonId { get; set; }
    public long? TVShowEpisodeId { get; set; }
}
public enum RecentEntryType
{
    Movie,
    MovieCollection,
    TVShow,
    TVShowSeason,
    TVShowEpisode
}