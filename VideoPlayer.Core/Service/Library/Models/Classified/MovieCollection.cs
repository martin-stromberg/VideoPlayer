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
                Genres = (((DataClassifiedEntry)DataModel).Genre is null) ? (new string[0]) : ((DataClassifiedEntry)DataModel).Genre
                                                                                                                              .Split(',')
                                                                                                                              .Where(g =>
                                                                                                                                     !string.IsNullOrWhiteSpace(g))
                                                                                                                              .ToArray();
                PicturePath = ((DataClassifiedEntry)DataModel).PicturePath;
                BannerPath = ((DataClassifiedEntry)DataModel).BannerPath;
                MediaItemCollectionId = ((DataClassifiedEntry)DataModel).MediaItemCollectionId;
                IsSingle = ((DataClassifiedEntry)DataModel).IsSingle;
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
        public string PictureBackgroundColor
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
        public string BannerBackgroundColor
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
        public string[] Genres
        {
            get
            {
                return GetProperty<string[]>();
            }
            set
            {
                SetProperty<string[]>(value);
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
            ((DataClassifiedEntry)DataModel).Genre = string.Join(',', Genres);
            ((DataClassifiedEntry)DataModel).BannerBackgroundColor = BannerBackgroundColor;
            ((DataClassifiedEntry)DataModel).PictureBackgroundColor = PictureBackgroundColor;
        }

    }
}
