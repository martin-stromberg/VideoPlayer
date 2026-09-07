namespace VideoWebPlayer.Client.Models
{
    public class DtoPlaylistEntriesPagedResult
    {
        public DtoPlaylistEntry[] Entries { get; set; } = Array.Empty<DtoPlaylistEntry>();
        public int TotalCount { get; set; }
        public bool HasNextPage { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }
}
