using VideoPlayer.Service.Database.Models;

namespace VideoPlayer.Service.Library.Models
{
    [DataModelReference(typeof(DataGenreName))]
    public class GenreName : BaseServiceModel
    {
        public GenreName(DataGenreName dataModel) : base(dataModel)
        {
            if (DataModel is not null)
            {
                GenreId = ((DataGenreName)DataModel).DataGenreId;
            }
        }
        public long GenreId { get => GetProperty<long>(); set => SetProperty(value); }

        protected override void AssignChanges()
        {
            base.AssignChanges();
            if (DataModel is null) return;
            ((DataGenreName)DataModel).DataGenreId = GenreId;
        }
    }
}
