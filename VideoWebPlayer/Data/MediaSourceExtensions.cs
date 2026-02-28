namespace VideoWebPlayer.Data
{
/// <summary>
/// Extension helpers for media sources.
/// </summary>
public static class MediaSourceExtensions
    {
        /// <summary>
        /// Aktualisiert die Eigenschaften dieser MediaSource mit den Werten einer anderen Instanz.
        /// </summary>
    /// <summary>
    /// Updates the target media source with values from the source instance.
    /// </summary>
    /// <param name="target">The target media source.</param>
    /// <param name="source">The source media source.</param>
    public static void Update(this MediaSource target, MediaSource source)
        {
            target.Name = source.Name;
            target.Path = source.Path;
            target.Host = source.Host;
            target.Port = source.Port;
            target.Username = source.Username;
            target.Password = source.Password;
            target.LastScannedAt = source.LastScannedAt;
            // CreatedAt wird in der Regel nicht überschrieben
        }
    }
}
