/// <summary>
/// Represents application setup metadata.
/// </summary>
public class Setup
{
    /// <summary>
    /// Gets or sets the setup identifier.
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// Gets or sets the current data version.
    /// </summary>
    public int DataVersion { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether genres have changed.
    /// </summary>
    public bool GenresChanged { get; set; }
}