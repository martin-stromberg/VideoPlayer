using System;
using System.Linq;
using VideoPlayer.Service.Database.Models;

namespace VideoPlayer.Service.Library.Models.Classified
{

    [DataModelReference(
        typeof(DataClassifiedEntry),
        ReferenceFieldName = nameof(ClassifiedEntry.Type),
        ReferenceFieldValue = nameof(EntryType.MovieCollection))]
    public class MovieCollection: ClassifiedEntry
    {

        public MovieCollection(DataClassifiedEntry dataModel)
            : base(dataModel, EntryType.MovieCollection)
        {
            if (DataModel is not null)
            {
                PicturePath = ((DataClassifiedEntry)DataModel).PicturePath;
                BannerPath = ((DataClassifiedEntry)DataModel).BannerPath;
                MediaItemCollectionId = ((DataClassifiedEntry)DataModel).MediaItemCollectionId;
                IsSingle = ((DataClassifiedEntry)DataModel).IsSingle;
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

        public bool IsSingle
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

        protected override void AssignChanges()
        {
            base.AssignChanges();
            if (DataModel is null)
                return;
            ((DataClassifiedEntry)DataModel).PicturePath = PicturePath;
            ((DataClassifiedEntry)DataModel).BannerPath = BannerPath;
            ((DataClassifiedEntry)DataModel).MediaItemCollectionId = MediaItemCollectionId;
            ((DataClassifiedEntry)DataModel).IsSingle = IsSingle;
        }

    }
}
