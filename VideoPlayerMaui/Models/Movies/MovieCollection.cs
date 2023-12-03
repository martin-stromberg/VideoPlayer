using VideoPlayer.Services.Database;

namespace VideoPlayer.Models.Movies
{
    [DataModelReference(typeof(Services.Database.Models.MovieCollection))]
    public class MovieCollection: BaseModel
    {

        public string PicturePath
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
                if (value == null)
                    Picture = null;
                else
                    Picture = ImageSource.FromFile(value);
            }
        }

        public ImageSource Picture
        {
            get
            {
                return GetProperty<ImageSource>();
            }
            set
            {
                SetProperty<ImageSource>(value);
            }
        }

        public long MediaItemCollectionId
        {
            get
            {
                return GetProperty<long>();
            }
            set
            {
                SetProperty<long>(value);
            }
        }

        public bool IsSingleMovie
        {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
            }
        }

    }
}
