using System;
using System.Linq;
using VideoPlayer.Service.Database.Models;

namespace VideoPlayer.Service.Library.Models.Classified
{
    public enum EntryType
    {

        Movie = 0,
        TVShow = 10,
        TVShowSeason = 11,
        TVShowEpisode = 12,
        MovieCollection = 100,
        TVShowCollection = 110,
        None = 999,
    }

    [DataModelReference(
        typeof(DataClassifiedEntry),
        ReferenceFieldName = nameof(ClassifiedEntry.Type),
        ReferenceFieldValue = nameof(EntryType.Movie))]
    public class Movie: ClassifiedEntry, 
        IMediaItemCollectionEntry, 
        IPicturedEntry, 
        IDownloadableEntry, 
        IPlayableEntry
    {

        public Movie(DataClassifiedEntry dataModel)
            : base(dataModel, EntryType.Movie)
        {
            MediaItemIds = new long[0];
            if (DataModel is not null)
            {
                Genres = (((DataClassifiedEntry)DataModel).Genre is null) ? (new string[0]) : ((DataClassifiedEntry)DataModel).Genre
                                                                                                                              .Split(',')
                                                                                                                              .Where(g =>
                                                                                                                                     !string.IsNullOrWhiteSpace(g))
                                                                                                                              .ToArray();
                Plot = ((DataClassifiedEntry)DataModel).Plot;
                PicturePath = ((DataClassifiedEntry)DataModel).PicturePath;
                BannerPath = ((DataClassifiedEntry)DataModel).BannerPath;
                CollectionId = ((DataClassifiedEntry)DataModel).CollectionId;
                TrailerMediaItemId = ((DataClassifiedEntry)DataModel).TrailerMediaItemId;
                DownloadMediaItemId = ((DataClassifiedEntry)DataModel).DownloadMediaItemId;
                IsSingle = ((DataClassifiedEntry)DataModel).IsSingle;
                Language = ((DataClassifiedEntry)DataModel).Language;
                OriginalTitle = ((DataClassifiedEntry)DataModel).OriginalTitle;
                BannerBackgroundColor = ((DataClassifiedEntry)DataModel).BannerBackgroundColor;
                PictureBackgroundColor = ((DataClassifiedEntry)DataModel).PictureBackgroundColor;
                Director = ((DataClassifiedEntry)DataModel).Director;
            }
        }

        public string OriginalTitle
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

        public string Plot
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
        public long CollectionId
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

        public long TrailerMediaItemId
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

        public long DownloadMediaItemId
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

        public string Language
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

        public long[] MediaItemIds
        {
            get
            {
                return GetProperty<long[]>();
            }
            set
            {
                SetProperty<long[]>(value);
            }
        }

        public string Director {
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
            ((DataClassifiedEntry)DataModel).OriginalTitle = OriginalTitle;
            ((DataClassifiedEntry)DataModel).Genre = string.Join(',', Genres);
            ((DataClassifiedEntry)DataModel).Plot = Plot;
            ((DataClassifiedEntry)DataModel).PicturePath = PicturePath;
            ((DataClassifiedEntry)DataModel).BannerPath = BannerPath;
            ((DataClassifiedEntry)DataModel).CollectionId = CollectionId;
            ((DataClassifiedEntry)DataModel).TrailerMediaItemId = TrailerMediaItemId;
            ((DataClassifiedEntry)DataModel).DownloadMediaItemId = DownloadMediaItemId;
            ((DataClassifiedEntry)DataModel).IsSingle = IsSingle;
            ((DataClassifiedEntry)DataModel).Language = Language;
            ((DataClassifiedEntry)DataModel).BannerBackgroundColor = BannerBackgroundColor;
            ((DataClassifiedEntry)DataModel).PictureBackgroundColor = PictureBackgroundColor;
            ((DataClassifiedEntry)DataModel).Director = Director;
        }

    }
}
