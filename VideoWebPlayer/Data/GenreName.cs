using System.ComponentModel.DataAnnotations;
using VideoWebPlayer.Data;

public class GenreName
{
    [Key]
    public long Id { get; set; }

    [Required]
    public string Name { get; set; } = "";

    [Required]
    public long GenreId { get; set; }
    public Genre Genre { get; set; }
}