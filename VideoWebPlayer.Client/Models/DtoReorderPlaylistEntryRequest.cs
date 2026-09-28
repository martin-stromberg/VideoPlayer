namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// Request body for reordering a single playlist entry.
    /// </summary>
    public class DtoReorderPlaylistEntryRequest
    {
        /// <summary>
        /// Gets or sets the new sort order value for the entry.
        /// </summary>
        public long NewSortOrder { get; set; }
    }
}
