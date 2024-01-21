namespace Mediathek.Models.TVShows
{
    public class TVShowName: BaseModel
    {

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

        public override string ToString()
        {
            return $"{Name}";
        }

        public static TVShowName FromDataModel(Services.Database.Models.TVShow show)
        {
            return new TVShowName() { Id = show.Id, Name = show.Name, CollectionId = show.CollectionId };
        }

    }
}
