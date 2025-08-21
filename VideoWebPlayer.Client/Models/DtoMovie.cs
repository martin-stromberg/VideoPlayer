using System.Text.Json.Serialization;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(DtoMediaEntry), "mediaEntry")]
[JsonDerivedType(typeof(DtoMovie), "movie")]
[JsonDerivedType(typeof(DtoMovieCollection), "movieCollection")]
[JsonDerivedType(typeof(DtoTVShow), "show")]
[JsonDerivedType(typeof(DtoTVShowSeason), "season")]
[JsonDerivedType(typeof(DtoTVShowEpisode), "episode")]
public class DtoMediaEntry
{
    public long Id { get; set; }
    public string Name { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public DateTime? PremieredAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public long? PosterPictureId { get; set; }
    public long? BannerPictureId { get; set; }
    public long? FanartPictureId { get; set; }
    public DtoPicture? PosterPicture { get; set; }
    public DtoPicture? BannerPicture { get; set; }
    public DtoPicture? FanartPicture { get; set; }
    public bool IsFavorite { get; set; }
}
public class DtoMovie: DtoMediaEntry
{
    public string? GenreNames { get; set; }
    public string? Plot { get; set; }
    public DtoMovieCollection? Collection { get; set; }
}
public class DtoMovieCollection : DtoMediaEntry
{
    [IgnoreAssignProperty]
    public DtoMovie[] Movies { get; set; }
}
public class DtoTVShow : DtoMediaEntry
{
    [IgnoreAssignProperty]
    public DtoTVShowSeason[] Seasons { get; set; }
    public string? GenreNames { get; set; }
    public string? Plot { get; set; }
}
public class DtoTVShowSeason : DtoMediaEntry
{
    public DtoTVShow? Show { get; set; }
    [IgnoreAssignProperty]
    public DtoTVShowEpisode[] Episodes { get; set; }
}
public class DtoTVShowEpisode : DtoMediaEntry
{
    public DtoTVShowSeason? Season { get; set; }
    public int Number { get; set; }
    public string? Plot { get; set; }
}
public class DtoPicture
{
    public long Id { get; set; }
    public long MediaItemId { get; set; } // Verweis auf die eigentliche Bilddatei
    public string Type { get; set; } // z.B. "poster", "banner", "fanart", "thumb"
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? Description { get; set; }
    public byte[] Data { get; set; }
    public string ContentType { get; set; }
}
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class IgnoreAssignPropertyAttribute: Attribute
{
    // Dieses Attribut kann verwendet werden, um bestimmte Eigenschaften bei der Serialisierung zu ignorieren
}