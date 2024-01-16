namespace Mediathek.Models.MetaInformation
{
    public class TVShowInformation: MediaInformation
    {

        public string Plot { get; set; }

        public string[] Genres { get; set; }

        public DateTime PremieredAt { get; set; }

        public string Language { get; set; }

    }
}
