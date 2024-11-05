using VideoPlayer.Service.Database.Models;

namespace VideoPlayer.Service.Library.Models.Classified
{
    public class TVShowEntry : ClassifiedEntry, IPicturedEntry
    {
        public TVShowEntry(DataClassifiedEntry dataModel, EntryType type) : base(dataModel, type)
        {
            if (DataModel is not null)
            {
                PicturePath = ((DataClassifiedEntry)DataModel).PicturePath;
                BannerPath = ((DataClassifiedEntry)DataModel).BannerPath;
                BannerBackgroundColor = ((DataClassifiedEntry)DataModel).BannerBackgroundColor;
                PictureBackgroundColor = ((DataClassifiedEntry)DataModel).PictureBackgroundColor;
            }
        }

        public string PicturePath
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
            }
        }

        public string BannerPath
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
            }
        }

        public string BannerBackgroundColor { get => GetProperty<string>(); set => SetProperty(value); }
        public string PictureBackgroundColor { get => GetProperty<string>(); set => SetProperty(value); }

        protected override void AssignChanges()
        {
            base.AssignChanges();
            if (DataModel is null)
                return;
            ((DataClassifiedEntry)DataModel).PicturePath = PicturePath;
            ((DataClassifiedEntry)DataModel).BannerPath = BannerPath;
            ((DataClassifiedEntry)DataModel).BannerBackgroundColor = BannerBackgroundColor;
            ((DataClassifiedEntry)DataModel).PictureBackgroundColor = PictureBackgroundColor;
        }
    }
}
