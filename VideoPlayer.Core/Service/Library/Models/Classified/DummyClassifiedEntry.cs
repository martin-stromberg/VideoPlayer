using VideoPlayer.Service.Database.Models;

namespace VideoPlayer.Service.Library.Models.Classified
{
    [DataModelReference(
        typeof(DataClassifiedEntry),
        ReferenceFieldName = nameof(ClassifiedEntry.Type),
        ReferenceFieldValue = nameof(EntryType.Dummy))]
    public class DummyClassifiedEntry : ClassifiedEntry
    {
        public DummyClassifiedEntry() 
            : base(null, EntryType.None)
        {
        }
    }
}
