namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// Response returned when changing a playlist's sort mode would cause loss of the current manual
    /// order and requires the caller to confirm.
    /// </summary>
    public class DtoChangeSortModeConflictResponse
    {
        /// <summary>
        /// Gets or sets whether the caller must confirm the loss of the current manual order before the
        /// sort mode change can be applied.
        /// </summary>
        public bool IsLossOfDataConfirmationRequired { get; set; }
    }
}
