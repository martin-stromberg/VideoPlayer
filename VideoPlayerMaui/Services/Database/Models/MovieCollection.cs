namespace VideoPlayer.Services.Database.Models
{
    public class MovieCollection: BaseDataModel
    {

        public string PicturePath { get; set; }

        public long MediaItemCollectionId { get; set; }

        public bool IsSingleMovie { get; set; }

    }
}