namespace VideoWebPlayer.Controllers.Models
{
    /// <summary>
    /// Daten-Transfer-Objekt fuer MediaEntry.
    /// </summary>
    public class MediaEntryDto
    {
        public string Type { get; set; }
        public long Id { get; set; }
        public string Title { get; set; }
        public string? Description { get; set; }
        public string? Url { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? PictureId { get; set; } // oder PosterPictureId
        public int ItemCount { get; set; }
        public DateTime? WatchedAt { get; set; }
        
    }
}
