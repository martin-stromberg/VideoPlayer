using System.Text.Json.Serialization;

/// <summary>
/// Base DTO for a media library entry (movie, movie collection, TV show, season or episode),
/// carrying the fields common to all of them.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(DtoMediaEntry), "mediaEntry")]
[JsonDerivedType(typeof(DtoMovie), "movie")]
[JsonDerivedType(typeof(DtoMovieCollection), "movieCollection")]
[JsonDerivedType(typeof(DtoTVShow), "show")]
[JsonDerivedType(typeof(DtoTVShowSeason), "season")]
[JsonDerivedType(typeof(DtoTVShowEpisode), "episode")]
public class DtoMediaEntry
{
    /// <summary>
    /// Gets or sets the unique identifier of the media entry.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the display name of the media entry.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the identifier of the media source the entry belongs to.
    /// </summary>
    public long MediaSourceId { get; set; }

    /// <summary>
    /// Gets or sets the release date of the media entry.
    /// </summary>
    public DateTime? ReleaseDate { get; set; }

    /// <summary>
    /// Gets or sets the date the media entry premiered (used for TV shows and seasons).
    /// </summary>
    public DateTime? PremieredAt { get; set; }

    /// <summary>
    /// Gets or sets the date the media entry ended (used for TV shows).
    /// </summary>
    public DateTime? EndedAt { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the poster picture, if any.
    /// </summary>
    public long? PosterPictureId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the banner picture, if any.
    /// </summary>
    public long? BannerPictureId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the fanart picture, if any.
    /// </summary>
    public long? FanartPictureId { get; set; }

    /// <summary>
    /// Gets or sets the poster picture.
    /// </summary>
    public DtoPicture? PosterPicture { get; set; }

    /// <summary>
    /// Gets or sets the banner picture.
    /// </summary>
    public DtoPicture? BannerPicture { get; set; }

    /// <summary>
    /// Gets or sets the fanart picture.
    /// </summary>
    public DtoPicture? FanartPicture { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the current user has marked the media entry as a favorite.
    /// </summary>
    public bool IsFavorite { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the media entry is unlocked for the current user.
    /// </summary>
    public bool IsUnlocked { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the metadata of the media entry has been manually edited.
    /// </summary>
    public bool IsManuallyEdited { get; set; }

    /// <summary>
    /// Gets or sets the date and time the media entry was last watched by the current user, if any.
    /// </summary>
    public DateTime? WatchedAt { get; set; }
}

/// <summary>
/// DTO for a selectable genre option, used to populate genre pickers in the UI.
/// </summary>
public sealed class DtoGenreOption
{
    /// <summary>
    /// Gets or sets the unique identifier of the genre.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the display name of the genre.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Request DTO for updating the editable metadata of a media entry.
/// </summary>
public sealed class MediaMetadataUpdateRequest
{
    /// <summary>
    /// Gets or sets the type of the media object being updated (e.g. movie, show, episode).
    /// </summary>
    public string ObjectType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the identifier of the media entry being updated.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the display name of the media entry.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the release date of the media entry.
    /// </summary>
    public DateTime? ReleaseDate { get; set; }

    /// <summary>
    /// Gets or sets the date the media entry premiered.
    /// </summary>
    public DateTime? PremieredAt { get; set; }

    /// <summary>
    /// Gets or sets the plot description of the media entry.
    /// </summary>
    public string? Plot { get; set; }

    /// <summary>
    /// Gets or sets the names of the genres assigned to the media entry.
    /// </summary>
    public string[] GenreNames { get; set; } = [];
}

/// <summary>
/// DTO for a movie, including its plot, genres and the collection it belongs to, if any.
/// </summary>
public class DtoMovie : DtoMediaEntry
{
    /// <summary>
    /// Gets or sets the comma-separated names of the genres assigned to the movie.
    /// </summary>
    public string? GenreNames { get; set; }

    /// <summary>
    /// Gets or sets the plot description of the movie.
    /// </summary>
    public string? Plot { get; set; }

    /// <summary>
    /// Gets or sets the movie collection the movie belongs to, if any.
    /// </summary>
    public DtoMovieCollection? Collection { get; set; }
}

/// <summary>
/// DTO for a movie collection, i.e. a group of related movies.
/// </summary>
public class DtoMovieCollection : DtoMediaEntry
{
    /// <summary>
    /// Gets or sets the movies contained in the collection.
    /// </summary>
    [IgnoreAssignProperty]
    public DtoMovie[] Movies { get; set; } = [];
}

/// <summary>
/// DTO for a TV show, including its plot, genres and seasons.
/// </summary>
public class DtoTVShow : DtoMediaEntry
{
    /// <summary>
    /// Gets or sets the seasons of the TV show.
    /// </summary>
    [IgnoreAssignProperty]
    public DtoTVShowSeason[] Seasons { get; set; } = [];

    /// <summary>
    /// Gets or sets the comma-separated names of the genres assigned to the TV show.
    /// </summary>
    public string? GenreNames { get; set; }

    /// <summary>
    /// Gets or sets the plot description of the TV show.
    /// </summary>
    public string? Plot { get; set; }
}

/// <summary>
/// DTO for a season of a TV show, including its episodes.
/// </summary>
public class DtoTVShowSeason : DtoMediaEntry
{
    /// <summary>
    /// Gets or sets the season number.
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    /// Gets or sets the TV show the season belongs to.
    /// </summary>
    public DtoTVShow? Show { get; set; }

    /// <summary>
    /// Gets or sets the episodes of the season.
    /// </summary>
    [IgnoreAssignProperty]
    public DtoTVShowEpisode[] Episodes { get; set; } = [];
}

/// <summary>
/// DTO for a single episode of a TV show season.
/// </summary>
public class DtoTVShowEpisode : DtoMediaEntry
{
    /// <summary>
    /// Gets or sets the season the episode belongs to.
    /// </summary>
    public DtoTVShowSeason? Season { get; set; }

    /// <summary>
    /// Gets or sets the episode number within its season.
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    /// Gets or sets the plot description of the episode.
    /// </summary>
    public string? Plot { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the automatically generated background picture for the episode, if any.
    /// </summary>
    public long? GeneratedBackgroundPictureId { get; set; }
}

/// <summary>
/// DTO for a picture (poster, banner, fanart or thumbnail) associated with a media entry.
/// </summary>
public class DtoPicture
{
    /// <summary>
    /// Gets or sets the unique identifier of the picture.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the underlying media item the picture file belongs to.
    /// </summary>
    public long MediaItemId { get; set; } // Verweis auf die eigentliche Bilddatei

    /// <summary>
    /// Gets or sets the type of the picture (e.g. "poster", "banner", "fanart", "thumb").
    /// </summary>
    public string Type { get; set; } = string.Empty; // z.B. "poster", "banner", "fanart", "thumb"

    /// <summary>
    /// Gets or sets the width of the picture in pixels, if known.
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Gets or sets the height of the picture in pixels, if known.
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// Gets or sets an optional description of the picture.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the raw image data.
    /// </summary>
    public byte[] Data { get; set; } = [];

    /// <summary>
    /// Gets or sets the MIME content type of the image data.
    /// </summary>
    public string ContentType { get; set; } = string.Empty;
}

/// <summary>
/// Marks a property so it is skipped when assigning values from one DTO instance onto another
/// (e.g. child collections that are populated separately).
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class IgnoreAssignPropertyAttribute : Attribute
{
    // Dieses Attribut kann verwendet werden, um bestimmte Eigenschaften bei der Serialisierung zu ignorieren
}
