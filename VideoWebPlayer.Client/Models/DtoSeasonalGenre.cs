namespace VideoWebPlayer.Client.Models;

/// <summary>A nonempty seasonal home section selected by the server.</summary>
public sealed class DtoSeasonalGenre
{
    /// <summary>Gets or sets the stable genre identifier.</summary>
    public long GenreId { get; set; }
    /// <summary>Gets or sets the section heading.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Gets or sets the exclusive UTC expiry supplied by the server.</summary>
    public DateTime ExpiresAt { get; set; }
    /// <summary>Gets or sets the ordered media cards.</summary>
    public List<DtoMediaEntry> Entries { get; set; } = [];
}
