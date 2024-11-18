using VideoPlayer.Service.Database.Models;

namespace VideoPlayer.Service.Library.Models
{
    [DataModelReference(typeof(DataRole))]
    public class Role: BaseServiceModel
    {
        public Role(BaseDataModel dataModel) : base(dataModel)
        {
            if (dataModel is not null)
            {
                EntryId = ((DataRole)DataModel).EntryId;
                ActorId = ((DataRole)DataModel).ActorId;
            }
        }
        protected override void AssignChanges()
        {
            base.AssignChanges();
            if (DataModel is not null)
            {
                ((DataRole)DataModel).EntryId = EntryId;
                ((DataRole)DataModel).ActorId = ActorId;
            }
        }
        public long EntryId { get => GetProperty<long>(); set => SetProperty(value); }
        public long ActorId { get => GetProperty<long>(); set => SetProperty(value); }
        public Actor Actor { get => GetProperty<Actor>(); set => SetProperty(value); }
    }
}
