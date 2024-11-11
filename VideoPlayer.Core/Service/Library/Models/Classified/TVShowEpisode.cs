using VideoPlayer.Service.Database.Models;

namespace VideoPlayer.Service.Library.Models.Classified
{
    [DataModelReference(
        typeof(DataClassifiedEntry),
        ReferenceFieldName = nameof(ClassifiedEntry.Type),
        ReferenceFieldValue = nameof(EntryType.TVShowEpisode))]
    public class TVShowEpisode : TVShowEntry, 
        IMediaItemCollectionEntry, 
        IDownloadableEntry,
        IPlayableEntry,
        IPicturedEntry
    {
        public TVShowEpisode(DataClassifiedEntry dataModel) : base(dataModel, EntryType.TVShowEpisode)
        {
            if (DataModel is not null)
            {
                DownloadMediaItemId = ((DataClassifiedEntry)DataModel).DownloadMediaItemId;
                Language = ((DataClassifiedEntry)DataModel).Language;
                OriginalName = ((DataClassifiedEntry)DataModel).OriginalTitle;
                Episode = ((DataClassifiedEntry)DataModel).Number;
                Part = ((DataClassifiedEntry)DataModel).PartNo;
                Plot = ((DataClassifiedEntry)DataModel).Plot;
                SeasonId = ((DataClassifiedEntry)DataModel).CollectionId;
                ShowName = ((DataClassifiedEntry)DataModel).ShowName;
                SeasonNo = ((DataClassifiedEntry)DataModel).SeasonNo;
            }
        }

        protected override void AssignChanges()
        {
            base.AssignChanges();
            if (DataModel is null)
                return;
            ((DataClassifiedEntry)DataModel).DownloadMediaItemId = DownloadMediaItemId;
            ((DataClassifiedEntry)DataModel).Language = Language;
            ((DataClassifiedEntry)DataModel).OriginalTitle = OriginalName;
            ((DataClassifiedEntry)DataModel).Number = Episode;
            ((DataClassifiedEntry)DataModel).PartNo = Part;
            ((DataClassifiedEntry)DataModel).Plot = Plot;
            ((DataClassifiedEntry)DataModel).CollectionId = SeasonId;
            ((DataClassifiedEntry)DataModel).ShowName = ShowName;
            ((DataClassifiedEntry)DataModel).SeasonNo = SeasonNo;
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

        public string Language { get; set; }
        public string OriginalName { get; set; }
        public int Episode { get; set; }
        public string Part { get; set; }
        public string Plot { get; set; }
        public long[] MediaItemIds { get; set; } = new long[0];
        public long SeasonId { get; set; }

        public string[] Genres => new string[0];

        public string ShowName { get; set; }
        public int SeasonNo { get; set; }
    }
}
