namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// Central string constants for the playlist sort modes, mirroring <c>VideoWebPlayer.Data.PlaylistSortMode</c>.
    /// Used at the client/server JSON boundary, where the sort mode is transferred as a string, to avoid
    /// duplicating the raw literal values across DTOs and Razor components.
    /// </summary>
    public static class PlaylistSortModeValues
    {
        public const string ByReleaseDate = "ByReleaseDate";
        public const string Manual = "Manual";
    }
}
