namespace WebPlayerApi.Models
{
    public class MediaItem: BaseDataModel
    {
        public string Title { get; set; }
        public MediaType Type { get; set; }
        public string FilePath { get; set; }
        public string? NfoContent { get; set; }
        public List<string> ImagePaths { get; set; } = new();
        public string? ParentId { get; set; } // For episodes or collection members
        public byte[] Picture { get; set; }
        public MediaItem[] Children { get; set; }
        public string? Plot { get; set; }
        public DateTime ReleaseDate { get; set; }        
        internal MediaDirectory Source { get; set; }
    }

}
