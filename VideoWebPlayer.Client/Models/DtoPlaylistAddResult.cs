namespace VideoWebPlayer.Client.Models
{
    public class DtoPlaylistAddResult
    {
        public DtoPlaylistEntry? TopLevelEntry { get; set; }
        public DtoPlaylistEntry[] AddedEntries { get; set; } = Array.Empty<DtoPlaylistEntry>();
        public int SkippedDuplicateCount { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
