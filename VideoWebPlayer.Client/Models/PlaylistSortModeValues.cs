namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// Central string constants for the playlist sort modes, mirroring <c>VideoWebPlayer.Data.PlaylistSortMode</c>.
    /// Used at the client/server JSON boundary, where the sort mode is transferred as a string, to avoid
    /// duplicating the raw literal values across DTOs and Razor components.
    /// </summary>
    public static class PlaylistSortModeValues
    {
        /// <summary>
        /// Entries are sorted by release date.
        /// </summary>
        public const string ByReleaseDate = "ByReleaseDate";

        /// <summary>
        /// Entries are sorted manually by the user.
        /// </summary>
        public const string Manual = "Manual";

        /// <summary>
        /// Returns the German display label for the given sort mode value ("Manuell" or "Nach
        /// Erscheinungsdatum"), so <c>PlaylistDetail</c> and <c>PlaylistsList</c> do not each duplicate
        /// the same ternary.
        /// </summary>
        /// <param name="sortMode">The raw sort mode value, e.g. from <see cref="DtoPlaylist.SortMode"/>.</param>
        /// <returns>The German display label.</returns>
        public static string GetDisplayLabel(string? sortMode)
            => sortMode == Manual ? "Manuell" : "Nach Erscheinungsdatum";
    }
}
