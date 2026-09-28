namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// Request body for changing a playlist's sort mode.
    /// </summary>
    public class DtoChangeSortModeRequest
    {
        /// <summary>
        /// Gets or sets the new sort mode, e.g. <see cref="PlaylistSortModeValues.Manual"/>.
        /// </summary>
        public string NewSortMode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the caller confirms the loss of the current manual order, in case
        /// switching away from <see cref="PlaylistSortModeValues.Manual"/> would discard it.
        /// </summary>
        public bool? ConfirmLossOfManualOrder { get; set; }
    }
}
