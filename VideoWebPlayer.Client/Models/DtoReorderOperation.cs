namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// A single entry reorder operation as part of a batch reorder request.
    /// </summary>
    public class DtoReorderOperation
    {
        /// <summary>
        /// Gets or sets the identifier of the playlist entry to reorder.
        /// </summary>
        public long EntryId { get; set; }

        /// <summary>
        /// Gets or sets the new sort order value for the entry.
        /// </summary>
        public long NewSortOrder { get; set; }
    }
}
