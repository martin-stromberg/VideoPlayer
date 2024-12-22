using VideoPlayer.Service.Database.Models;

namespace VideoPlayer.Service.Library.Models.Classified
{
    [DataModelReference(
        typeof(DataClassifiedEntry),
        ReferenceFieldName = nameof(ClassifiedEntry.Type),
        ReferenceFieldValue = nameof(EntryType.TVShowSeason))]
    public class TVShowSeason : TVShowEntry
    {
        public TVShowSeason(DataClassifiedEntry dataModel) : base(dataModel, EntryType.TVShowSeason)
        {
            if (DataModel is not null)
            {
                Number = ((DataClassifiedEntry)DataModel).Number;
                ShowId = ((DataClassifiedEntry)DataModel).CollectionId;
                PictureBackgroundColor = ((DataClassifiedEntry)DataModel).PictureBackgroundColor;
                BannerBackgroundColor = ((DataClassifiedEntry)DataModel).BannerBackgroundColor;
                ShowName = ((DataClassifiedEntry)DataModel).ShowName;
            }
        }

        public int Number 
        {
            get
            {
                return GetProperty<int>();
            }
            set
            {
                SetProperty<int>(value);
            }
        }

        public long ShowId {
            get
            {
                return GetProperty<long>();
            }
            set
            {
                SetProperty<long>(value);
            }
        }

        public string ShowName {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
            }
        }

        protected override void AssignChanges()
        {
            base.AssignChanges();
            if (DataModel is null)
                return;
            ((DataClassifiedEntry)DataModel).Number = Number;
            ((DataClassifiedEntry)DataModel).CollectionId = ShowId;
            ((DataClassifiedEntry)DataModel).PictureBackgroundColor = PictureBackgroundColor;
            ((DataClassifiedEntry)DataModel).BannerBackgroundColor = BannerBackgroundColor;
            ((DataClassifiedEntry)DataModel).ShowName = ShowName;
        }
        public override string ToString()
        {
            return $"Staffel {Number}";
        }
    }
}
