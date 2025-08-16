namespace VideoWebPlayer.Data
{
    public static class MediaSourceExtensions
    {
        /// <summary>
        /// Aktualisiert die Eigenschaften dieser MediaSource mit den Werten einer anderen Instanz.
        /// </summary>
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
