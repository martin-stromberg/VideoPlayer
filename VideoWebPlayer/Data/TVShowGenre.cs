using VideoWebPlayer.Data;

public class TVShowGenre
{
    public long TVShowId { get; set; }
    public TVShow TVShow { get; set; }

    public long GenreId { get; set; }
    public Genre Genre { get; set; }
}