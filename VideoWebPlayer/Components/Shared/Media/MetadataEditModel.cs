using System.ComponentModel.DataAnnotations;

namespace VideoWebPlayer.Components.Shared.Media;

/// <summary>
/// Form model for editable media metadata.
/// </summary>
public sealed class MetadataEditModel
{
    /// <summary>
    /// Gets or sets the edited title.
    /// </summary>
    [Required(ErrorMessage = "Der Titel darf nicht leer sein.")]
    [StringLength(512, ErrorMessage = "Der Titel darf maximal 512 Zeichen lang sein.")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the edited date.
    /// </summary>
    public DateTime? Date { get; set; }

    /// <summary>
    /// Gets or sets the edited plot.
    /// </summary>
    [StringLength(10000, ErrorMessage = "Der Plot darf maximal 10000 Zeichen lang sein.")]
    public string Plot { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional comma-separated genre names.
    /// </summary>
    public string NewGenres { get; set; } = string.Empty;
}
