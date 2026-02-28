using System.ComponentModel.DataAnnotations;
using VideoWebPlayer.Data;

/// <summary>
/// Represents an alternate name for a genre.
/// </summary>
public class GenreName
{
    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    [Key]
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the alternate name.
    /// </summary>
    [Required]
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the genre identifier.
    /// </summary>
    [Required]
    public long GenreId { get; set; }
    /// <summary>
    /// Gets or sets the genre navigation property.
    /// </summary>
    public Genre Genre { get; set; }
}