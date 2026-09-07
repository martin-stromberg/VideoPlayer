namespace VideoWebPlayer.Client.Models
{
    public class DtoBatchReorderPlaylistEntriesRequest
    {
        public List<DtoReorderOperation> ReorderOperations { get; set; } = new();
    }
}
