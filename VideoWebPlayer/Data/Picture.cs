namespace VideoWebPlayer.Data
{
    public class Picture
    {
        public long Id { get; set; }
        public long MediaItemId { get; set; } // Verweis auf die eigentliche Bilddatei
        public string Type { get; set; } // z.B. "poster", "banner", "fanart", "thumb"
        public int? Width { get; set; }
        public int? Height { get; set; }
        public string? Description { get; set; }
        public MediaItem MediaItem { get; set; }
        public byte[] Data { get; set; }
        public string ContentType { get; set; }
    }
}