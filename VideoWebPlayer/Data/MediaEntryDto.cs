namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Daten-Transfer-Objekt für MediaEntry.
    /// </summary>
    public class MediaEntryDto
    {
        public long Id { get; set; }
        public string Title { get; set; }
        public string? Description { get; set; }
        public string? Url { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? PictureId { get; set; } // oder PosterPictureId
        public int ItemCount { get; set; }
    }
}
