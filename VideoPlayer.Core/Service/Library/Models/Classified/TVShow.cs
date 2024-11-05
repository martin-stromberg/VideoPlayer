using System;
using VideoPlayer.Service.Database.Models;

namespace VideoPlayer.Service.Library.Models.Classified
{

    [DataModelReference(
        typeof(DataClassifiedEntry),
        ReferenceFieldName = nameof(ClassifiedEntry.Type),
        ReferenceFieldValue = nameof(EntryType.TVShow))]
    public class TVShow : TVShowEntry
    {
        public TVShow(DataClassifiedEntry dataModel) : base(dataModel, EntryType.TVShow)
        {
            if (DataModel is not null)
            {
                OriginalName = ((DataClassifiedEntry)DataModel).OriginalTitle;
                Language = ((DataClassifiedEntry)DataModel).Language;
                Plot = ((DataClassifiedEntry)DataModel).Plot;                
            }
        }

        public string OriginalName { get => GetProperty<string>(); set => SetProperty(value); }
        public string Language { get => GetProperty<string>(); set => SetProperty(value); }
        public string Plot { get => GetProperty<string>(); set => SetProperty(value); }
        

        protected override void AssignChanges()
        {
            base.AssignChanges();
            if (DataModel is null)
                return;
            ((DataClassifiedEntry)DataModel).OriginalTitle = OriginalName;
            ((DataClassifiedEntry)DataModel).Plot = Plot;
            ((DataClassifiedEntry)DataModel).Language = Language;            
        }

    }
}
