namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// Request body for reordering multiple playlist entries in a single batch.
    /// </summary>
    public class DtoBatchReorderPlaylistEntriesRequest
    {
        /// <summary>
        /// Gets or sets the individual reorder operations to apply.
        /// </summary>
        /// <value>The list of reorder operations.</value>
        public List<DtoReorderOperation> ReorderOperations { get; set; } = new();
    }
}
