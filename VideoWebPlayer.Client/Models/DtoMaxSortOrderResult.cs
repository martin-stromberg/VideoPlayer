namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// Response of <c>GET /api/playlists/{id}/entries/max-sort-order</c>. Wrapped in an object (rather
    /// than returning a bare <c>long?</c> body) because the shared <c>VideoWebPlayerClient</c>
    /// deserialization helper treats a top-level JSON <c>null</c> body as a deserialization failure, which
    /// a bare nullable value would produce whenever a playlist has no manually sorted entries yet.
    /// </summary>
    public class DtoMaxSortOrderResult
    {
        public long? MaxSortOrder { get; set; }
    }
}
